"""Cloud-only exhaustive original-frame index; extraction is not manual acceptance."""
import pathlib,subprocess,json,os,hashlib,io,shutil
from PIL import Image,ImageDraw
part=int(os.environ['REFERENCE_PART']);clip=pathlib.Path(f'/tmp/phone-reference/review/monoposto-part-{part}.mp4')
out=pathlib.Path('outputs/monoposto-full');out.mkdir(parents=True,exist_ok=True)
probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-select_streams','v:0','-show_frames','-show_entries','frame=best_effort_timestamp_time,width,height','-of','json',str(clip)]))
frames=probe['frames'];assert frames,'No decoded video frames'
run=os.environ['GITHUB_RUN_ID'];batch=0;count=0;records=[];assets=[];images=[]
folder=pathlib.Path('/tmp/frame-batch');folder.mkdir(exist_ok=True)
def flush():
 global batch,records,images
 if not records:return
 sheets=folder/'sheets';sheets.mkdir()
 for start in range(0,len(images),16):
  sheet=Image.new('RGB',(1280,720),(8,13,18));draw=ImageDraw.Draw(sheet)
  for tile,(path,record) in enumerate(zip(images[start:start+16],records[start:start+16])):
   with Image.open(path) as im:
    thumb=im.convert('RGB');thumb.thumbnail((320,152));x=(tile%4)*320;y=(tile//4)*180;sheet.paste(thumb,(x+(320-thumb.width)//2,y))
   draw.text((x+5,y+156),f"frame {record['frame']:06} / {record['pts']:.6f}s",fill='white')
  sheet.save(sheets/f'sheet-{start//16:02}.jpg',quality=90)
 (folder/'frames.jsonl').write_text(''.join(json.dumps(r)+'\n' for r in records))
 name=f'monoposto-full-{run}-part{part}-batch{batch:04}.enc';enc=pathlib.Path('/tmp')/name
 subprocess.run(['python','tools/cloud_encrypt_output.py',str(folder),str(enc)],check=True)
 digest=hashlib.sha256(enc.read_bytes()).hexdigest()
 for attempt in (1,2):
  result=subprocess.run(['gh','release','upload','repair-review-20261029',str(enc)])
  if result.returncode==0:break
 else:raise RuntimeError('Encrypted batch upload failed twice; preserve failure history')
 assets.append({'asset':name,'sha256':digest,'bytes':enc.stat().st_size,'firstFrame':records[0]['frame'],'lastFrame':records[-1]['frame'],'count':len(records),'manualReviewed':0})
 (out/f'part-{part}-coverage.json').write_text(json.dumps({'sourceClip':clip.name,'expectedFrames':len(frames),'extractedFrames':count,'completeExtraction':False,'manualReviewed':0,'accepted':0,'batches':assets},indent=2))
 shutil.rmtree(folder);folder.mkdir();enc.unlink();records=[];images=[];batch+=1
with open('/tmp/frame-decode.log','wb') as errors:
 process=subprocess.Popen(['ffmpeg','-v','error','-i',str(clip),'-map','0:v:0','-fps_mode','passthrough','-f','image2pipe','-c:v','mjpeg','-q:v','3','pipe:1'],stdout=subprocess.PIPE,stderr=errors)
 buffer=b''
 while True:
  chunk=process.stdout.read(65536)
  if not chunk:break
  buffer+=chunk
  while True:
   end=buffer.find(b'\xff\xd9')
   if end<0:break
   data=buffer[:end+2];buffer=buffer[end+2:];assert data.startswith(b'\xff\xd8'),'Invalid JPEG boundary'
   assert count<len(frames),'More extracted frames than source index'
   source=frames[count];count+=1;path=folder/f'frame-{count:06}.jpg';path.write_bytes(data)
   with Image.open(io.BytesIO(data)) as image:dimensions=image.size
   assert dimensions==(source['width'],source['height']),'Unexpected resize'
   record={'part':part,'frame':count,'pts':float(source['best_effort_timestamp_time']),'sourceWidth':dimensions[0],'sourceHeight':dimensions[1],'imageSha256':hashlib.sha256(data).hexdigest(),'manualReviewed':False,'qualityAccepted':False}
   records.append(record);images.append(path)
   if len(records)==256:flush()
  assert len(buffer)<32*1024*1024,'Unbounded image decode buffer'
 code=process.wait();assert code==0,f'ffmpeg exit {code}';assert not buffer,'Incomplete trailing frame';flush()
assert count==len(frames),f'Incomplete frame coverage {count}/{len(frames)}'
(out/f'part-{part}-coverage.json').write_text(json.dumps({'sourceClip':clip.name,'expectedFrames':len(frames),'extractedFrames':count,'completeExtraction':True,'manualReviewed':0,'accepted':0,'scope':'Every decoded original-resolution video frame indexed; manual review and game integration NOT complete','batches':assets},indent=2))
print('EXHAUSTIVE_FRAME_INDEX_COMPLETE',part,count,'manualReviewed=0')
