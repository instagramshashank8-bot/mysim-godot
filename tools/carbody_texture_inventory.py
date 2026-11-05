"""Read-only texture inventory + fleet manifest for carbody/livery evidence (no rendering, no mutation).

Inventory mode (default):
Builds, for one car folder (or one track folder): every source texture's true pixel
size/colour type (PNG IHDR), its Godot import settings (.import), which prefab
renderers bind it (reverse lookup through assets/car_map.json), the per-surface
UV-repair policy state (data/car_surface_uv_repairs.json), and - when an assembly
audit JSON is supplied - the measured per-surface basisDeterminant / metresPerUv /
visibility joined onto the rows. Writes a markdown table and a JSON sidecar.

Manifest mode (--manifest):
Enumerates the WHOLE fleet + every track strictly from the catalog sources
(src/Logic/CarCatalog.cs, src/Logic/SourceRoutes.cs, the runtime TrackList.json)
with fail-closed verification asserts: catalog/car_map/car_pivots key-set equality,
per-car source folders with FBX+PNG presence, TrackList <-> SourceRoutes count and
name alignment, per-track scene/variation file existence, and the data/ vs runtime
TrackList divergence check. Exits 3 with the assert report on any mismatch - it
never falls back to folder guesses.

Usage:
  python tools/carbody_texture_inventory.py --index 16
  python tools/carbody_texture_inventory.py --car 2018_lamborghini_huracan_gt3
  python tools/carbody_texture_inventory.py --track Brands_Hatch --assembly <audit.json>
  python tools/carbody_texture_inventory.py --manifest --root <repo> [--out-md ...] [--out-json ...]

Exit codes: 0 ok, 2 bad arguments, 3 missing required input or failed assert (never a silent report).
"""
import argparse, json, pathlib, re, struct, sys

PNG_SIG = b"\x89PNG\r\n\x1a\n"
KNOWN_CHUNKS = {"IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB", "iCCP", "pHYs", "tIME", "tEXt", "zTXt", "iTXt", "bKGD"}


def png_info(path):
    data = path.read_bytes()
    if data[:8] != PNG_SIG:
        raise ValueError(f"not a PNG: {path}")
    width, height = struct.unpack(">II", data[16:24])
    bit_depth, colour_type = data[24], data[25]
    chunks, off = [], 8
    while off + 8 <= len(data) and len(chunks) < 4096:
        (length,) = struct.unpack(">I", data[off:off + 4])
        name = data[off + 4:off + 8].decode("latin-1", "replace")
        chunks.append(name)
        off += 12 + length
        if name == "IEND":
            break
    extra = [c for c in chunks if c not in KNOWN_CHUNKS]
    return {
        "width": width, "height": height, "bitDepth": bit_depth,
        "colourType": colour_type, "hasAlpha": colour_type in (4, 6),
        "iccOrGamma": [c for c in chunks if c in ("iCCP", "gAMA", "sRGB", "cHRM")],
        "unknownChunks": sorted(set(extra)), "bytes": len(data),
    }


def import_info(path):
    out = {"present": False}
    if not path.exists():
        return out
    text = path.read_text(encoding="utf-8", errors="replace")

    def grab(pattern, default=None, cast=str):
        m = re.search(pattern, text)
        return cast(m.group(1)) if m else default

    out = {
        "present": True,
        "importer": grab(r'importer="([^"]+)"'),
        "compressMode": grab(r"compress/mode=(\d+)", cast=int),
        "mipmaps": grab(r"mipmaps/generate=(\w+)"),
        "sizeLimit": grab(r"size_limit=(-?\d+)", cast=int),
        "detect3dTo": grab(r"detect_3d/compress_to=(\d+)", cast=int),
        "vramTexture": grab(r'"vram_texture": (\w+)'),
        "premultAlpha": grab(r"premult_alpha=(\w+)"),
        "fixAlphaBorder": grab(r"fix_alpha_border=(\w+)"),
    }
    return out


def car_catalog(root):
    """Parse src/Logic/CarCatalog.cs for the folder order (index -> folder)."""
    path = root / "src" / "Logic" / "CarCatalog.cs"
    text = path.read_text(encoding="utf-8", errors="replace")
    m = re.search(r"Folders=\{(.*?)\}", text, re.S)
    if not m:
        raise ValueError("CarCatalog Folders not found")
    folders = re.findall(r'"([^"]+)"', m.group(1))
    return {i: f for i, f in enumerate(folders)}


def load_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def reverse_bindings(car_map):
    """texture res-path -> list of {car, renderer, guid, slot} from the car map."""
    out = {}
    res = lambda p: ("res://assets/" + p.split("Assets/", 1)[1]) if p and "Assets/" in p else p
    for index, renderers in car_map.get("cars", {}).items():
        for renderer, lists in renderers.items():
            guids = lists[0] if lists else []
            for guid in guids:
                mat = car_map.get("materials", {}).get(guid, {})
                for slot, raw in (mat.get("texs") or {}).items():
                    path = res(raw)
                    if path:
                        out.setdefault(path, []).append(
                            {"car": int(index), "renderer": renderer, "guid": guid, "slot": slot})
    return out


def assembly_index(path):
    """renderer name -> measured fields from a CarAssemblyAudit JSON."""
    data = load_json(path)
    out = {}
    for row in data.get("rows", []):
        mats = row.get("materials") or []
        first = mats[0] if mats else {}
        uv0 = first.get("uv0") or {}
        out[row.get("name", "")] = {
            "visible": row.get("visible"),
            "basisDeterminant": row.get("basisDeterminant"),
            "miscTwinHidden": row.get("miscTwinHidden"),
            "metresPerUv0": uv0.get("metresPerUv"),
            "uv0Count": uv0.get("count"),
            "uv2Present": (first.get("uv2") or {}).get("present"),
            "boundTexture": first.get("texture"),
        }
    return out


def density_note(width, metres_per_uv):
    if not width or not metres_per_uv:
        return None
    return round(width / metres_per_uv, 1)  # texels per metre at mip0


# ------------------------------------------------------------- six-field schema
# Controlled vocabularies per EVIDENCE_INDEX.md §TEXTURE-INVENTORY QC (verification agent).
ORIENTATION_CLASSES = ("as-authored", "mirrored-X", "rotated-90", "transposed", "mixed-per-island", "indeterminate")
DEFECT_TAXONOMY = ("black-card", "alpha-bleed-dark-RGB", "mid-alpha-scissor-loss", "sub-pixel-coverage-collapse",
                   "mirrored-UV", "wrong-scale", "compression-artifact", "low-res-source", "none")
CORRECTION_FAMILIES = ("none-needed", "mip-rebuild", "uv-remap-per-island", "uv-remap-per-surface",
                       "threshold-change", "material-rebinding", "geometry-fix", "source-art-fix")

# The runtime flip policy, per src/CarLoader.cs (cars) and the TrackMaterialBinder rule (tracks):
# car shaders default to uv_scale=(-1,-1)/uv_offset=(1,1) for ALL surfaces (a 180-degree rotation:
# the V half restores the authored bottom-left origin under Godot's top-left upload, the U half is a
# horizontal mirror); track binders apply the same default to non-org_/non-crowd names.
# Repair entries (data/car_surface_uv_repairs.json) override per surface with V-only (1,-1)/(0,1).
DEFAULT_CAR_SCALE, DEFAULT_CAR_OFFSET = (-1.0, -1.0), (1.0, 1.0)


def orientation_class(scale, offset):
    """Map an effective UV transform onto the controlled orientation-class set."""
    s = (round(scale[0], 3), round(scale[1], 3))
    o = (round(offset[0], 3), round(offset[1], 3))
    if s == (-1.0, -1.0) and o == (1.0, 1.0):
        return "mirrored-X"  # 180-degree rotation: V corrected, U mirrored
    if s == (1.0, -1.0) and o == (0.0, 1.0):
        return "as-authored"  # V-only correction restores the authored orientation
    if s == (1.0, 1.0) and (o == (0.0, 0.0) or o is None):
        return "as-authored"
    return "indeterminate (transform %s/%s outside the known classes)" % (s, o)


