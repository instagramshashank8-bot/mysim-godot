"""Validate editable lighting/weather inputs before cloud build or simulation."""
import json,math,pathlib
root=pathlib.Path(__file__).resolve().parents[1]
def number(value,label,low=0,high=None):
 if isinstance(value,bool) or not isinstance(value,(float,int)) or not math.isfinite(value) or value<low or (high is not None and value>high):raise ValueError(label+' invalid finite range')
def colour(value,label):
 if not isinstance(value,list) or len(value)!=3:raise ValueError(label+' requires RGB')
 for component in value:number(component,label,0,1)
for filename in ('weather_profiles.json','lighting_profiles.json'):
 data=json.loads((root/'data'/filename).read_text());profiles=data['profiles'];assert profiles,filename+' empty'
 names=set()
 for profile in profiles:
  name=profile['name'];assert isinstance(name,str) and name.strip() and name not in names;names.add(name)
  if filename.startswith('weather'):
   number(profile['initialWater'],name+' initialWater',0,1)
   for key in ('rainRate','dryingRate','sunScale','fogDensity'):number(profile[key],name+' '+key)
   number(profile['rainAmount'],name+' rainAmount');assert isinstance(profile['rainAmount'],int)
   for key in ('skyTint','horizonTint'):colour(profile[key],name+' '+key)
  else:
   assert isinstance(profile['night'],bool);assert len(profile['rotation'])==3
   for angle in profile['rotation']:number(angle,name+' rotation',-360,360)
   for key in ('sunColor','ambientColor','fogColor','skyTop','horizon'):colour(profile[key],name+' '+key)
   for key in ('sunEnergy','ambientEnergy','exposure'):number(profile[key],name+' '+key)
 assert 'data/'+filename in (root/'export_presets.cfg').read_text(),filename+' excluded from player'
 print('PASS',filename,len(profiles),'valid editable profiles and player export inclusion')

rain=json.loads((root/'data/weather_profiles.json').read_text())['rainVisual']
for key in ('spawnHalfWidth','spawnHeight','fallSpeedMin','fallSpeedMax','lifetime','dropWidth','dropLength'):number(rain[key],'rain '+key,0.00001)
assert rain['fallSpeedMax']>=rain['fallSpeedMin']
number(rain['opacity'],'rain opacity',0,1);colour(rain['tint'],'rain tint')
assert len(rain['wind'])==3
for component in rain['wind']:number(component,'rain wind',-100,100)
print('PASS editable world-rain visual profile')

surface=json.loads((root/"data/weather_profiles.json").read_text())["surfaceVisual"]
for key in ("wet_darkening","wet_roughness","wet_specular"):number(surface[key],"wet ground "+key,0,1)
print("PASS editable water-driven ground material profile")
