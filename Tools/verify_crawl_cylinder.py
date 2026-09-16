import bpy,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];P=Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions');OUT=P/'crawl-cylinder'
all_entries=json.loads((P/'manifest.json').read_text()) if (P/'manifest.json').exists() else json.loads((P.parent/'manifest.json').read_text())
names=json.loads((OUT/'corrective-report.json').read_text())['clips'];entries=[a for a in all_entries if a['clip'] in names];report=[]
def load(path):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path));s=bpy.context.scene;r=next(o for o in s.objects if o.type=='ARMATURE');return s,r,bpy.data.objects['Body']
for a in entries:
 s,r,b=load(ROOT/'Assets/Scenes/CharacterTest/First'/a['file']);old=[]
 for f in range(1,a['end']+2):
  s.frame_set(f);old.append({p.name:p.matrix.copy() for p in r.pose.bones})
 s,r,b=load(OUT/a['file']);maxerr=0;poses=[]
 for f in range(1,a['end']+2):
  s.frame_set(f);bpy.context.view_layer.update()
  maxerr=max(maxerr,max(abs(p.matrix[j][k]-old[f-1][p.name][j][k]) for p in r.pose.bones for j in range(4) for k in range(4)))
  ev=b.evaluated_get(bpy.context.evaluated_depsgraph_get());me=ev.to_mesh();pts=[v.co.copy() for v in me.vertices];ev.to_mesh_clear()
  assert all(math.isfinite(c) and abs(c)<10 for v in pts for c in v),(a['clip'],f)
  if f in [1,a['end']+1]:poses.append(pts)
 assert maxerr<1e-4,(a['clip'],maxerr)
 closure=max((x-y).length for x,y in zip(*poses))
 if a['loop']:assert closure<.002,(a['clip'],closure)
 report.append({'clip':a['clip'],'bone_pose_max_error':maxerr,'frames_checked':a['end']+1,'closure':closure})
 if a['clip'] in ['Crawl_Forward','Crawl_Left','Carry_TwoHands_Crawl_Forward','Prone_Idle']:
  for f in [1,10,19,28] if a['clip']=='Crawl_Forward' else [19]:
   s.frame_set(f)
   for ob in s.objects:ob.hide_render=ob.type!='MESH'
   s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=560;s.render.resolution_y=500;s.render.resolution_percentage=100;s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.58,.66,.72)
   cam=bpy.data.objects.new('QA',bpy.data.cameras.new('QA'));s.collection.objects.link(cam);s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.35
   cam.location=(3.8,-5,2);cam.rotation_euler=(Vector((0,0,.55))-cam.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(OUT/f'{a["clip"]}_{f}.png');bpy.ops.render.render(write_still=True);bpy.data.objects.remove(cam,do_unlink=True)
 print('CHECKED',a['clip'],maxerr,flush=True)
 (OUT/'verification.json').write_text(json.dumps(report,indent=2))
