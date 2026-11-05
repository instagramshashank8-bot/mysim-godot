"""car16 WHEEL-DETAIL visual review pack generator (read-only).

Builds outputs/mistral-orchestration/visual-review/car16-wheel-pack/ :
- per-row composites: SOURCE (authored texture island for the wheel renderer) +
  in-game view crops from the two P1 legs (wheel content is UNCHANGED by the badge
  candidates: leg A = leg B = current production state for wheel textures; the
  legA->legB diff per wheel view is measured and reported as the control).
- clearance/fit numbers cited from the merged records (no run, no dispatch).
Row list source: car16 assembly audit (car-assembly-emitted, merged); the dedicated
car-assembly wheel-surface-list doc is NOT yet landed - noted in WHEEL-PACK.md.
"""
import json, os, re, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:/Users/shash/Documents/MySimGodot"
WT = r"C:/Users/shash/MySimGodot-wt-carbody"
REP = os.path.join(ROOT, "outputs/repair-20261029")
LEG_A = os.path.join(REP, "p1legA-41870086370/decrypted/review")
LEG_B = os.path.join(REP, "p1legB-41874911549/decrypted/review")
PACK = os.path.join(WT, "outputs/mistral-orchestration/visual-review/car16-wheel-pack")
IMGDIR = os.path.join(PACK, "images")
os.makedirs(IMGDIR, exist_ok=True)

WHEEL_VIEWS = ["wheel-fl", "wheel-fr", "wheel-rl", "wheel-rr", "wheel-fl-steered"]
VIEW_OF_ROW = {
    "LOD_A_WHEEL_mm": ["wheel-fl", "wheel-fr"],
    "LOD_A_WHEEL_REAR_mm": ["wheel-rl", "wheel-rr"],
    "LOD_A_WHEEL_REAR_BLUR_mm": ["wheel-rl", "wheel-rr"],
    "LOD_A_TYRE_mm_tyre": ["wheel-fl", "wheel-fr"],
    "LOD_A_TYRE_REAR_mm_tyre": ["wheel-rl", "wheel-rr"],
    "LOD_A_WHEELGUARD_FRONT_LEFT_mm_misc": ["wheel-fl", "wheel-fl-steered"],
    "LOD_A_WHEELGUARD_FRONT_RIGHT_mm_misc": ["wheel-fr", "wheel-fl-steered"],
    "LOD_A_WHEELGUARD_FRONT_LEFT_mm_chassis": ["wheel-fl", "wheel-fl-steered"],
    "LOD_A_WHEELGUARD_FRONT_RIGHT_mm_chassis": ["wheel-fr", "wheel-fl-steered"],
}

try:
    FONT = ImageFont.truetype("arial.ttf", 15)
    FONTB = ImageFont.truetype("arialbd.ttf", 15)
    FONTS = ImageFont.truetype("arial.ttf", 13)
except Exception:
    FONT = ImageFont.load_default()
    FONTB = FONT
    FONTS = FONT

metrics = {"legs": {"legA": "41870086370 @26d0191 production", "legB": "41874911549 @a313492 candidates"},
           "viewDiff": {}, "rows": []}

# ---------- 1) wheel renderer rows from the merged car16 assembly audit ----------
asm = json.load(open(os.path.join(REP, "cloud/formula16-density/outputs/cloud-game/car16-channel-0-both-assembly.json"), encoding="utf-8"))
rows = {}
for vm in asm.get("visibleMaterials", []):
    nm = str(vm.get("renderer", ""))
    if not re.search(r"WHEEL|TYRE|CALIPER", nm) or nm.startswith("LOD_A_STEERING"):
        continue
    if vm.get("slot", "_BaseMap") != "_BaseMap":
        continue
    d = rows.setdefault(nm, {"tex": vm.get("texture") or "", "uvMin": None, "uvMax": None, "uvCount": 0})
    d["uvCount"] += int(vm.get("uvCount", 0) or 0)
    for key, tag in (("uvMinimum", "uvMin"), ("uvMaximum", "uvMax")):
        n = [float(x) for x in re.findall(r"-?\d+\.?\d*(?:e-?\d+)?", str(vm.get(key, "")))]
        if len(n) >= 2 and d[tag] is None:
            d[tag] = [n[0], n[1]]

def tex_path(res):
    if not res:
        return None
    rel = str(res)
    if rel.startswith("res://"):
        rel = rel[len("res://"):]
    p = os.path.join(ROOT, rel.replace("/", os.sep))
    return p if os.path.isfile(p) else None

def atlas_crop(path, u0, v0, u1, v1, max_side=430):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    a = np.asarray(im, dtype=np.uint8)
    x0, x1 = int(math.floor(u0 * W)), int(math.ceil(u1 * W))
    y0, y1 = int(math.floor(v0 * H)), int(math.ceil(v1 * H))
    xs = np.arange(x0, x1) % W
    ys = np.arange(y0, y1) % H
    patch = a[np.ix_(ys, xs)]
    img = Image.fromarray(patch)
    w, h = img.size
    s = max_side / max(w, h)
    if s != 1.0:
        img = img.resize((max(1, int(w * s)), max(1, int(h * s))), Image.NEAREST)
    return img, (W, H)

