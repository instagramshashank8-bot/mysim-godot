"""Cloud-only visual evidence extraction; outputs are reference observations, not physics telemetry."""
import json,pathlib,subprocess,math
from PIL import Image,ImageDraw
source=pathlib.Path('/tmp/phone-reference/review');out=pathlib.Path('outputs/phone-reference');out.mkdir(parents=True,exist_ok=True)
clips=sorted(source.glob('*.mp4'));assert 1<=len(clips)<=8,'Unexpected reference clip count'
manifest=[]
for clip in clips:
 probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_format','-show_streams','-of','json',str(clip)]));duration=float(probe['format']['duration']);assert math.isfinite(duration) and duration>0
 folder=out/clip.stem;folder.mkdir(exist_ok=True)
 subprocess.run(['ffmpeg','-v','error','-i',str(clip),'-vf',"fps=1/8,scale='min(1280,iw)':-2",'-q:v','3',str(folder/'frame-%04d.jpg')],check=True)
 frames=sorted(folder.glob('frame-*.jpg'));assert frames
 for start in range(0,len(frames),12):
  sheet=Image.new('RGB',(1280,660),(8,13,18));draw=ImageDraw.Draw(sheet)
  for cell,path in enumerate(frames[start:start+12]):
   image=Image.open(path).convert('RGB');image.thumbnail((320,190));x=(cell%4)*320;y=(cell//4)*220;sheet.paste(image,(x+(320-image.width)//2,y));draw.text((x+5,y+193),f'{clip.stem} / sample {start+cell+1}',fill='white');draw.text((x+5,y+206),'~'+str((start+cell+.5)*8)+' sec (fps sampling)',fill=(100,220,205))
  sheet.save(out/(clip.stem+f'-sheet-{start//12:02}.jpg'))
 # A short motion sequence permits direct inspection of wheel/body/camera continuity.
 burst=min(30,max(0,duration-4));subprocess.run(['ffmpeg','-v','error','-ss',str(burst),'-i',str(clip),'-t','3','-vf',"fps=2,scale='min(1280,iw)':-2",'-q:v','3',str(folder/'motion-%02d.jpg')],check=True)
 manifest.append({'clip':clip.name,'duration_seconds':duration,'sampled_frames':len(frames),'motion_burst_start_seconds':burst,'scope':'Observed video frames only; no force constants, exact telemetry, driving quality or lap completion inferred'})
(out/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n');print('REFERENCE_FRAME_EXTRACTION_COMPLETE',len(clips),'clips; manual review remains required')
