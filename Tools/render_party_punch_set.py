import bpy,sys
from pathlib import Path
from mathutils import Vector
OUT=Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv else Path('C:/Users/SSAFY/.codex/visualizations/2026/09/07/01a0792f-184a-72c2-8704-dfc1b0ae814d/party_punch_footwork')
for name in ('Punch_Left','Punch_Combo','Punch_Combo_Run','Punch_Combo_Crouch_Walk'):
 bpy.ops.wm.open_mainfile(filepath=str(OUT/f'{name}.blend'),use_scripts=False)
 s=bpy.context.scene;r=next(o for o in s.objects if o.type=='ARMATURE')
 s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.65,.72,.8)
 s.render.resolution_x=500;s.render.resolution_y=500;s.render.resolution_percentage=100
 c=bpy.data.objects.new('PreviewCamera',bpy.data.cameras.new('PreviewCamera'));s.collection.objects.link(c);s.camera=c;c.data.type='ORTHO';c.data.ortho_scale=2.55
 target=r.matrix_world@Vector((0,.9,0));c.location=r.matrix_world@Vector((1.5,1.3,3));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
 for f in range(0,s.frame_end+1,2):
  s.frame_set(f);s.render.filepath=str(OUT/name/f'{f:03}.png');bpy.ops.render.render(write_still=True)
 for screen in bpy.data.screens:
  for area in screen.areas:
   if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
 s.frame_set(10);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/f'{name}.blend'))
