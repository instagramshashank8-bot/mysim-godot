"""car20 (Bugatti Chiron) visual review pack generator (read-only).

Builds outputs/mistral-orchestration/visual-review/car20-carbody-pack/ :
- 11 promoted UV-repair renderers (data/car_surface_uv_repairs.json 20/*, pending:false)
- per row: SOURCE (authored ext_blue.png island, wrapped) + the split-atlas A/B from the
  locally decrypted chiron sets:
    tier 0 (run 41453316712): 'none' = pre-repair mapping (the documented defect),
                              'v'   = the V-only mode selected for promotion
    integrated (run 41458535677): production-path re-verification views
                                  (front/rear/side/overhead; exteriorAtlasCorrectionVerified)
  Side-view rows (doors, mirrors) have NO before-side view in the tier sets - gap stated.
Fail-closed: the row set must equal the 11 repair entries.
"""
import json, os, re, math, glob
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:/Users/shash/Documents/MySimGodot"
WT = r"C:/Users/shash/MySimGodot-wt-carbody"
REP = os.path.join(ROOT, "outputs/repair-20261029")
PACK = os.path.join(WT, "outputs/mistral-orchestration/visual-review/car20-carbody-pack")
IMGDIR = os.path.join(PACK, "images")
os.makedirs(IMGDIR, exist_ok=True)

T0 = os.path.join(REP, "cloud/chiron-atlas-baseline-visuals/review")
INT = os.path.join(REP, "cloud/chiron-integrated-visuals/review")
ALT = os.path.join(REP, r"cloud/car-material-bindings/game-runtime-0-attempt-1/outputs/cloud-game/geometry.json")

VIEW_OF_ROW = {
    "LOD_A_BODY_mm_ext": [("front", "none-front", "v-front", "front"), ("rear", "none-rear", "v-rear", "rear")],
    "LOD_A_FRONTBUMPER_mm_ext": [("front", "none-front", "v-front", "front")],
    "LOD_A_HOOD_mm_ext": [("front", "none-front", "v-front", "overhead"), ("rear", "none-rear", "v-rear", "overhead")],
    "LOD_A_DOOR_LEFT_mm_ext": [("side", None, None, "side")],
    "LOD_A_DOOR_RIGHT_mm_ext": [("side", None, None, "side")],
    "LOD_A_MIRROR_LEFT_mm_ext": [("side", None, None, "side")],
    "LOD_A_MIRROR_RIGHT_mm_ext": [("side", None, None, "side")],
    "LOD_A_BOOT_mm_ext": [("rear", "none-rear", "v-rear", "rear")],
    "LOD_A_REARBUMPER_mm_ext": [("rear", "none-rear", "v-rear", "rear")],
    "LOD_A_EXHAUST_mm_ext": [("rear", "none-rear", "v-rear", "rear")],
    "LOD_A_WING_mm_ext": [("rear", "none-rear", "v-rear", "rear")],
}

try:
    FONTB = ImageFont.truetype("arialbd.ttf", 15)
    FONTS = ImageFont.truetype("arial.ttf", 13)
except Exception:
    FONTB = FONTS = ImageFont.load_default()

metrics = {"runs": {"tier0": "41453316712 (chiron-atlas-baseline-visuals: 4 modes x front/rear)",
                    "integrated": "41458535677 (production-path re-verification; exteriorAtlasCorrectionVerified=true; wholeCarAccepted=false)"},
           "rows": []}

# ---- rows from the merged bindings artifact (car 20, _mm_ext, _BaseMap) + fail-closed match ----
g = json.load(open(ALT, encoding="utf-8"))
c20 = next((c for c in g if c.get("car") == 20), None)
assert c20 is not None, "car20 missing from the car-material-bindings artifact"
bounds = {}
for vm in c20.get("visibleMaterials", []):
    nm = str(vm.get("renderer", ""))
    if "_mm_ext" not in nm or vm.get("slot", "_BaseMap") != "_BaseMap":
        continue
    def nums(val):
        n = [float(x) for x in re.findall(r"-?\d+\.?\d*(?:e-?\d+)?", str(val))]
        return (n[0], n[1]) if len(n) >= 2 else None
    b = bounds.setdefault(nm, {"uvMin": None, "uvMax": None, "uvCount": 0, "tex": str(vm.get("texture") or "")})
    b["uvCount"] += int(vm.get("uvCount", 0) or 0)
    for key, tag in (("uvMinimum", "uvMin"), ("uvMaximum", "uvMax")):
        pr = nums(vm.get(key, ""))
        if pr is None:
            continue
        if b[tag] is None:
            b[tag] = list(pr)
        elif tag == "uvMin":
            b[tag] = [min(b[tag][0], pr[0]), min(b[tag][1], pr[1])]
        else:
            b[tag] = [max(b[tag][0], pr[0]), max(b[tag][1], pr[1])]

rep_data = json.load(open(os.path.join(ROOT, "data/car_surface_uv_repairs.json"), encoding="utf-8"))
staged20 = []
def walk(o):
    if isinstance(o, dict):
        for k, v in o.items():
            if isinstance(k, str) and re.fullmatch(r"20/[A-Za-z0-9_]+", k):
                staged20.append(k.split("/", 1)[1])
            else:
                walk(v)
    elif isinstance(o, list):
        for v in o:
            walk(v)
walk(rep_data)
assert len(staged20) == 11 and set(staged20) == set(bounds.keys()), \
    "row-set mismatch: staged20=%d bounds=%d" % (len(staged20), len(bounds))

