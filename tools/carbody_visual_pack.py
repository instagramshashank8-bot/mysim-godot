"""car16 mirrored-UV visual review pack generator (read-only).

Builds outputs/mistral-orchestration/visual-review/car16-mirrored-uv-pack/ :
- per-row composite images: source atlas crop (authored orientation) + in-game
  BEFORE (production, leg A = 41870086370) vs AFTER (candidates, leg B = 41874911549)
  crops at the legA->legB diff clusters.
- per-view diff sheet + metrics.json + PACK.md index.
No game run, no dispatch: purely local composition of existing evidence.
"""
import json, os, re, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:/Users/shash/Documents/MySimGodot"
WT = r"C:/Users/shash/MySimGodot-wt-carbody"
REP = os.path.join(ROOT, "outputs/repair-20261029")
LEG_A = os.path.join(REP, "p1legA-41870086370/decrypted/review")
LEG_B = os.path.join(REP, "p1legB-41874911549/decrypted/review")
PACK = os.path.join(WT, "outputs/mistral-orchestration/visual-review/car16-mirrored-uv-pack")
IMGDIR = os.path.join(PACK, "images")
os.makedirs(IMGDIR, exist_ok=True)

ATLASES = {
    "ext": os.path.join(ROOT, "assets/RealRacing/Source/2019_f1_ferrari_sf90/livery/2019_f1_ferrari_sf90_ext_16.png"),
    "badges": os.path.join(ROOT, "assets/RealRacing/Source/2019_f1_ferrari_sf90/2019_f1_ferrari_sf90_badges.png"),
}

ROWS = [
    ("LOD_A_DOOR_LEFT_mm_ext", "ext", ["left", "front"]),
    ("LOD_A_DOOR_RIGHT_mm_ext", "ext", ["right", "front"]),
    ("LOD_A_MIRROR_LEFT_mm_ext", "ext", ["left", "rear"]),
    ("LOD_A_MIRROR_RIGHT_mm_ext", "ext", ["right", "rear"]),
    ("LOD_A_STRUT_UPPER_FRONT_LEFT_mm_ext", "ext", ["left", "front"]),
    ("LOD_A_STRUT_UPPER_FRONT_RIGHT_mm_ext", "ext", ["right", "front"]),
    ("LOD_A_EXHAUST_mm_ext", "ext", ["rear", "top"]),
    ("LOD_A_BODY_mm_badges", "badges", ["front", "top"]),
    ("LOD_A_FRONTBUMPER_mm_badges", "badges", ["front"]),
    ("LOD_A_REARBUMPER_mm_badges", "badges", ["rear", "top"]),
]
VIEWS = ["front", "left", "right", "top", "rear", "cockpit"]

try:
    FONT = ImageFont.truetype("arial.ttf", 15)
    FONTB = ImageFont.truetype("arialbd.ttf", 15)
    FONTS = ImageFont.truetype("arial.ttf", 13)
except Exception:
    FONT = ImageFont.load_default()
    FONTB = FONT
    FONTS = FONT

metrics = {"legs": {"legA": "41870086370 @26d0191 production", "legB": "41874911549 @a313492 candidates active"},
           "viewDiff": {}, "rows": []}

# ---------- 1) per-renderer UV0 bounds from the local assembly evidence ----------
asm = json.load(open(os.path.join(REP, "cloud/formula16-density/outputs/cloud-game/car16-channel-0-both-assembly.json"), encoding="utf-8"))
uvinfo = {}
for vm in asm.get("visibleMaterials", []):
    nm = vm.get("renderer", "")
    d = uvinfo.setdefault(nm, {"uvMin": None, "uvMax": None, "uvCount": 0, "texture": vm.get("texture", "")})
    d["uvCount"] += int(vm.get("uvCount", 0) or 0)

    def nums_of(val):
        n = [float(x) for x in re.findall(r"-?\d+\.?\d*(?:e-?\d+)?", str(val))]
        return (n[0], n[1]) if len(n) >= 2 else None

    def absorb(pair, which):
        if pair is None:
            return
        if d[which] is None:
            d[which] = list(pair)
        elif which == "uvMin":
            d[which] = [min(d[which][0], pair[0]), min(d[which][1], pair[1])]
        else:
            d[which] = [max(d[which][0], pair[0]), max(d[which][1], pair[1])]

    absorb(nums_of(vm.get("uvMinimum")), "uvMin")
    absorb(nums_of(vm.get("uvMaximum")), "uvMax")
    uv0 = vm.get("uv0")
    if isinstance(uv0, dict):
        for key, val in uv0.items():
            kl = str(key).lower()
            if "min" in kl:
                absorb(nums_of(val), "uvMin")
            elif "max" in kl:
                absorb(nums_of(val), "uvMax")

# ---------- 2) staged pending entries (verbatim bodies) ----------
rep = json.load(open(os.path.join(ROOT, "data/car_surface_uv_repairs.json"), encoding="utf-8"))
staged = {}
def walk(o):
    if isinstance(o, dict):
        for k, v in o.items():
            if isinstance(k, str) and re.fullmatch(r"16/[A-Za-z0-9_]+", k):
                staged[k.split("/", 1)[1]] = v
            else:
                walk(v)
    elif isinstance(o, list):
        for v in o:
            walk(v)