# Documented, source-cited findings per car (from the recorded runs/reports; read-only annotations).
# Keys: car index -> texture file substring -> {defectTaxonomy, correctionFamily, evidence}.
DOCUMENTED_FINDINGS = {
    16: {
        "_mm_badges": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                       "evidence": "staged pending entries + runs 41306343412/41572315488/41788844369; turn-2 report H1"},
        "badges.png": {"defectTaxonomy": "low-res-source", "correctionFamily": "source-art-fix",
                       "evidence": "256x256 atlas carries all sponsor marks (turn-1 sec 1.2/1.5: no higher-res original exists in the recovery)"},
        "_mm_ext": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                    "evidence": "runs 41306343412 (uv states) + 41572315488 (V-only paired validates); the promoted trio fixed, 7 ext pending"},
        "ext_16.png": {"defectTaxonomy": "low-res-source", "correctionFamily": "source-art-fix",
                       "evidence": "1024x1024 for the whole body livery (~4-5 mm/texel; turn-1 sec 1.3); source ceiling - no higher-res original"},
        "_mm_misc": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                     "evidence": "default transform; mostly non-directional content so low visual impact (turn-1 sec 1.3)"},
        "_mm_chassis": {"defectTaxonomy": "sub-pixel-coverage-collapse", "correctionFamily": "none-needed",
                        "evidence": "WHEELGUARD_*_mm_chassis UV0 sliver (delta 0.0007, run 41510907196 rows); likely intentional flat; no correction pending source verification"},
        "wheel.png": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                      "evidence": "bound via LOD_A_WHEEL_mm_wheel after the role-alias fix (run 41573514607); rim lettering mirrored at the default transform (minor)"},
        "_mm_cab": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                    "evidence": "INTERIOR_mm_cab + STEERING_WHEEL_mm_cab ride the default transform: instrument/cabin text renders mirrored in the cockpit view (the cockpit-usability delegation to carbody-livery; the reviewer's 'instrument display not readable', VISUAL_REVIEW 09:10Z); cab-family candidates NOT staged yet - blocked on a cockpit-audit emitting INT-surface UV data (the assembly audits cover visible-only surfaces)"},
        "_mm_int": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                    "evidence": "the LOD_A_INT_* family at the default transform (hidden in the exterior flow; visible only in cockpit mode - pending the cockpit-audit extension)"},
        "_mm_sw": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                   "evidence": "SHIFT_LIT_* LED segments are non-directional (low impact); wheel-face labels/letters are directional and render mirrored"},
        "cab.png": {"defectTaxonomy": "low-res-source", "correctionFamily": "source-art-fix",
                    "evidence": "512x512 carries dash+cabin+wheel faces; instrument glyphs land at a few px at seated-eye distance (the cockpit readability complaint); conditional cloud-only upscale candidate per the R3 policy - measure at the cockpit camera first"},
        "int.png": {"defectTaxonomy": "low-res-source", "correctionFamily": "source-art-fix",
                    "evidence": "512x512 for the full interior cage; same conditional-upscale policy; no higher-res original local (turn-1 sec 1.5)"},
    },
    6: {
        "_mm_ext": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                    "evidence": "fleet default transform; car6 has no ext repairs (turn-1 sec 1.6; role comparison run 41015188520)"},
        "badges.png": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                       "evidence": "fleet default transform on all badge overlays (no car6 badge repairs staged)"},
        "_mm_misc": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                     "evidence": "HOOD misc FIXED (promoted, run 41023270551); other misc surfaces follow the default transform (role comparison 41015188520: misc-scope flips whiten wing struts - per-surface only)"},
    },
    0: {
        "_mm_ext": {"defectTaxonomy": "mirrored-UV", "correctionFamily": "uv-remap-per-surface",
                    "evidence": "fleet default transform; no car0 repairs (ASSEMBLED_CAR_REVIEW-era findings: dark rims/discontinuous shading)"},
    },
    15: {
        "_mm_ext": {"defectTaxonomy": "source-atlas-fragmentation", "correctionFamily": "source-art-fix",
                    "evidence": "FULL_F1_W10_WHEEL_REVIEW (run 41310403219): source body atlas fragmented; FOUR_CAR_PAINT_PARTIAL_REVIEW (run 41335778130 @960e56f): W10 body paint coherent silver/teal after the material change but plain body lacks final decals and wing/misc source surfaces still broken looking; qualityAccepted false"},
        "wheel": {"defectTaxonomy": "authored-detail-missing", "correctionFamily": "source-art-fix",
                  "evidence": "FULL_F1_W10_WHEEL_REVIEW (run 41310403219, 8 captures viewed): barrels plain bright, raised rings plastic, brake detail insufficient; NEUTRAL_GRADE_W10_SF90_RESULTS (run 41304169011 @e931c33): wet tread channels + rubber curvature confirmed, overall wheel art still rejected; F1_CUSTOM_WHEEL_PLAN.md/SPEC.json = proposed art direction, not accepted art"},
    },
    17: {
        "_mm_ext": {"defectTaxonomy": "source-atlas-fragmentation", "correctionFamily": "source-art-fix",
                    "evidence": "FULL_F1_RB15_WHEEL_REVIEW (run 41310403219): body atlas/wing decals fragmented; RB15_SIDEWALL_CORRECTION_REVIEW (run 41302933144): body livery/UV fragments remain, issue shared by all four F1 models; qualityAccepted false"},
        "wheel": {"defectTaxonomy": "authored-detail-missing", "correctionFamily": "source-art-fix",
                  "evidence": "FULL_F1_RB15_WHEEL_REVIEW (run 41310403219, all 8 views): distinct swept 10-spoke rim + compound markings + dry slicks/wet grooves visible, raised rings plastic-looking, barrel plain bright, caliper mostly occluded; RB15_SIDEWALL_CORRECTION_REVIEW (run 41302933144): sidewall marking placement verified (blue front Wet / yellow rear Medium) but rubber featureless without tread/sidewall detail; NEUTRAL_GRADE_RB15_MCL34_RESULTS (run 41304169011): grooves + curved rubber shading restored, art rejected"},
    },
    18: {
        "_mm_ext": {"defectTaxonomy": "source-atlas-fragmentation", "correctionFamily": "source-art-fix",
                    "evidence": "FULL_F1_MCL34_WHEEL_REVIEW (run 41310403219): body atlas/wing decals remain fragmented; COATED_PAINT_PARTIAL_REVIEW (run 41337127855 @ff93c22): broad bright body highlights, plain undetailed panels, distinct improvement from the material-only change not established; qualityAccepted false"},
        "wheel": {"defectTaxonomy": "authored-detail-missing", "correctionFamily": "source-art-fix",
                  "evidence": "FULL_F1_MCL34_WHEEL_REVIEW (run 41310403219, all 8 front/rear 4-compound views): distinct swept spokes + compound colours + wet drainage geometry visible, raised plastic rings / plain bright barrels / mostly occluded brakes unaccepted; F1_SIDEWALL_SF90_MCL34_REVIEW (run 41302933144): correct blue/yellow compound rings visible, rubber black without readable tread/sidewall detail"},
    },
}

# Defect -> correction family mapping for rule-derived defects (no documented override).
RULE_CORRECTION = {
    "mirrored-UV": "uv-remap-per-surface",
    "low-res-source": "source-art-fix",
    "sub-pixel-coverage-collapse": "geometry-fix",
    "none": "none-needed",
}


