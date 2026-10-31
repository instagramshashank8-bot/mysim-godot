"""CI-only physics backend experiment. Source project defaults are never changed by this tool's authoring commit."""
import os,pathlib,json,re
backend=os.environ.get('NATIVE_PHYSICS_BACKEND','source');assert backend in ('source','jolt')
p=pathlib.Path('project.godot')
if backend=='jolt':
 text=p.read_text();settings='3d/physics_engine="Jolt Physics"\njolt_physics_3d/simulation/continuous_cd_movement_threshold=0.05\njolt_physics_3d/simulation/continuous_cd_max_penetration=0.01\njolt_physics_3d/simulation/penetration_slop=0.005\n'
 if '[physics]' in text:text=text.replace('[physics]','[physics]\n'+settings,1)
 else:text+='\n[physics]\n'+settings
 p.write_text(text)
out=pathlib.Path('outputs/cloud-game');out.mkdir(parents=True,exist_ok=True);(out/'physics-backend-experiment.json').write_text(json.dumps({'backend':backend,'scope':'Cloud-only comparison; no production engine promotion'},indent=2))
