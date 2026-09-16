"""Re-import staged FBXs, check skin/animation binding and render representative poses."""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector
OUT=Path(sys.argv[sys.argv.index('--')+1])
manifest=json.loads((OUT/'manifest.json').read_text())
reports=[]
selected={'Idle','Walk_Forward','Run_Forward','Jump','Crouch_Idle','Crawl_Forward','Carry_TwoHands','Punch','Pickup_Low','Fall','Stun_Idle','Prone_Idle'}
for i,a in enumerate(manifest):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.fbx(filepath=str(OUT/'fbx'/a['file']),use_anim=True)
 s=bpy.context.scene;rig=next(o for o in s.objects if o.type=='ARMATURE');body=bpy.data.objects['Body']
 first,last=rig.animation_data.action.frame_range
 assert abs(last-first-a['end'])<.01,(a['clip'],first,last)
 assert len(rig.data.bones)==28,(a['clip'],len(rig.data.bones))
 assert len(body.data.vertices)>1537
 assert len(body.data.shape_keys.key_blocks)==6
 keyframes=[first,(first+last)/2,last];points=[]
 for f in keyframes:
  s.frame_set(math.floor(f),subframe=f-math.floor(f));bpy.context.view_layer.update()
  ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
  pts=[v.co.copy() for v in mesh.vertices];ev.to_mesh_clear()
  assert all(math.isfinite(c) for p in pts for c in p)
  points.append(pts)
 closure=max((p-q).length for p,q in zip(points[0],points[-1]))
 if a['loop']:assert closure<.002,(a['clip'],closure)
 reports.append({'clip':a['clip'],'vertices':len(body.data.vertices),'loop':a['loop'],'closure':closure})
 if a['clip'] in selected:
  f=first+a['end']*.33;s.frame_set(math.floor(f),subframe=f-math.floor(f))
  for ob in s.objects:ob.hide_render=ob.type!='MESH'
  s.render.engine='BLENDER_WORKBENCH';s.render.resolution_x=400;s.render.resolution_y=420;s.render.resolution_percentage=100
  s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.58,.66,.72);s.display.shading.show_cavity=False
  bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
  pts=[o.matrix_world@Vector(p) for o in s.objects if o.type=='MESH' for p in o.evaluated_get(dg).bound_box]
  low=Vector(tuple(min(p[j] for p in pts) for j in range(3)));high=Vector(tuple(max(p[j] for p in pts) for j in range(3)));center=(low+high)/2
  cam=bpy.data.objects.new('QA',bpy.data.cameras.new('QA'));s.collection.objects.link(cam);s.camera=cam
  cam.location=center+Vector((3,-6,2));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=max(2.4,(high-low).length*1.1)
  s.render.filepath=str(OUT/('pose_'+a['clip']+'.png'));bpy.ops.render.render(write_still=True)
 print('ROUNDTRIP',i+1,'/',len(manifest),a['clip'],flush=True)
 (OUT/'roundtrip-report.json').write_text(json.dumps(reports,indent=2))
print('ALL_EXPORTS_VERIFIED',len(reports),flush=True)
