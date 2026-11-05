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
        "counts": {"cars": len(cars), "tracks": len(tracks), "sourceRoutes": len(routes)},
        "assertsPassed": not problems, "problems": problems, "notes": notes,
        "cars": car_rows, "tracks": track_rows,
    }
    return result


def markdown_manifest(result):
    lines = ["# Fleet manifest tables (tool output; facts only)", "",
             f"cars={result['counts']['cars']} tracks={result['counts']['tracks']} sourceRoutes={result['counts']['sourceRoutes']} assertsPassed={result['assertsPassed']}", "",
             "## Cars", "", "| id | name | folder | class | fbx | textures | liveries | bound livery | repairs promoted/pending | special paths |",
             "|---|---|---|---|---|---|---|---|---|---|"]
    for c in result["cars"]:
        lines.append(f"| {c['id']} | {c['name']} | {c['folder']} | {c['raceClass']} | {c['fbx']} | {c['textures']} | "
                     f"{len(c['liveriesOnDisk'])} | {c['boundLivery'] or '-'} | "
                     f"{len(c['uvRepairsPromoted'])}/{len(c['uvRepairsPending'])} | {'; '.join(c['specialPaths']) or '-'} |")
    lines += ["", "## Tracks", "", "| id | name | map/layout | scene present | route name | variations ok |", "|---|---|---|---|---|---|"]
    for t in result["tracks"]:
        ok = all(v["present"] for v in t["variationPaths"])
        lines.append(f"| {t['id']} | {t['name']} | {t['map']}/{t['layout']} | {t['sceneBytes'] is not None} | {t['routeName'] or '-'} | {ok} |")
    if result["problems"]:
        lines += ["", "## ASSERT FAILURES", ""]
        lines += [f"- {p}" for p in result["problems"]]
    return "\n".join(lines) + "\n"


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", default=str(pathlib.Path(__file__).resolve().parent.parent))
    ap.add_argument("--manifest", action="store_true", help="enumerate the whole fleet + tracks from catalog sources (fail-closed asserts)")
    ap.add_argument("--car"); ap.add_argument("--index", type=int)
    ap.add_argument("--track")
    ap.add_argument("--assembly", help="assembly-audit JSON to join (optional)")
    ap.add_argument("--out-md"); ap.add_argument("--out-json")
    args = ap.parse_args()
    root = pathlib.Path(args.root)
    assets = root / "assets"
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

    rows = []
    for png in sorted(folder.rglob("*.png")):
        rel = png.relative_to(assets).as_posix()
        info = png_info(png)
        imp = import_info(png.with_name(png.name + ".import"))
        bound = bindings.get("res://assets/" + rel, [])
        repair_rows = []
        for b in bound:
            key = f"{b['car']}/{b['renderer']}"
            if key in repairs:
                r = repairs[key]
                repair_rows.append({"key": key, "scale": r.get("scale"), "offset": r.get("offset"),
                                    "pending": r.get("pending", False)})
        # join measured assembly rows through the binding's renderer names
        joined = []
        for b in bound:
            m = assembly.get(b["renderer"])
            if m:
                joined.append({"renderer": b["renderer"], **m})
        rows.append({
            "file": rel, "pixels": f"{info['width']}x{info['height']}", **info,
            "import": imp, "boundTo": bound, "repairs": repair_rows, "assembly": joined,
            "density": [{"renderer": j["renderer"],
                         "pxPerMetre": density_note(info["width"], j["metresPerUv0"]),
                         "metresPerUv0": j["metresPerUv0"], "visible": j["visible"],
                         "basisDeterminant": j["basisDeterminant"]} for j in joined],
        })

    result = {
        "scope": "read-only texture inventory (no mutation, no rendering)",
        "target": args.car and f"car {args.car} (index {index})" or f"track {args.track}",
        "assemblySource": args.assembly, "rows": rows,
        "notes": [
            "orientation classes and defect columns live in the turn report; this tool emits measured facts only",
            "density pxPerMetre = textureWidth / metresPerUv0, valid only when --assembly supplies metresPerUv0",
        ],
    }
    if args.out_json:
        jp = pathlib.Path(args.out_json); jp.parent.mkdir(parents=True, exist_ok=True)
        jp.write_text(json.dumps(result, indent=1), encoding="utf-8")
    if args.out_md:
        lines = [f"# Texture inventory — {result['target']}", "",
                 "| file | px | bytes | RGBA | icc | import | bound to | repairs |", "|---|---|---|---|---|---|---|---|"]
        for r in rows:
            imp = r["import"]
            istr = (f"mode={imp.get('compressMode')} mip={imp.get('mipmaps')} "
                    f"limit={imp.get('sizeLimit')} vram={imp.get('vramTexture')}") if imp.get("present") else " NONE"
            bstr = "; ".join(f"car{b['car']}:{b['renderer']}[{b['slot']}]" for b in r["boundTo"]) or "-"
            rstr = "; ".join(f"{k}(scale={s},pending={p})" for k, s, p in
                             [(x["key"], x["scale"], x["pending"]) for x in r["repairs"]]) or "-"
            lines.append(f"| {r['file']} | {r['pixels']} | {r['bytes']} | {r['hasAlpha']} | "
                         f"{r['iccOrGamma'] or '-'} | {istr} | {bstr} | {rstr} |")
        if any(r["density"] for r in rows):
            lines += ["", "| renderer | visible | metresPerUv0 | px/m | det |", "|---|---|---|---|---|"]
            for r in rows:
                for d in r["density"]:
                    lines.append(f"| {d['renderer']} | {d['visible']} | {d['metresPerUv0']} | {d['pxPerMetre']} | {d['basisDeterminant']} |")
        mp = pathlib.Path(args.out_md); mp.parent.mkdir(parents=True, exist_ok=True)
        mp.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"inventory rows={len(rows)} target={result['target']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
