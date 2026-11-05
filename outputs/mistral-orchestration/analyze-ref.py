from PIL import Image
import os, json
base='outputs/repair-20261029/cloud/reference-evidence-41788844369/encrypted-review-0-attempt-1/game-review-41788844369-1-0/review/'
out={}
for name in ('ref-car16-front','ref-car16-rear','ref-car16-left','ref-car16-right','ref-car16-top','ref-car16-wheel-fl','ref-car16-cockpit','ref-lap-near','ref-lap-wide'):
    p=base+name+'.png'
    if not os.path.exists(p):
        for root,dirs,files in os.walk('outputs/repair-20261029/cloud/reference-evidence-41788844369'):
            for f in files:
                if f==name+'.png': p=os.path.join(root,f)
    if not os.path.exists(p): out[name]='MISSING'; continue
    im=Image.open(p).convert('RGB'); w,h=im.size
    def mean(box):
        c=im.crop(box).resize((24,14)); px=list(c.getdata()); n=len(px)
        return [round(sum(q[i] for q in px)/n) for i in range(3)]
    thirds=[mean((0,0,w//3,h)),mean((w//3,0,2*w//3,h)),mean((2*w//3,0,w,h))]
    c3=im.crop((w//3,0,2*w//3,h)).resize((48,27)); px=list(c3.getdata()); n=len(px)
    dark=sum(1 for r,g,b in px if r<40 and g<40 and b<40)*100//n
    red=sum(1 for r,g,b in px if r>90 and g<60 and b<60)*100//n
    brightgrass=sum(1 for r,g,b in px if g>100 and r>80 and b<70)*100//n
    out[name]={'size':[w,h],'thirds_rgb':thirds,'centre_dark_pct':dark,'centre_red_pct':red,'centre_brightgrass_pct':brightgrass}
with open('outputs/repair-20261029/REFERENCE_EVIDENCE_METRICS.json','w') as f: json.dump(out,f,indent=1)
print(json.dumps(out))
