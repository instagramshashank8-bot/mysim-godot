"""F1 TRIAD visual review pack (cars 15 W10 / 17 RB15 / 18 MCL34) - read-only.

Builds outputs/mistral-orchestration/f1-triad-pack/ : per-car four-panel main strip
(SOURCE atlas island - BEFORE candidate-0 - AFTER candidate-1 - CURRENT in-game view)
plus a wheel/rubber closeup strip per car, per the documented defects:
car15 W10 fragmented atlas, car17 RB15 featureless rubber, car18 MCL34 coated-paint
unestablished. All from existing local artifacts; gaps stated; art decisions stay
with the human reviewer.
"""
import json, os, re, math
from PIL import Image, ImageDraw, ImageFont

ROOT = r"C:/Users/shash/Documents/MySimGodot"
REP = os.path.join(ROOT, "outputs/repair-20261029")
PACK = r"C:/Users/shash/MySimGodot-wt-carbody/outputs/mistral-orchestration/f1-triad-pack"
IMGDIR = os.path.join(PACK, "images")
os.makedirs(IMGDIR, exist_ok=True)

ALT = os.path.join(REP, r"cloud/car-material-bindings/game-runtime-0-attempt-1/outputs/cloud-game/geometry.json")

CARS = {
    15: {
        "name": "Mercedes W10 (2019 F1)",
        "before": REP + "/cloud/coatedpaint0/review/f1-paint-car-15-candidate-0-view-0.png",
        "after": REP + "/cloud/coatedpaint0/review/f1-paint-car-15-candidate-1-view-0.png",
        "current": REP + "/cloud/car-review-current/review/car-15-view-0.png",
        "wheels": [REP + "/cloud/deepwet0/review/rebuilt-wheel-car-15-compound-%d-wheel-%d.png" % (c, w)
                   for c in (0, 1, 2) for w in (0, 2)],
        "reject": "fragmented source atlas (FULL_F1_W10_WHEEL_REVIEW run 41310403219 + FOUR_CAR_PAINT_PARTIAL_REVIEW run 41335778130 @960e56f)",
    },
    17: {
        "name": "Red Bull RB15 (2019 F1)",
        "before": REP + "/cloud/coatedpaint2/review/f1-paint-car-17-candidate-0-view-0.png",
        "after": REP + "/cloud/coatedpaint2/review/f1-paint-car-17-candidate-1-view-0.png",
        "current": REP + "/cloud/car-review-current/review/car-17-view-0.png",
        "wheels": [REP + "/cloud/f1-custom-renders/game-review-41300908861-1-2/review/rebuilt-wheel-car-17-compound-%d-wheel-%d.png" % (c, w)
                   for c in (0, 1, 2) for w in (0, 2)],
        "reject": "rubber featureless / brake detail absent (RB15_SIDEWALL_CORRECTION_REVIEW run 41302933144: marking placement verified; FULL_F1_RB15_WHEEL_REVIEW run 41310403219)",
    },
    18: {
        "name": "McLaren MCL34 (2019 F1)",
        "before": REP + "/cloud/coatedpaint3/review/f1-paint-car-18-candidate-0-view-0.png",
        "after": REP + "/cloud/coatedpaint3/review/f1-paint-car-18-candidate-1-view-0.png",
        "current": REP + "/fleet-41848086075/decrypted-shard0/review/fleet-car-18-front.png",
        "wheels": [REP + "/fleet-41848086075/decrypted-shard0/review/fleet-car-18-wheel-%s.png" % q for q in ("fl", "fr", "rl", "rr")],
        "reject": "coated-paint improvement not established (COATED_PAINT_PARTIAL_REVIEW run 41337127855 @ff93c22); body atlas fragmented (FULL_F1_MCL34_WHEEL_REVIEW run 41310403219)",
    },
}

try:
    FONTB = ImageFont.truetype("arialbd.ttf", 15)
    FONTS = ImageFont.truetype("arial.ttf", 13)
except Exception:
    FONTB = FONTS = ImageFont.load_default()

def crop_atlas(path, u0, v0, u1, v1, max_side=430):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    a = None
    import numpy as np
    a = np.asarray(im, dtype=np.uint8)
    xs = (np.arange(int(math.floor(u0 * W)), int(math.ceil(u1 * W))) % W)
    ys = (np.arange(int(math.floor(v0 * H)), int(math.ceil(v1 * H))) % H)
    img = Image.fromarray(a[np.ix_(ys, xs)])
    w, h = img.size
    s = max_side / max(w, h)
    if s != 1.0:
        img = img.resize((max(1, int(w * s)), max(1, int(h * s))), Image.NEAREST)
    return img, (W, H)

def fit(path_or_img, width=560):
    if isinstance(path_or_img, str):
        im = Image.open(path_or_img).convert("RGB")
    else:
        im = path_or_img
    w, h = im.size
    s = width / w
    return im.resize((width, max(1, int(h * s))), Image.NEAREST)

