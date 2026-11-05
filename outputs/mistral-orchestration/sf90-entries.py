import json
d=json.load(open('data/car_surface_uv_repairs.json'))
for k in d:
    if 'sf90' in k.lower() or '2019' in k.lower(): print(k)
print('total entries:',len(d))