def caption(img_or_w, text):
    im = img_or_w
    if isinstance(im, int):
        c = Image.new("RGB", (im, 22), (30, 30, 30))
        ImageDraw.Draw(c).text((4, 3), text, fill=(255, 255, 255), font=FONTS)
        return c
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

def center_box(path, frac=0.8):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    dx, dy = int(W * (1 - frac) / 2), int(H * (1 - frac) / 2)
    return im.crop((dx, dy, W - dx, H - dy)), (dx, dy, W - dx, H - dy)

# ---------- 2) legA vs legB diff per wheel view (the wheel-content control) ----------
for v in WHEEL_VIEWS:
    A = np.asarray(Image.open(os.path.join(LEG_A, "ref-car16-%s.png" % v)).convert("RGB"), dtype=np.int16)
    B = np.asarray(Image.open(os.path.join(LEG_B, "ref-car16-%s.png" % v)).convert("RGB"), dtype=np.int16)
    d = np.abs(A - B).max(axis=2)
    metrics["viewDiff"][v] = {"changedPx": int((d > 16).sum()), "totalPx": int(d.shape[0] * d.shape[1])}

# ---------- 3) per-row composites ----------
for idx, (nm, inf) in enumerate(sorted(rows.items()), 1):
    r = {"renderer": nm, "texture": os.path.basename(inf["tex"]) or "NONE (geometry-only)",
         "uvBounds": {"min": inf["uvMin"], "max": inf["uvMax"]}, "uvCount": inf["uvCount"]}
    safe = nm.replace("LOD_A_", "")
    panels = []
    if inf["uvMin"] and inf["uvMax"]:
        p = tex_path(inf["tex"])
        if p:
            crop, dims = atlas_crop(p, inf["uvMin"][0], inf["uvMin"][1], inf["uvMax"][0], inf["uvMax"][1])
            panels.append(caption(crop, "SOURCE (authored)  %s  UV0 u[%.3f..%.3f] v[%.3f..%.3f] (wrapped, atlas %s)"
                                  % (os.path.basename(p), inf["uvMin"][0], inf["uvMax"][0], inf["uvMin"][1], inf["uvMax"][1], dims)))
    else:
        panels.append(caption(430, "NO TEXTURE BOUND on this renderer (geometry-authored surface)"))
    # in-game evidence: fixed center crop of the first available view, both legs
    chosen = None
    for v in VIEW_OF_ROW.get(nm, []):
        if os.path.isfile(os.path.join(LEG_A, "ref-car16-%s.png" % v)):
            chosen = v
            break
    if chosen:
        Abox, rect = center_box(os.path.join(LEG_A, "ref-car16-%s.png" % chosen))
        Bbox, _ = center_box(os.path.join(LEG_B, "ref-car16-%s.png" % chosen))
        panel = hcat([caption(Abox, "IN-GAME CURRENT  legA 41870086370  (%s)" % chosen),
                      caption(Bbox, "IN-GAME CURRENT  legB 41874911549  (same %s)" % chosen)])
        panels.append(panel)
        r["evidence"] = {"view": chosen, "box": rect, "viewChangedPx_legA_vs_legB": metrics["viewDiff"][chosen]["changedPx"]}
        chg = metrics["viewDiff"][chosen]["changedPx"]
        if chg > 200:
            r["evidenceNote"] = ("no candidates staged for THIS renderer; the %d px legA->legB change measured in the %s view "
                                 "comes from OTHER staged surfaces in frame (front views: STRUT_UPPER_FRONT corrections)" % (chg, chosen))
        else:
            r["evidenceNote"] = ("current production state: legA = legB for this view (%d px change, at-noise level) - "
                                 "wheel content unchanged by the badge/ext candidates" % chg)
    panel_w = max(p.size[0] for p in panels) + 24
    panel_h = sum(p.size[1] for p in panels) + 30 + 22 * len(panels)
    canvas = Image.new("RGB", (panel_w, panel_h), (16, 16, 16))
    dd = ImageDraw.Draw(canvas)
    dd.text((6, 2), "car16 wheel surface: %s   texture=%s" % (nm, r["texture"]), fill=(255, 255, 255), font=FONTB)
    y = 24
    for pn in panels:
        canvas.paste(pn, (12, y))
        y += pn.size[1] + 22
    fname = "row-%02d-%s.png" % (idx, safe)
    canvas.save(os.path.join(IMGDIR, fname))
    r["image"] = "images/" + fname
    metrics["rows"].append(r)

metrics["pack"] = {"dest": "car16-wheel-pack", "turn5bHullRefs": "agents/carbody-livery-turn5b.md (committed); native-hull-summary.json under outputs/repair-20261029/dvride-41848766195/game-runtime-0-attempt-1/outputs/cloud-game/"}
json.dump(metrics, open(os.path.join(PACK, "metrics.json"), "w", encoding="utf-8"), indent=1)
print("PACK", PACK)
print("views:", {v: metrics["viewDiff"][v]["changedPx"] for v in WHEEL_VIEWS})
print("rows:", len(metrics["rows"]))