def documented_finding(car_index, texture_name):
    """Return the documented (defect, correction, evidence) for a car texture, or None."""
    table = DOCUMENTED_FINDINGS.get(car_index, {})
    for needle, finding in table.items():
        if needle in texture_name or texture_name.endswith(needle) or needle.endswith(texture_name):
            return finding
    return None


def load_guid2path_reverse(root):
    """assets-relative file path (no 'assets/' prefix) -> source GUID, from guid2path.json."""
    path = root / "guid2path.json"
    if not path.exists():
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    out = {}
    for guid, target in raw.items():
        text = str(target)
        if "Assets/RealRacing/" not in text:
            continue
        rel = text.split("Assets/", 1)[1]  # e.g. RealRacing/Source/<car>/<file>.png
        out.setdefault(rel, guid)  # first GUID wins; determinism over completeness
    return out


def unity_map_reverse(root):
    """texture res-path -> [ {guid, materialPath, shader} ] from assets/unity_map.json (tracks)."""
    path = root / "assets" / "unity_map.json"
    if not path.exists():
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    out = {}
    for guid, mat in (raw.get("materials") or {}).items():
        for slot, target in (mat.get("texs") or {}).items():
            if not target or "Assets/RealRacing/" not in str(target):
                continue
            rel = "assets/" + str(target).split("Assets/", 1)[1]
            out.setdefault(rel, []).append({"guid": guid, "materialPath": mat.get("path"), "slot": slot})
    return out


# ---------------------------------------------------------------- manifest mode

def quoted_braced(text, anchor):
    """Return the quoted strings of the first {...} block that follows `anchor`."""
    i = text.index(anchor)
    body = text[i:text.index("}", i)]
    return re.findall(r'"([^"]+)"', body)


def parse_catalog(root):
    """Cars strictly from src/Logic/CarCatalog.cs (id/folder/name + RaceClass mirror)."""
    src = (root / "src" / "Logic" / "CarCatalog.cs").read_text(encoding="utf-8")
    folders = quoted_braced(src, "Folders={")
    names = quoted_braced(src, "Names={")
    if len(folders) != len(names):
        raise AssertionError(f"CarCatalog Folders({len(folders)}) != Names({len(names)})")
    out = []
    for i, (folder, name) in enumerate(zip(folders, names)):
        race_class = 1 if 15 <= i <= 18 else 2 if i in (21, 22) else 3 if i in (19, 20, 23) else 0
        out.append({"id": i, "folder": folder, "name": name, "raceClass": race_class})
    return out


def parse_source_routes(root):
    """Track route names strictly from src/Logic/SourceRoutes.cs (runtime route order)."""
    src = (root / "src" / "Logic" / "SourceRoutes.cs").read_text(encoding="utf-8")
    return quoted_braced(src, 'Names={')


def norm_name(s):
    return " ".join(s.upper().split())


# Evidence-state annotations, recorded from REVIEW_PACKET session-4 (f03cfc1-era) 2026-11-06.
# Purely declarative: maps in-flight/completed runs to the inventory fields they complete,
# so fleet.json rows can carry a per-car completion state that survives regeneration.
EVIDENCE_ANNOTATIONS = {
    "fleetEvidence": {
        "runs": [
            {"run": 41848086075, "head": "b5ca3e0", "outcome": "SUCCESS but only shard 0 (jobs: prepare+render(0))"},
            {"run": 41862411919, "head": "eb3890f", "outcome": "SUCCESS but only shard 0 (jobs: prepare+render(0)) - re-dispatch reproduced the collapse"},
        ],
        "cause": "CONFIRMED at a313492: the fleet term sat inside the paren group opened at 'chiron_wing_rebuild' that closed after 'f1_wheel_diagnostic' followed by && '[0]' - the group evaluated to '[0,1,2,3,4,5]' and the trailing && '[0]' collapsed it to '[0]'",
        "fix": "FIXED at c4fb876 ('shard matrix: fix the fleet-grouping bug'); this agent re-verified mechanically - at c4fb876 the fleet term's enclosing paren is the fromJSON( itself (top-level) and the wet/f1_wheel_diagnostic group opens its own parenthesis; merged forward into the later integration heads",
        "next": "the NEXT post-fix fleet run (c4fb876 or later head, dispatched when the owner's queue drains) = the PRE-PROMOTION mirrored baseline (battery item in the P1 production-runbook); then promotion pending:true->false (NOT deletion); then the post-promotion fleet run",
        "coveredCars": [0, 6, 12, 18],
        "provides": "42/42 captures per run, missing=[] (5 views + 4 wheel closeups + cockpit per car + track0 day/night), PRODUCTION render state (SOURCE_UV_CANDIDATES=0, MISC_TWIN_HIDE=0)",
        "decryption": "WORKS locally: owner key C:/Users/shash/.codex/private/mysim-review/review-20261031.key via MYSIM_INTAKE_KEY (verified on the fleet shard-0 envelope: 42/42 PNGs; key never printed)",
        "doesNotProvide": "car16 (shard 4 material) has never been fleet-captured yet; no CarAssemblyAudit JSONs on this depth",
        "completes": ["fleet visual evidence for cars 0/6/12/18 (decrypted locally; visual verdicts with the human reviewer)"],
    },
    "assemblyAudit": {
        "cars15to18": {"runs": [41837315358, 41843317303], "state": "41837315358 SUCCESS (no-fit baseline; 275 poses/car); 41843317303 = axle-fit leg",
                        "artifacts": "emitted per-car *-assembly.json (metresPerUv/basisDeterminant/textOrientation); downloaded to the car-assembly agent's worktree (uncommitted there)"},
        "cars0to3": {"run": 41849732286, "outcome": "FAILURE (at 6505f16)", "retry": 41854428291, "state": "corrected retry FAILED at 6263c51; next batch pending"},
        "remaining": {"state": "further 4-car batches queued per the car-assembly turn-6 plan (6505f16)"},
    },
    "p1Badges": {
        "legA": {"run": 41870086370, "ref": "26d0191 (commit 'P1 leg A baseline rebased onto the current integration head': only the three 16/*_mm_badges entries removed)", "outcome": "SUCCESS",
                  "note": "v1 (41847896437 at adf89ca) died on the audit's HUD-viewport NameError (cloud_game_audit.py:389, undefined 'want'; fixed at current head); the rebased run captures the full 18-PNG set"},
        "legB": {"run": 41874911549, "ref": "a313492 (integration head; badges entries present, pending:true, candidates active via the depth)", "state": "SUCCESS (full 18-PNG set; no audit bug at this head)",
                  "caveat": "Q3 asked for exact-SHA isolation; the rebase makes the pair span 26d0191..a313492 whose delta (mounted-bake, collision-overlay fixes, user-path harness, gate wiring) is classified render-neutral for car_evidence but is NOT byte-identical lineage - the reviewer decides whether to accept or require the zero-confound option below",
                  "zeroConfoundOption": "scratch ref mistral/p1-badge-candidates = 26d0191 + the exact reverse patch (the three entries re-added); dispatch car_evidence there = leg B at the SAME base SHA as leg A"},
    },
    "productionStateComparison": {
        "purpose": "Q3 condition: the A/B legs both carry twin-hide+ext-candidate env; promotion must be validated at PRODUCTION state (no env)",
        "vehicle": "fleet_evidence depth (candidates=0, twin-hide=0 by construction) - the only production-state capture path for car16",
        "blockedBy": "RESOLVED: the fix landed at c4fb876 and is verified; car16 (shard 4) still needs its first fleet captures via the battery run",
        "sequence": ["1. DONE: paren fix at c4fb876 (verified)", "2. fleet_evidence at the post-fix head (c4fb876 or later; current at dispatch) = pre-promotion baseline (mirrored badges at production state; owner battery item)", "3. promotion commit: the three entries pending:true -> false (NOT deletion: deletion reverts to mirrored)", "4. fleet_evidence at the promoted head = production captures with corrected badges, no env", "5. per-marking comparison per the turn-4 verdict table + non-badge regression rows unchanged"],
        "baseline": "41798036351 decrypted set (locally available, e8b80bb, twin-hide state per its logs) doubles as the reviewed pre-fix baseline",
    },
}