atlas_rel = bounds["LOD_A_BODY_mm_ext"]["tex"]
atlas_rel = atlas_rel[len("res://"):] if atlas_rel.startswith("res://") else atlas_rel
ATLAS = os.path.join(ROOT, atlas_rel.replace("/", os.sep))
assert os.path.isfile(ATLAS), "chiron ext atlas missing: " + ATLAS
metrics["atlas"] = {"file": atlas_rel, "pixels": Image.open(ATLAS).size}

def atlas_crop(path, u0, v0, u1, v1, max_side=430):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    a = np.asarray(im, dtype=np.uint8)
    xs = np.arange(int(math.floor(u0 * W)), int(math.ceil(u1 * W))) % W
    ys = np.arange(int(math.floor(v0 * H)), int(math.ceil(v1 * H))) % H
    img = Image.fromarray(a[np.ix_(ys, xs)])
    w, h = img.size
    s = max_side / max(w, h)
    if s != 1.0:
        img = img.resize((max(1, int(w * s)), max(1, int(h * s))), Image.NEAREST)
    return img

def fit_file(path, width=560):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    s = width / w
    return im.resize((width, int(h * s)), Image.LANCZOS)

def caption(im, text):
    c = Image.new("RGB", (im.size[0], 22), (30, 30, 30))
    ImageDraw.Draw(c).text((4, 3), text, fill=(255, 255, 255), font=FONTS)
    out = Image.new("RGB", (im.size[0], 22 + im.size[1]), (10, 10, 10))
    out.paste(c, (0, 0))
    out.paste(im, (0, 22))
    return out

def hcat(imgs, pad=8, bg=(24, 24, 24)):
    w = sum(i.size[0] for i in imgs) + pad * (len(imgs) + 1)
    h = max(i.size[1] for i in imgs) + pad * 2
    out = Image.new("RGB", (w, h), bg)
    x = pad
    for i in imgs:
        out.paste(i, (x, pad))
        x += i.size[0] + pad
    return out

def png_in(base, name):
    p = os.path.join(base, name)
    return p if os.path.isfile(p) else None

for idx, nm in enumerate(sorted(bounds.keys()), 1):
    b = bounds[nm]
    row = {"renderer": nm, "atlas": os.path.basename(ATLAS),
           "uvBounds": {"min": b["uvMin"], "max": b["uvMax"]}, "uvCount": b["uvCount"], "strips": []}
    safe = nm.replace("LOD_A_", "").replace("_mm_ext", "-ext")
    for si, spec in enumerate(VIEW_OF_ROW[nm]):
        view, before, after, iview = spec
        title = "car20 %s   %s   atlas=%s" % (nm, view.upper(), os.path.basename(ATLAS))
        panels = [caption(atlas_crop(ATLAS, b["uvMin"][0], b["uvMin"][1], b["uvMax"][0], b["uvMax"][1]),
                          "SOURCE (authored)  u[%.3f..%.3f] v[%.3f..%.3f] (wrapped)" % (b["uvMin"][0], b["uvMax"][0], b["uvMin"][1], b["uvMax"][1]))]
        ok_before = ok_after = False
        if before:
            pb = png_in(T0, "chiron-atlas-transpose-0-%s.png" % before)
            pa = png_in(T0, "chiron-atlas-transpose-0-%s.png" % after)
            if pb:
                panels.append(caption(fit_file(pb), "BEFORE  run 41453316712  mode=%s" % before))
                ok_before = True
            if pa:
                panels.append(caption(fit_file(pa), "AFTER (tier-0)  run 41453316712  mode=%s" % after))
                ok_after = True
        pi = png_in(INT, "chiron-reconstructed-%s.png" % iview)
        ok_int = False
        if pi:
            panels.append(caption(fit_file(pi), "AFTER (production path)  run 41458535677  %s view" % iview))
            ok_int = True
        gaps = []
        if before and not ok_before:
            gaps.append("before view missing locally")
        if view == "side":
            gaps.append("no before-side capture exists in the tier sets (front/rear only) - the side A/B gap is real")
        strip = hcat(panels)
        fname = "row-%02d%s-%s.png" % (idx, "abcdef"[si], safe)
        wid = max(p.size[0] for p in [strip]) + 24
        canvas = Image.new("RGB", (strip.size[0] + 24, strip.size[1] + 66), (16, 16, 16))
        dd = ImageDraw.Draw(canvas)
        dd.text((8, 2), title, fill=(255, 255, 255), font=FONTB)
        if gaps:
            dd.text((8, strip.size[1] + 38), "GAP: " + "; ".join(gaps), fill=(230, 190, 110), font=FONTS)
        canvas.paste(strip, (12, 26))
        canvas.save(os.path.join(IMGDIR, fname))
        row["strips"].append({"view": view, "tier0Before": ok_before, "tier0After": ok_after,
                              "integratedAfter": ok_int, "gaps": gaps, "image": "images/" + fname})
    metrics["rows"].append(row)

json.dump(metrics, open(os.path.join(PACK, "metrics.json"), "w", encoding="utf-8"), indent=1)
print("PACK", PACK)
print("rows:", len(metrics["rows"]),
      "strips:", sum(len(r["strips"]) for r in metrics["rows"]),
      "gaps:", sum(len(s["gaps"]) for r in metrics["rows"] for s in r["strips"]))