walk(rep)

# ---------- 3) atlas crops (wrapped sampling) ----------
def atlas_crop(path, u0, v0, u1, v1, max_side=560):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    px = im.load()
    a = np.asarray(im, dtype=np.uint8)
    x0, x1 = int(math.floor(u0 * W)), int(math.ceil(u1 * W))
    y0, y1 = int(math.floor(v0 * H)), int(math.ceil(v1 * H))
    xs = (np.arange(x0, x1) % W)
    ys = (np.arange(y0, y1) % H)
    if len(ys) == 0 or len(xs) == 0:
        return None, (0, 0)
    patch = a[np.ix_(ys, xs)]
    return Image.fromarray(patch), (W, H)

def fit(img, max_side, upscale=True):
    w, h = img.size
    s = max_side / max(w, h)
    if not upscale and s > 1:
        s = 1.0
    if s == 1.0:
        return img
    return img.resize((max(1, int(w * s)), max(1, int(h * s))), Image.NEAREST)

# ---------- 4) legA/legB diff clusters per view ----------
def load_view(legdir, view):
    return np.asarray(Image.open(os.path.join(legdir, "ref-car16-%s.png" % view)).convert("RGB"), dtype=np.int16)

def diff_clusters(view, thresh=16, min_area=20, scale=4):
    A, B = load_view(LEG_A, view), load_view(LEG_B, view)
    if A.shape != B.shape:
        return None, "SIZE MISMATCH"
    d = np.abs(A - B).max(axis=2)
    changed = int((d > thresh).sum())
    h, w = d.shape
    small = d[::scale, ::scale] > thresh
    lbl = np.zeros(small.shape, dtype=np.int32)
    cur = 0
    boxes = []
    from collections import deque
    for i in range(small.shape[0]):
        for j in range(small.shape[1]):
            if small[i, j] and lbl[i, j] == 0:
                cur += 1
                q = deque([(i, j)])
                lbl[i, j] = cur
                n = 0
                bi0, bi1, bj0, bj1 = i, i, j, j
                while q:
                    y, x = q.popleft()
                    n += 1
                    bi0, bi1 = min(bi0, y), max(bi1, y)
                    bj0, bj1 = min(bj0, x), max(bj1, x)
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            yy, xx = y + dy, x + dx
                            if 0 <= yy < small.shape[0] and 0 <= xx < small.shape[1] and small[yy, xx] and lbl[yy, xx] == 0:
                                lbl[yy, xx] = cur
                                q.append((yy, xx))
                if n * scale * scale >= min_area:
                    pad = 8
                    x0 = max(0, bj0 * scale - pad)
                    y0 = max(0, bi0 * scale - pad)
                    x1 = min(w, (bj1 + 1) * scale + pad)
                    y1 = min(h, (bi1 + 1) * scale + pad)
                    boxes.append({"bbox": [int(x0), int(y0), int(x1), int(y1)],
                                  "area": int(n * scale * scale)})
    boxes.sort(key=lambda b: -b["area"])
    return {"changedPx": changed, "totalPx": int(h * w), "clusters": boxes[:6]}, None

# build view crops + sheets
view_data = {}
for v in VIEWS:
    res, err = diff_clusters(v)
    view_data[v] = res or {"changedPx": 0, "totalPx": 0, "clusters": [], "err": err}
    metrics["viewDiff"][v] = view_data[v]

# ---------- 5) per-row composites ----------
def caption(im, text):
    bar = Image.new("RGB", (im.size[0], 22), (30, 30, 30))
    dr = ImageDraw.Draw(bar)
    dr.text((4, 3), text, fill=(255, 255, 255), font=FONTS)
    out = Image.new("RGB", (im.size[0], 22 + im.size[1]), (10, 10, 10))
    out.paste(bar, (0, 0))
    out.paste(im, (0, 22))
    return out

def hcat(imgs, pad=6, bg=(24, 24, 24)):
    w = sum(i.size[0] for i in imgs) + pad * (len(imgs) + 1)
    h = max(i.size[1] for i in imgs) + pad * 2
    out = Image.new("RGB", (w, h), bg)
    x = pad
    for i in imgs:
        out.paste(i, (x, pad))
        x += i.size[0] + pad
    return out

