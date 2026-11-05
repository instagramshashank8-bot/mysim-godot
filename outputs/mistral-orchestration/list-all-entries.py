import json
d=json.load(open('data/car_surface_uv_repairs.json'))
for k in d: print(k, '->', d[k].get('texture','')[-60:])