def evidence_state_for(car_id):
    """Per-car completion state for the inventory fields (declarative, from EVIDENCE_ANNOTATIONS)."""
    if car_id in (15, 16, 17, 18):
        metrics = "measured by 41837315358/41843317303 (incorporation pending)"
    elif car_id in (0, 1, 2, 3):
        metrics = "41849732286 FAILED; retry 41854428291 also FAILED (at 6263c51); re-run pending"
    else:
        metrics = "pending-run-<wheel-sweep-batch>"
    if car_id in (0, 6, 12, 18):
        visuals = "captured-41848086075+41862411919-shard0 (42/42 twice; decrypted locally - visual verdicts with the reviewer)"
    else:
        visuals = "pending the post-fix fleet run (owner battery item = pre-promotion baseline for all 24 cars; shard-matrix fix landed at c4fb876, merged into later heads)"
    return {
        "fleetVisualEvidence": visuals,
        "assemblyMetrics(metresPerUv/basisDeterminant/textOrientation)": metrics,
        "uvSixFieldFill": "all 24 cars carry md+json six-field inventories (car16/car6/track0 + batch 1 = cars 0-5,7-11 + batch 2 = cars 12-15,17-23); the per-surface METRIC fields inside await the assembly-audit joins listed above",
    }


def manifest(root):
    """Enumerate the whole fleet + tracks from catalog sources; fail closed on mismatch.

    Exits 3 (after writing whatever partial report was requested) if any verification
    assert fails; exits 0 only when every assert passed.
    """
    problems, notes = [], []

    cars = parse_catalog(root)
    if len(cars) != 24:
        problems.append(f"catalog cars = {len(cars)}, expected 24")

    routes = parse_source_routes(root)

    runtime_tl = root / "assets" / "RealRacing" / "TrackList.json"
    if not runtime_tl.exists():
        problems.append(f"runtime TrackList missing: {runtime_tl}")
        tracks = []
    else:
        tracks = load_json(runtime_tl)["tracks"]

    repo_tl = root / "data" / "TrackList.json"
    both_lists = None
    if repo_tl.exists() and tracks:
        both_lists = load_json(repo_tl)["tracks"]
        if len(both_lists) != len(tracks):
            problems.append(f"TrackList divergence: data/ has {len(both_lists)} entries, assets/ (runtime) has {len(tracks)}")
        else:
            for i, (a, b) in enumerate(zip(both_lists, tracks)):
                for key in ("map", "layout", "scene", "name"):
                    if a.get(key) != b.get(key):
                        problems.append(f"TrackList divergence at {i} field {key}: data/={a.get(key)!r} assets/={b.get(key)!r}")

    car_map = load_json(root / "assets" / "car_map.json")
    car_pivots = load_json(root / "assets" / "car_pivots.json")
    repairs = load_json(root / "data" / "car_surface_uv_repairs.json") if (root / "data" / "car_surface_uv_repairs.json").exists() else {}

    # car asserts: key-set equality + source presence + pivots
    map_keys = set(car_map.get("cars", {}).keys())
    pivot_keys = set(car_pivots.keys())
    want_keys = {str(c["id"]) for c in cars}
    if map_keys != want_keys:
        problems.append(f"car_map cars keys != catalog: missing={sorted(want_keys - map_keys)} extra={sorted(map_keys - want_keys)}")
    if pivot_keys != want_keys:
        problems.append(f"car_pivots keys != catalog: missing={sorted(want_keys - pivot_keys)} extra={sorted(pivot_keys - want_keys)}")

    src_root = root / "assets" / "RealRacing" / "Source"
    car_rows = []
    for c in cars:
        folder = src_root / c["folder"]
        fbxs = sorted(p.name for p in folder.glob("*.fbx")) if folder.is_dir() else []
        pngs = sorted(str(p.relative_to(folder).as_posix()) for p in folder.rglob("*.png")) if folder.is_dir() else []
        if not fbxs:
            problems.append(f"car {c['id']} ({c['folder']}): no source FBX under {folder}")
        if not pngs:
            problems.append(f"car {c['id']} ({c['folder']}): no source textures under {folder}")
        entries = car_map.get("cars", {}).get(str(c["id"]), {})
        if not entries:
            problems.append(f"car {c['id']}: no car_map bindings")
        pivots = car_pivots.get(str(c["id"]))
        if not isinstance(pivots, list) or len(pivots) != 4:
            problems.append(f"car {c['id']}: car_pivots entry missing or not 4 pivots")
        livery_dir = folder / "livery"
        liveries = sorted(p.name for p in livery_dir.glob("*.png")) if livery_dir.is_dir() else []
        bound_livery = None
        for renderer, lists in entries.items():
            if "_mm_ext" not in renderer or not lists:
                continue
            for guid in lists[0]:
                tex = (car_map.get("materials", {}).get(guid, {}) or {}).get("texs") or {}
                base = tex.get("_BaseMap") or tex.get("_MainTex")
                if base and "livery/" in base.replace("\\", "/"):
                    bound_livery = base.split("/")[-1]
                break
            if bound_livery:
                break
        rep = {k: v for k, v in repairs.items() if k.startswith(f"{c['id']}/")}
        pending = sorted(k for k, v in rep.items() if v.get("pending"))
        promoted = sorted(k for k, v in rep.items() if not v.get("pending"))
        special = []
        if 15 <= c["id"] <= 18:
            special = ["f1_suspension_visual_builder", "tyre_compound_markings(formula-only)", "f1 rim/brake candidate branch", "f1_paint_candidate(opt-in)"]
        if c["id"] == 20:
            special = ["chiron_reconstructed_wing(opt-in)", "chiron_atlas_dihedral_diagnostic(opt-in)"]
        car_rows.append({
            "id": c["id"], "name": c["name"], "folder": f"assets/RealRacing/Source/{c['folder']}/",
            "raceClass": c["raceClass"],
            "fbx": len(fbxs), "textures": len(pngs), "liveriesOnDisk": liveries, "boundLivery": bound_livery,
            "boundRenderers": len(entries), "pivots": 4 if isinstance(pivots, list) else 0,
            "uvRepairsPromoted": promoted, "uvRepairsPending": pending, "specialPaths": special,
            "evidenceState": evidence_state_for(c["id"]),
        })

    # track asserts: runtime TrackList <-> SourceRoutes alignment + file existence
    track_rows = []
    if tracks:
        if len(routes) != len(tracks):
            problems.append(f"route count mismatch: SourceRoutes.Names={len(routes)} vs TrackList={len(tracks)}")
        for i, t in enumerate(tracks):
            route_name = routes[i] if i < len(routes) else None
            if route_name is not None and norm_name(route_name) != norm_name(t.get("name", "")):
                problems.append(f"route name mismatch at index {i}: SourceRoutes='{route_name}' TrackList='{t.get('name')}'")
            scene = t.get("scene") or ""
            map_dir = t.get("map") or ""
            scene_path = root / "assets" / "RealRacing" / "Source" / map_dir / scene
            scene_ok = scene_path.exists()
            if not scene_ok:
                problems.append(f"track {i} ({t.get('name')}): scene missing: {scene_path}")
            variations = []
            for vp in (t.get("variationPaths") or []):
                rp = str(vp).replace("Assets/RealRacing/", "assets/RealRacing/", 1)
                ok = (root / rp).exists()
                variations.append({"path": vp, "present": ok})
                if not ok:
                    problems.append(f"track {i} ({t.get('name')}): variation missing: {rp}")
            track_rows.append({
                "id": i, "name": t.get("name"), "map": map_dir, "layout": t.get("layout"),
                "scene": scene, "sceneBytes": scene_path.stat().st_size if scene_ok else None,
                "baseTrackIndex": t.get("baseTrackIndex"), "variationPaths": variations,
                "routeName": route_name, "sourceSplineSha": (t.get("sourceSpline") or "")[-16:],
                "hasAiPoints": bool(t.get("aiPoints")), "hasCentrePoints": bool(t.get("centrePoints")),
            })
    else:
        track_rows = []

    result = {
        "scope": "read-only fleet/track manifest from catalog sources (CarCatalog.cs, SourceRoutes.cs, runtime TrackList.json); no mutation, no rendering",
        "catalogSource": "src/Logic/CarCatalog.cs", "routesSource": "src/Logic/SourceRoutes.cs",
        "trackListRuntime": "assets/RealRacing/TrackList.json",
        "evidencePipeline": EVIDENCE_ANNOTATIONS,
        "counts": {"cars": len(cars), "tracks": len(tracks), "sourceRoutes": len(routes)},
        "assertsPassed": not problems, "problems": problems, "notes": notes,
        "cars": car_rows, "tracks": track_rows,
    }
    return result