for idx, (renderer, atlasKey, prefViews) in enumerate(ROWS, 1):
    r = {"renderer": renderer, "atlas": os.path.basename(ATLASES[atlasKey]), "staged": staged.get(renderer, "NOT-STAGED")}
    ui = uvinfo.get(renderer, {})
    r["uvCount"] = ui.get("uvCount")
    r["uvBounds"] = {"min": ui.get("uvMin"), "max": ui.get("uvMax")}
    safe = renderer.replace("LOD_A_", "").replace("_mm_", "-")
    fname = "row-%02d-%s.png" % (idx, safe)

    panels = []
    # source panel
    umin, umax = ui.get("uvMin"), ui.get("uvMax")
    if umin is not None and umax is not None:
        crop, dims = atlas_crop(ATLASES[atlasKey], umin[0], umin[1], umax[0], umax[1])
        if crop is not None:
            crop = fit(crop, 430)
            rect = "UV0 bounds u[%.3f..%.3f] v[%.3f..%.3f] (wrapped)  atlas %s" % (umin[0], umax[0], umin[1], umax[1], dims)
            panels.append((caption(crop, "SOURCE (authored)  " + rect), "src"))
    # in-game before/after panels
    evid = None
    for v in prefViews:
        cl = view_data.get(v, {}).get("clusters", [])
        if cl:
            evid = (v, cl[0])
            break
    if evid:
        v, clu = evid
        x0, y0, x1, y1 = clu["bbox"]
        A = Image.open(os.path.join(LEG_A, "ref-car16-%s.png" % v)).convert("RGB").crop((x0, y0, x1, y1))
        B = Image.open(os.path.join(LEG_B, "ref-car16-%s.png" % v)).convert("RGB").crop((x0, y0, x1, y1))
        cw, ch = x1 - x0, y1 - y0
        up = 640 // max(cw, 1)
        up = min(max(up, 1), 4)
        A = A.resize((cw * up, ch * up), Image.NEAREST)
        B = B.resize((cw * up, ch * up), Image.NEAREST)
        strip = hcat([caption(A, "IN-GAME BEFORE  legA 41870086370"),
                      caption(B, "IN-GAME AFTER  legB 41874911549")])
        panels.append((strip, v))
        r["evidence"] = {"view": v, "clusterBBox": clu["bbox"], "clusterAreaPx": clu["area"],
                         "viewChangedPx": view_data[v]["changedPx"]}
        r["evidenceNote"] = "largest of %d legA->legB diff clusters in the %s view" % (
            len(view_data[v]["clusters"]), v)
    else:
        r["evidence"] = None
        r["evidenceNote"] = "no legA->legB change at any expected review camera (%s)" % ",".join(prefViews)
        nb = Image.new("RGB", (640, 120), (18, 18, 18))
        dd = ImageDraw.Draw(nb)
        dd.text((10, 40), "NO VISIBLE legA->legB CHANGE at " + ",".join(prefViews), fill=(220, 200, 120), font=FONTB)
        panels.append((nb, "nochange"))

    # compose vertical
    wid = max(p[0].size[0] for p in panels) + 24
    hei = sum(p[0].size[1] for p in panels) + 30 + 24 * len(panels)
    canvas = Image.new("RGB", (wid, hei), (16, 16, 16))
    dd = ImageDraw.Draw(canvas)
    y = 22
    dd.text((6, 2), "car16 %s   atlas=%s" % (renderer, r["atlas"]), fill=(255, 255, 255), font=FONTB)
    for panel, tag in panels:
        canvas.paste(panel, (12, y))
        y += panel.size[1] + 22
    canvas.save(os.path.join(IMGDIR, fname))
    r["image"] = "images/" + fname
    metrics["rows"].append(r)

# ---------- 6) per-view diff sheets ----------
for v in VIEWS:
    cl = view_data[v]["clusters"]
    if not cl:
        continue
    A = Image.open(os.path.join(LEG_A, "ref-car16-%s.png" % v)).convert("RGB")
    B = Image.open(os.path.join(LEG_B, "ref-car16-%s.png" % v)).convert("RGB")
    parts = [caption(fit(A.crop(tuple(c["bbox"])), 400), "BEFORE %s #%d" % (v, i + 1)) for i, c in enumerate(cl)]
    parts += [caption(fit(B.crop(tuple(c["bbox"])), 400), "AFTER %s #%d" % (v, i + 1)) for i, c in enumerate(cl)]
    row1 = hcat(parts[:3]) if len(parts) > 3 else hcat(parts)
    row2 = hcat(parts[3:6]) if len(parts) > 3 else None
    rows_ = [row1] + ([row2] if row2 else [])
    W = max(r_.size[0] for r_ in rows_)
    H = sum(r_.size[1] for r_ in rows_)
    sheet = Image.new("RGB", (W, H), (16, 16, 16))
    y = 0
    for r_ in rows_:
        sheet.paste(r_, (0, y))
        y += r_.size[1]
    sheet.save(os.path.join(IMGDIR, "view-%s-diff.png" % v))

metrics["pack"] = {"atlasExt": os.path.basename(ATLASES["ext"]), "atlasBadges": os.path.basename(ATLASES["badges"]),
                   "uvSource": "outputs/repair-20261029/cloud/formula16-density/outputs/cloud-game/car16-channel-0-both-assembly.json (read-only local evidence, run 41510907196-era import metadata)"}
json.dump(metrics, open(os.path.join(PACK, "metrics.json"), "w", encoding="utf-8"), indent=1)
print("PACK", PACK)
print("views:", {v: view_data[v]["changedPx"] for v in VIEWS})
print("rows ok:", sum(1 for r in metrics["rows"] if r["evidence"]), "/", len(metrics["rows"]))