def caption(im, text):
    c = Image.new("RGB", (im.size[0], 24), (30, 30, 30))
    ImageDraw.Draw(c).text((4, 4), text, fill=(255, 255, 255), font=FONTS)
    out = Image.new("RGB", (im.size[0], 24 + im.size[1]), (10, 10, 10))
    out.paste(c, (0, 0))
    out.paste(im, (0, 24))
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

metrics = {"cars": {}}
import numpy as np
for idx, cfg in sorted(CARS.items()):
    ALTROW = None
    g = json.load(open(ALT, encoding="utf-8"))
    carr = next((c for c in g if c.get("car") == idx), None)
    body = next((vm for vm in carr["visibleMaterials"]
                 if vm.get("renderer") == "LOD_A_BODY_mm_ext" and vm.get("slot", "_BaseMap") == "_BaseMap"), None)
    atlas_rel = str(body["texture"]).replace("res://", "")
    atlas = os.path.join(ROOT, atlas_rel.replace("/", os.sep))
    umin = [float(x) for x in re.findall(r"-?\d+\.?\d*", str(body["uvMinimum"]))][:2]
    umax = [float(x) for x in re.findall(r"-?\d+\.?\d*", str(body["uvMaximum"]))][:2]
    src_crop, dims = crop_atlas(atlas, umin[0], umin[1], umax[0], umax[1])

    panels = [caption(src_crop, "SOURCE (authored BODY ext island)  atlas %s  u[%.2f..%.2f] v[%.2f..%.2f]"
                                    % (os.path.basename(atlas), umin[0], umax[0], umin[1], umax[1]))]
    row = {"atlas": os.path.basename(atlas), "dims": dims, "bodyIsland": [umin, umax], "panels": []}
    for key, label in (("before", "BEFORE candidate-0 (run 41335778130)" if idx != 18 else "BEFORE candidate-0 (run 41337127855)"),
                       ("after", "AFTER candidate-1"), ("current", "CURRENT in-game view")):
        p = cfg.get(key)
        if p and os.path.isfile(p):
            panels.append(caption(fit(p), label + "  " + os.path.basename(p)))
            row["panels"].append({"label": label, "file": os.path.relpath(p, ROOT).replace("\\", "/")})
        else:
            row["panels"].append({"label": label, "file": None})
    strip = hcat(panels)
    fname_main = "car%02d-main.png" % idx
    Image.new("RGB", (strip.size[0] + 24, strip.size[1] + 34), (16, 16, 16)).save(os.path.join(IMGDIR, "tmp-%s" % fname_main))
    canvas = Image.new("RGB", (strip.size[0] + 24, strip.size[1] + 50), (16, 16, 16))
    ImageDraw.Draw(canvas).text((8, 4), "car%d %s   documented reject: painter/UV state per the L1/L2 reviews" % (idx, cfg["name"]), fill=(255, 255, 255), font=FONTB)
    canvas.paste(strip, (12, 26))
    canvas.save(os.path.join(IMGDIR, fname_main))
    os.remove(os.path.join(IMGDIR, "tmp-%s" % fname_main))

    wp = [p for p in cfg["wheels"] if os.path.isfile(p)]
    wheel_row = {"present": len(wp), "missing": [os.path.relpath(p, ROOT).replace("\\", "/") for p in cfg["wheels"] if not os.path.isfile(p)]}
    if wp:
        wstrip = hcat([caption(fit(p), os.path.basename(p).replace("rebuilt-wheel-", "").replace(".png", "")
                               .replace("fleet-car-18-wheel-", "fleet18-wheel-")) for p in wp])
        canvas = Image.new("RGB", (wstrip.size[0] + 24, wstrip.size[1] + 50), (16, 16, 16))
        ImageDraw.Draw(canvas).text((8, 4), "car%d wheel/rubber closeups (%d of %d)" % (idx, len(wp), len(cfg["wheels"])), fill=(255, 255, 255), font=FONTB)
        canvas.paste(wstrip, (12, 26))
        fname_w = "car%02d-wheels.png" % idx
        canvas.save(os.path.join(IMGDIR, fname_w))
        wheel_row["image"] = "images/" + fname_w
    metrics["cars"][idx] = {"name": cfg["name"], "main": "images/" + fname_main, "wheelStrip": wheel_row,
                            "reject": cfg["reject"]}

metrics["dirs"] = {"coatedpaint0/2/3": "the f1-paint candidate artifacts (candidate-0 vs candidate-1 per car)",
                   "car-review-current": "the most recent generic car views",
                   "fleet-41848086075/decrypted-shard0": "car18 production fleet captures (42/42 set, local)"}
json.dump(metrics, open(os.path.join(PACK, "metrics.json"), "w", encoding="utf-8"), indent=1)
print("PACK", PACK)
for k, v in metrics["cars"].items():
    print("car%s: missing wheel files = %d" % (k, len(v["wheelStrip"]["missing"])))
