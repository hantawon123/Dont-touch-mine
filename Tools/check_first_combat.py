import bpy,sys,math
from pathlib import Path
from mathutils import Vector
p=Path(sys.argv[sys.argv.index('--')+1])
for name in ('Punch_Right','Hit_Head','Knocked_Out'):
 bpy.ops.wm.open_mainfile(filepath=str(p/(name+'.blend')),use_scripts=False);s=bpy.context.scene;r=bpy.data.objects['DGN_Armature'];end=s.frame_end
 s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.65,.72,.8);s.render.resolution_x=600;s.render.resolution_y=600;s.render.resolution_percentage=100
 c=bpy.data.objects.new('PreviewCamera',bpy.data.cameras.new('PreviewCamera'));s.collection.objects.link(c);s.camera=c;c.data.type='ORTHO';c.data.ortho_scale=2.5;target=r.matrix_world@Vector((0,.95,0));c.location=r.matrix_world@Vector((-2,1.3,3));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
 if name=='Knocked_Out':
  target=r.matrix_world@Vector((0,.6,-.5));c.location=target+r.matrix_world.to_3x3()@Vector((-2,1,3));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();c.data.ortho_scale=3
 previous=None;maxangle=0
 for f in range(end+1):
  s.frame_set(f);bpy.context.view_layer.update();qs=[b.matrix.to_quaternion() for b in r.pose.bones]
  if previous:maxangle=max(maxangle,max(2*math.acos(min(1,abs(a.dot(b)))) for a,b in zip(qs,previous)))
  previous=qs
  for b in r.pose.bones:assert abs((b.tail-b.head).length-b.bone.length)<1e-4
  if f%3==0 or f==end:s.render.filepath=str(p/name/f'{f:02}.png');bpy.ops.render.render(write_still=True)
 assert maxangle<math.radians(60),(name,maxangle)
 s.frame_set(0);bpy.ops.wm.save_as_mainfile(filepath=str(p/(name+'.blend')));print('PASS',name,'maxStep',math.degrees(maxangle))