def markdown_manifest(result):
    lines = ["# Fleet manifest tables (tool output; facts only)", "",
             f"cars={result['counts']['cars']} tracks={result['counts']['tracks']} sourceRoutes={result['counts']['sourceRoutes']} assertsPassed={result['assertsPassed']}", "",
             "## Cars", "", "| id | name | folder | class | fbx | textures | liveries | bound livery | repairs promoted/pending | special paths | evidence state |",
             "|---|---|---|---|---|---|---|---|---|---|---|"]
    for c in result["cars"]:
        es = c.get("evidenceState", {})
        lines.append(f"| {c['id']} | {c['name']} | {c['folder']} | {c['raceClass']} | {c['fbx']} | {c['textures']} | "
                     f"{len(c['liveriesOnDisk'])} | {c['boundLivery'] or '-'} | "
                     f"{len(c['uvRepairsPromoted'])}/{len(c['uvRepairsPending'])} | {'; '.join(c['specialPaths']) or '-'} | "
                     f"visuals:{es.get('fleetVisualEvidence', '-')}; metrics:{es.get('assemblyMetrics(metresPerUv/basisDeterminant/textOrientation)', '-')} |")
    lines += ["", "## Tracks", "", "| id | name | map/layout | scene present | route name | variations ok |", "|---|---|---|---|---|---|"]
    for t in result["tracks"]:
        ok = all(v["present"] for v in t["variationPaths"])
        lines.append(f"| {t['id']} | {t['name']} | {t['map']}/{t['layout']} | {t['sceneBytes'] is not None} | {t['routeName'] or '-'} | {ok} |")
    if result["problems"]:
        lines += ["", "## ASSERT FAILURES", ""]
        lines += [f"- {p}" for p in result["problems"]]
    return "\n".join(lines) + "\n"


CAR_ACCEPTANCE_PACKS = {
    16: [("car16 mirrored-UV pack", "outputs/mistral-orchestration/visual-review/car16-mirrored-uv-pack/PACK.md"),
         ("car16 wheel-detail pack", "outputs/mistral-orchestration/visual-review/car16-wheel-pack/WHEEL-PACK.md")],
    20: [("car20 carbody pack (11 promoted roles)", "outputs/mistral-orchestration/visual-review/car20-carbody-pack/CAR20-PACK.md")],
    15: [("F1 triad pack - car15 section", "outputs/mistral-orchestration/f1-triad-pack/F1-TRIAD-PACK.md"),
         ("car15 main strip", "outputs/mistral-orchestration/f1-triad-pack/images/car15-main.png"),
         ("car15 rubber closeups", "outputs/mistral-orchestration/f1-triad-pack/images/car15-wheels.png")],
    17: [("F1 triad pack - car17 section", "outputs/mistral-orchestration/f1-triad-pack/F1-TRIAD-PACK.md"),
         ("car17 main strip", "outputs/mistral-orchestration/f1-triad-pack/images/car17-main.png"),
         ("car17 rubber closeups", "outputs/mistral-orchestration/f1-triad-pack/images/car17-wheels.png")],
    18: [("F1 triad pack - car18 section", "outputs/mistral-orchestration/f1-triad-pack/F1-TRIAD-PACK.md"),
         ("car18 main strip", "outputs/mistral-orchestration/f1-triad-pack/images/car18-main.png"),
         ("car18 fleet wheel closeups", "outputs/mistral-orchestration/f1-triad-pack/images/car18-wheels.png")],
}
TRACK0_INDEX = ("track0 standing-rejects index", "outputs/mistral-orchestration/visual-review/track0-index/TRACK0-INDEX.md")


def cards_mode(root, args):
    """Per-car acceptance cards generated FROM the committed inventory jsons (+ catalog,
    repairs, packs): the 24-car acceptance pass infrastructure. Read-only."""
    src = pathlib.Path("outputs/mistral-orchestration/inventory")
    if not src.is_dir():
        src = root / "outputs/mistral-orchestration/inventory"
    files = sorted(src.glob("car*.json"), key=lambda p: int(re.sub(r"\D", "", p.stem)))
    if len(files) < 24:
        print(f"cards: only {len(files)} committed inventory jsons found at {src} - refusing (need 24)")
        return 3
    cat = parse_catalog(root)
    cars_by_idx = {}
    for row in (cat if isinstance(cat, list) else cat.get("cars", [])):
        cars_by_idx[row.get("id")] = row
    rep_data = json.loads((root / "data/car_surface_uv_repairs.json").read_text(encoding="utf-8"))
    rep_by_car = {}
    def collect(o):
        if isinstance(o, dict):
            for k, v in o.items():
                if isinstance(k, str) and re.fullmatch(r"(\d{1,2})/[A-Za-z0-9_]+", k) and isinstance(v, dict):
                    i = int(k.split("/")[0])
                    rep_by_car.setdefault(i, []).append((k, v))
                else:
                    collect(v)
        elif isinstance(o, list):
            for v in o:
                collect(v)
    collect(rep_data)
    out_dir = pathlib.Path(args.out_cards); out_dir.mkdir(parents=True, exist_ok=True)
    index = ["# 24-car acceptance cards", "",
             "Generated from the committed six-field inventories (24/24) by `tools/carbody_texture_inventory.py --cards`.", "",
             "**Standing rules:** in-tree/local evidence only; **cloud-UNVALIDATED**; no acceptance is implied by greps/inventories/cards - each card's verdict slot stays open for the human reviewer.", ""]
    for f in files:
        idx = int(re.sub(r"\D", "", f.stem))
        d = json.loads(f.read_text(encoding="utf-8"))
        rows = d.get("rows", [])
        cres = rep_by_car.get(idx, [])
        n_pending = sum(1 for _, v in cres if v.get("pending") is True)
        n_prom = len(cres) - n_pending
        oc, dt = {}, {}
        for r in rows:
            oc[r.get("orientationClass", "-")] = oc.get(r.get("orientationClass", "-"), 0) + 1
            dt[r.get("defectTaxonomy", "-")] = dt.get(r.get("defectTaxonomy", "-"), 0) + 1
        car = cars_by_idx.get(idx, {})
        name = car.get("name") or "?"
        folder = car.get("folder") or "?"
        es = evidence_state_for(idx)
        L = [f"# Acceptance card - car{idx} {name}", "",
             f"- target: {d.get('target')} | folder: `{folder}` | class: {car.get('raceClass', '-')}",
             f"- inventory: `outputs/mistral-orchestration/inventory/car{idx}.md` (+ `.json`, committed)",
             "", "## Repair state (data/car_surface_uv_repairs.json)",
             f"- {n_prom} promoted (active in every render) / {n_pending} stage-gated pending (candidates env)",
             *(f"  - `{k}` pending" for k, v in cres if v.get("pending") is True),
             "", "## Orientation / defect summary (over %d inventory rows)" % len(rows),
             *(f"- orientationClass: {k} x{v}" for k, v in sorted(oc.items(), key=lambda x: -x[1])),
             *(f"- defectTaxonomy: {k} x{v}" for k, v in sorted(dt.items(), key=lambda x: -x[1]))]
        for r in rows:
            for s in r.get("secondaryDefects", []) or []:
                L.append(f"- secondary: {s.get('defectTaxonomy')} -> {s.get('correctionFamily')}: {str(s.get('evidence'))[:150]}")
        L += ["", "## Atlas table",
              "| file | px | guid | uv | orientationClass | defect | correction |", "|---|---|---|---|---|---|---|"]
        for r in rows:
            L.append(f"| {r.get('file')} | {r.get('pixels')} | {(r.get('guid') or '')[:12]} | {r.get('uvChannel')} | "
                     f"{r.get('orientationClass')} | {r.get('defectTaxonomy')} | {r.get('correctionFamily')} |")
        L += ["", "## Evidence + packs",
              *(f"- {label}: `{path}`" for label, path in CAR_ACCEPTANCE_PACKS.get(idx, [])),
              "- fleet visual state: " + es["fleetVisualEvidence"],
              "- P5 metrics state: " + es["assemblyMetrics(metresPerUv/basisDeterminant/textOrientation)"],
              "", "## Open decisions (reviewer's)",
              "- " + (f"{n_pending} pending entries need the per-surface confirm run before promotion" if n_pending
                     else "no pending UV entries; the open items are the art/detail verdicts documented in the inventory")]
        if not any(r.get("secondaryDefects") for r in rows):
            L.append("- no secondary (documented, cited) defects recorded for this car beyond the rule-derived rows")
        L += ["", "## Verdict", "", "**ACCEPTANCE VERDICT (human reviewer):** ____________________", "",
              "_cloud-UNVALIDATED; no acceptance from in-tree evidence._"]
        cp = out_dir / f"car{idx}.md"
        cp.write_text("\n".join(L) + "\n", encoding="utf-8")
        index.append(f"- [car{idx} {name}](car{idx}.md) - rows={len(rows)}, promoted={n_prom}, pending={n_pending}"
                     + (", pack(s) linked" if idx in CAR_ACCEPTANCE_PACKS else ""))
    index += ["", "- [track0 standing-rejects index](%s) (the track-side acceptance rows)" % TRACK0_INDEX[1],
              "", "Packs referenced live under `outputs/mistral-orchestration/visual-review/` and `outputs/mistral-orchestration/f1-triad-pack/`."]
    ip = out_dir / "CARDS-INDEX.md"
    ip.write_text("\n".join(index) + "\n", encoding="utf-8")
    print(f"cards: {len(files)} cards + index -> {out_dir}")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", default=str(pathlib.Path(__file__).resolve().parent.parent))
    ap.add_argument("--manifest", action="store_true", help="enumerate the whole fleet + tracks from catalog sources (fail-closed asserts)")
    ap.add_argument("--cards", action="store_true", help="per-car acceptance cards from the committed inventories")
    ap.add_argument("--out-cards", default="outputs/mistral-orchestration/cards")
    ap.add_argument("--car"); ap.add_argument("--index", type=int)
    ap.add_argument("--track")
    ap.add_argument("--assembly", help="assembly-audit JSON to join (optional)")
    ap.add_argument("--out-md"); ap.add_argument("--out-json")
    args = ap.parse_args()
    root = pathlib.Path(args.root)
    assets = root / "assets"
    if args.cards:
        return cards_mode(root, args)
    if args.manifest:
        try:
            result = manifest(root)
        except (AssertionError, ValueError, FileNotFoundError, json.JSONDecodeError) as e:
            print(f"manifest failed: {e}"); return 3
        if args.out_json:
            out_json = pathlib.Path(args.out_json); out_json.parent.mkdir(parents=True, exist_ok=True)
            out_json.write_text(json.dumps(result, indent=1), encoding="utf-8")
        if args.out_md:
            out_md = pathlib.Path(args.out_md); out_md.parent.mkdir(parents=True, exist_ok=True)
            out_md.write_text(markdown_manifest(result), encoding="utf-8")
        print(f"manifest cars={result['counts']['cars']} tracks={result['counts']['tracks']} "
              f"routes={result['counts']['sourceRoutes']} assertsPassed={result['assertsPassed']} problems={len(result['problems'])}")
        for p in result["problems"][:20]:
            print("ASSERT: " + p)
        return 0 if result["assertsPassed"] else 3
    car_map_path = assets / "car_map.json"
    if args.car and args.index is None:
        index = next((i for i, f in car_catalog(root).items() if f == args.car), None)
        if index is None:
            print(f"unknown car folder: {args.car}"); return 2
    elif args.index is not None:
        index = args.index
        args.car = car_catalog(root).get(index)
        if args.car is None:
            print(f"unknown car index: {args.index}"); return 2
    if args.car:
        folder = assets / "RealRacing" / "Source" / args.car
        if not folder.is_dir():
            print(f"missing car folder: {folder}"); return 3
        if not car_map_path.exists():
            print(f"missing {car_map_path}"); return 3
        car_map = load_json(car_map_path)
        target = f"res://assets/RealRacing/Source/{args.car}/"
        bindings = {k: v for k, v in reverse_bindings(car_map).items() if k.startswith(target)}
    elif args.track:
        index, target, bindings = None, None, {}
        folder = assets / "RealRacing" / "Source" / args.track
        if not folder.is_dir():
            print(f"missing track folder: {folder}"); return 3
    else:
        print("choose --car/--index/--track"); return 2

    repairs_path = root / "data" / "car_surface_uv_repairs.json"
    repairs = load_json(repairs_path) if repairs_path.exists() else {}
    assembly = assembly_index(args.assembly) if args.assembly else {}
    g2p = load_guid2path_reverse(root)
    umap = unity_map_reverse(root) if args.track else {}
    densities = None
    if args.track:
        density_path = root / "outputs" / "repair-20261029" / "TRACK_UV_DENSITY_ACTUAL.json"
        if density_path.exists():
            densities = load_json(density_path)
        else:
            print(f"note: measured-density file not found at {density_path} (ground-pool linkage section will be omitted)")

    rows = []
    for png in sorted(folder.rglob("*.png")):
        rel = png.relative_to(assets).as_posix()
        res_path = "res://assets/" + rel.split("assets/", 1)[-1]
        info = png_info(png)
        imp = import_info(png.with_name(png.name + ".import"))
        bound = bindings.get(res_path, [])
        repair_rows, bindings_detail, states = [], [], set()

        def resolve(car_index, renderer_name):
            """Effective transform + repair state for one renderer binding."""
            key = f"{car_index}/{renderer_name}"
            rep = repairs.get(key)
            if rep and not rep.get("pending", False):
                scale = tuple(rep.get("scale") or DEFAULT_CAR_SCALE)
                offset = tuple(rep.get("offset") or DEFAULT_CAR_OFFSET)
                state = "applied (promoted repair overrides the default flip policy)"
            elif rep and rep.get("pending"):
                scale, offset = DEFAULT_CAR_SCALE, DEFAULT_CAR_OFFSET
                state = "pending-inert at production state (applies only under the candidates env); default flip policy in effect"
            else:
                scale, offset = DEFAULT_CAR_SCALE, DEFAULT_CAR_OFFSET
                state = "default flip policy (cars: uv_scale=(-1,-1)/offset=(1,1) for all surfaces; tracks: same for non-org_/non-crowd names)"
            if args.track and ("org_" in renderer_name or "crowd" in renderer_name.lower()):
                scale, offset = (1.0, 1.0), (0.0, 0.0)
                state = "flip-policy EXCEPTION (org_/crowd binder name): no UV transform applied"
            return scale, offset, state, key, rep

        def find_finding(car_index, renderer_name, file_name):
            table = DOCUMENTED_FINDINGS.get(car_index, {}) if car_index is not None else {}
            for needle, finding in table.items():
                if needle in renderer_name or needle in file_name:
                    return finding
            return None

        if bound:
            for b in bound:
                scale, offset, state, key, rep = resolve(b["car"], b["renderer"])
                cls = orientation_class(scale, offset)
                states.add(cls.split(" ")[0])
                finding = find_finding(b["car"], b["renderer"], rel)
                hidden = any(t in b["renderer"] for t in ("LOD_A_INT_", "_mm_int", "DAMAGE", "BLUR", "SCRATCH"))
                if b["slot"] == "_MaskMap":
                    defect, corr = "none", "none-needed"
                    ev = ("data map (_MaskMap): shares the _BaseMap's UV mapping - its orientation tracks the albedo's per-binding state; "
                          "bound but gated by _UseMask (car16 ext material _UseMask=1 = active; others 0 = inert); LINEAR data semantics - never treat as sRGB art")
                elif finding:
                    defect, corr = finding["defectTaxonomy"], finding["correctionFamily"]
                    ev = finding["evidence"]
                elif cls == "mirrored-X":
                    defect, corr = "mirrored-UV", "uv-remap-per-surface"
                    ev = ("rule-derived: mirrored-X transform on this binding; no documented per-surface finding"
                          + ("; renderer hidden by name policy (DAMAGE/INT/BLUR/SCRATCH) so cosmetic-only" if hidden else ""))
                else:
                    defect, corr = "none", "none-needed"
                    ev = "rule-derived: no transform-induced defect on this binding; no documented finding"
                bindings_detail.append({
                    "car": b["car"], "renderer": b["renderer"], "slot": b["slot"], "guid": b["guid"],
                    "uvChannel": "UV0", "uvScale": list(scale), "uvOffset": list(offset),
                    "repairState": state, "orientationClass": cls,
                    "defectTaxonomy": defect, "correctionFamily": corr, "evidence": ev,
                })
                if rep:
                    repair_rows.append({"key": key, "scale": rep.get("scale"), "offset": rep.get("offset"),
                                        "pending": rep.get("pending", False)})
        elif args.track:
            links = umap.get(res_path, [])
            for link in links[:24]:
                renderer_name = link.get("materialPath") or ""
                scale, offset, state, _, _ = resolve(None, renderer_name)
                cls = orientation_class(scale, offset)
                states.add(cls.split(" ")[0])
                finding = find_finding(None, renderer_name, rel)
                if finding:
                    defect, corr, ev = finding["defectTaxonomy"], finding["correctionFamily"], finding["evidence"]
                else:
                    defect, corr = "none", "none-needed"
                    ev = "no documented finding for this track material"
                bindings_detail.append({
                    "car": None, "renderer": renderer_name, "slot": link.get("slot"), "guid": link["guid"],
                    "uvChannel": "UV0", "uvScale": list(scale), "uvOffset": list(offset),
                    "repairState": state, "orientationClass": cls,
                    "defectTaxonomy": defect, "correctionFamily": corr, "evidence": ev,
                })

        # join measured assembly rows through the binding's renderer names
        joined = []
        for b in bound:
            m = assembly.get(b["renderer"])
            if m:
                joined.append({"renderer": b["renderer"], **m})

        # fallback-livery annotation (cars): CarLoader.FallbackTex binds the first
        # livery/*.png when the car's ext source materials carry no _BaseMap.
        fallback_note = None
        if not args.track and not bound and rel.split("/")[-2] == "livery":
            ext_bases = []
            for renderer, lists in car_map.get("cars", {}).get(str(index), {}).items():
                if "_mm_ext" not in renderer:
                    continue
                for guid in (lists[0] if lists else []):
                    texs = (car_map.get("materials", {}).get(guid, {}) or {}).get("texs") or {}
                    ext_bases.append(texs.get("_BaseMap") or texs.get("_MainTex"))
            if not any(ext_bases):
                liveries = sorted(p.name for p in (folder / "livery").glob("*.png"))
                first = liveries[0] if liveries else None
                fallback_note = (
                    "runtime FALLBACK: CarLoader.FallbackTex binds the first livery/*.png ("
                    + str(first) + ") because this car's ext materials carry no _BaseMap"
                    + (" - THIS FILE is the live fallback livery" if first == png.name
                       else " - this file is an UNUSED alternate (the fallback binds alphabetically-first)"))

        # ---- row-level six fields ----
        if bound:
            guid, guid_src = bound[0]["guid"], "car_map binding"
        elif bindings_detail:
            guid, guid_src = bindings_detail[0]["guid"], "unity_map material binding"
        else:
            guid = g2p.get(rel)
            guid_src = "guid2path.json (source-known, runtime-unbound)" if guid else "none (not in guid2path)"
        flip = ("track flip policy per the TrackMaterialBinder rule: uv_scale=(-1,-1)/offset=(1,1) for non-org_/non-crowd names; "
                "org_/crowd binders are the documented exception"
                if args.track else
                "car flip policy per CarLoader.CarShader: uv_scale=(-1,-1)/offset=(1,1) applies to ALL surfaces (no org_/crowd exception); repair entries override per surface")
        if len(states) > 1:
            row_cls = "indeterminate (mixed per surface: see bindings[]; reason: different surfaces of this texture sit in different repair states)"
        elif states:
            row_cls = next(iter(states))
        else:
            row_cls = "as-authored (file on disk; not bound by any runtime material in the local maps)"
        row_defect, row_corr = "none", "none-needed"
        for b in bindings_detail:
            if b["defectTaxonomy"] != "none":
                row_defect, row_corr = b["defectTaxonomy"], b["correctionFamily"]
                break
        # secondary documented defects for this FILE (file-level findings beyond the row's primary defect)
        secondaries = []
        file_findings = DOCUMENTED_FINDINGS.get(index, {}) if index is not None else {}
        for needle, finding in file_findings.items():
            if needle in rel and finding["defectTaxonomy"] not in ("none", row_defect) \
                    and not any(b["defectTaxonomy"] == finding["defectTaxonomy"] for b in bindings_detail):
                secondaries.append({"defectTaxonomy": finding["defectTaxonomy"],
                                    "correctionFamily": finding["correctionFamily"],
                                    "evidence": finding["evidence"]})
        rows.append({
            "file": rel, "pixels": f"{info['width']}x{info['height']}", **info,
            "import": imp, "boundTo": bound, "repairs": repair_rows, "assembly": joined,
            "guid": guid, "guidSource": guid_src,
            "uvChannel": "UV0" if (bound or bindings_detail) else "none-bound",
            "fallbackNote": fallback_note,
            "flipPolicy": flip,
            "orientationClass": row_cls,
            "defectTaxonomy": row_defect,
            "correctionFamily": row_corr,
            "secondaryDefects": secondaries,
            "bindings": bindings_detail,
            "density": [{"renderer": j["renderer"],
                         "pxPerMetre": density_note(info["width"], j["metresPerUv0"]),
                         "metresPerUv0": j["metresPerUv0"], "visible": j["visible"],
                         "basisDeterminant": j["basisDeterminant"]} for j in joined],
        })

    # ground-pool linkage (track mode): the MEASURED per-surface ground bindings that paint
    # the drivable area from the shared RecoveredNativeTextures pool - these textures live
    # outside the track source folder, so they appear here and not as file rows.
    ground_pool = None
    if args.track and densities:
        by_texture = {}
        for e in densities:
            tex = (e.get("texture") or "").split("/")[-1]
            row = by_texture.setdefault(tex, {"texture": tex, "surfaces": 0,
                                              "uv0Min": 1e9, "uv0Max": 0.0, "collapsed": 0})
            row["surfaces"] += 1
            uv0 = e.get("uv0")
            if isinstance(uv0, (int, float)):
                row["uv0Min"] = min(row["uv0Min"], uv0)
                row["uv0Max"] = max(row["uv0Max"], uv0)
            row["collapsed"] += (e.get("collapsed0") or 0)
        ground_pool = []
        for row in sorted(by_texture.values(), key=lambda r: r["texture"]):
            pool_file = root / "assets" / "RealRacing" / "RecoveredNativeTextures"
            hit = list(pool_file.rglob(row["texture"]))
            if hit:
                info = png_info(hit[0])
                row["pixels"] = f"{info['width']}x{info['height']}"
                row["pxPerMetro"] = round(info["width"] / row["uv0Min"], 1) if row["uv0Min"] else None
                row["pxPerMetroAtWorst"] = round(info["width"] / row["uv0Max"], 1) if row["uv0Max"] else None
            ground_pool.append(row)

    # material-family groups (the cockpit/interior readability grouping)
    INT_HINTS = ("_mm_cab", "_mm_int", "_mm_sw", "LOD_A_INT_", "LOD_A_STEERING")

    def is_interior_texture(row):
        base_bindings = [b for b in row["bindings"] if b.get("slot") == "_BaseMap"]
        if not base_bindings:
            return False
        return all(any(h in (b.get("renderer") or "") for h in INT_HINTS) for b in base_bindings)

    int_rows = [r for r in rows if is_interior_texture(r)]
    groups = {"int": {"label": "cockpit-readable surfaces (INT group)",
                      "rows": [r["file"] for r in int_rows],
                      "note": ("dashboard/interior textures: mirrored instrument text = the same H1 mechanism on the "
                               "interior materials; readings blocked until a cockpit-audit emits INT-surface UV data "
                               "(the assembly audits cover visible-only surfaces); low-res-source applies (512-class)"),
                      }} if int_rows else None

    result = {
        "scope": "read-only texture inventory (no mutation, no rendering); six-field schema per EVIDENCE_INDEX QC",
        "target": args.car and f"car {args.car} (index {index})" or f"track {args.track}",
        "assemblySource": args.assembly, "rows": rows, "groups": groups,
        "groundPool": ground_pool,
        "schema": {
            "orientationClass": list(ORIENTATION_CLASSES),
            "defectTaxonomy": list(DEFECT_TAXONOMY),
            "correctionFamily": list(CORRECTION_FAMILIES),
            "uvChannelBasis": "UV0 primary for car materials (no UV2 in any inspected car16/car6 material set: runs 41306343412/41510907196); tracks bind UV0 tiled with UV1 as the lightmap channel",
            "flipPolicy": "stated per row; the QC's as-authored rule requires the flip-policy exception to be stated (done per row + per binding)",
        },
        "notes": [
            "orientationClass/defect/correction for undocumented bindings are RULE-DERIVED from the effective transform and tagged 'rule-derived' in evidence; documented findings carry their run/commit citations",
            "density pxPerMetre = textureWidth / metresPerUv0, valid only when --assembly supplies metresPerUv0",
            "track rows: density join fills uv0/uv1 metres-per-tile from TRACK_UV_DENSITY_ACTUAL.json where the texture matches",
        ],
    }
    if args.out_json:
        jp = pathlib.Path(args.out_json); jp.parent.mkdir(parents=True, exist_ok=True)
        jp.write_text(json.dumps(result, indent=1), encoding="utf-8")
    if args.out_md:
        lines = [f"# Texture inventory — {result['target']}", "",
                 " Six-field schema per EVIDENCE_INDEX QC: guid+file / dimensions / uvChannel+flipPolicy / orientationClass / defectTaxonomy / correctionFamily.", "",
                 "| file | px | bytes | guid (source) | uvChannel | orientationClass | defectTaxonomy | correctionFamily | import | repairs |",
                 "|---|---|---|---|---|---|---|---|---|---|"]
        for r in rows:
            imp = r["import"]
            istr = (f"mode={imp.get('compressMode')} mip={imp.get('mipmaps')} "
                    f"limit={imp.get('sizeLimit')} vram={imp.get('vramTexture')}") if imp.get("present") else " NONE"
            gstr = (r["guid"][:12] + "…(" + r["guidSource"].split(" ")[0] + ")") if r["guid"] else "-"
            rstr = "; ".join(f"{x['key']}(pending={x['pending']})" for x in r["repairs"]) or "-"
            lines.append(f"| {r['file']} | {r['pixels']} | {r['bytes']} | {gstr} | {r['uvChannel']} | "
                         f"{r['orientationClass']} | {r['defectTaxonomy']} | {r['correctionFamily']} | {istr} | {rstr} |")
        lines += ["", "### Per-binding detail (orientation/defect per renderer)", ""]
        for r in rows:
            if not r["bindings"]:
                continue
            lines.append(f"**{r['file']}** — flipPolicy: {r['flipPolicy']}")
            lines += ["", "| renderer/car | slot | uvScale/offset | state | orientationClass | defect | correction |", "|---|---|---|---|---|---|---|"]
            for b in r["bindings"]:
                who = f"car{b['car']}:{b['renderer']}" if b["car"] is not None else (b["renderer"] or b["guid"][:12])
                lines.append(f"| {who} | {b['slot']} | {b['uvScale']}/{b['uvOffset']} | {b['repairState'][:60]} | {b['orientationClass']} | {b['defectTaxonomy']} | {b['correctionFamily']} |")
            lines.append("")
        if any(r["density"] for r in rows):
            lines += ["", "| renderer/source | visible | metresPerUv0 | px/m | det |", "|---|---|---|---|---|"]
            for r in rows:
                for d in r["density"]:
                    nm = d.get("renderer") or d.get("source")
                    lines.append(f"| {nm} | {d.get('visible', '-')} | {d.get('metresPerUv0', d.get('uv0mPerTile'))} | {d.get('pxPerMetre', '-')} | {d.get('basisDeterminant', '-')} |")
        fb_notes = [(r["file"], r["fallbackNote"]) for r in rows if r.get("fallbackNote")]
        if fb_notes:
            lines += ["", "### Fallback-livery notes", ""]
            lines += [f"- `{f}`: {n}" for f, n in fb_notes]
        if result.get("groups"):
            lines += ["", "### INT group (cockpit-readable surfaces)", ""]
            for r in rows:
                if r["file"] in result["groups"]["int"]["rows"]:
                    lines.append(f"- **{r['file']}** ({r['pixels']}, {r['guidSource'] if not str(r['guid']).startswith('none') else 'unbound'}) — "
                                 f"orientation: {r['orientationClass']}; defect: {r['defectTaxonomy']}; correction: {r['correctionFamily']}")
            lines.append("")
            lines.append(f"Note: {result['groups']['int']['note']}")
        if result.get("groundPool"):
            lines += ["", "### Ground-pool bindings (measured; the drivable-area textures come from the shared pool outside this folder)",
                      "", "| texture | px | surfaces | uv0 m/tile (best-worst) | px/m (best-worst) | collapsed uv0 tris |",
                      "|---|---|---|---|---|---|"]
            for g in result["groundPool"]:
                rng = (f"{g['uv0Min']:.1f}-{g['uv0Max']:.1f}" if g["uv0Max"] else "-")
                pxm = (f"{g.get('pxPerMetro','-')}-{g.get('pxPerMetroAtWorst','-')}" if g.get("pxPerMetro") else "-")
                lines.append(f"| {g['texture']} | {g.get('pixels','-')} | {g['surfaces']} | {rng} | {pxm} | {g['collapsed']} |")
        mp = pathlib.Path(args.out_md); mp.parent.mkdir(parents=True, exist_ok=True)
        mp.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"inventory rows={len(rows)} target={result['target']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
