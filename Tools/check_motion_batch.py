import bpy,sys
from pathlib import Path
from mathutils import Vector
p=Path(sys.argv[sys.argv.index('--')+1])
for path in p.glob('*.blend'):
 bpy.ops.wm.open_mainfile(filepath=str(path),use_scripts=False);s=bpy.context.scene;r=bpy.data.objects['DGN_Armature'];end=s.frame_end;s.frame_set(0);a=[b.matrix.copy() for b in r.pose.bones];s.frame_set(end)
 if 'Crawl' in path.stem:assert max(abs(x[i][j]-b.matrix[i][j]) for x,b in zip(a,r.pose.bones) for i in range(4) for j in range(4))<1e-4
 if path.stem not in ('Throw','Carry_Jump','Crawl_Forward'):continue
 s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.render.resolution_x=600;s.render.resolution_y=600;s.render.resolution_percentage=100
 c=bpy.data.objects.new('Preview',bpy.data.cameras.new('Preview'));s.collection.objects.link(c);s.camera=c;c.data.type='ORTHO';c.data.ortho_scale=3
 target=r.matrix_world@Vector((0,.8,0));c.location=r.matrix_world@Vector((-2,2,3));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();folder=p/path.stem;folder.mkdir(exist_ok=True)
 for f in range(0,end+1,3):s.frame_set(f);s.render.filepath=str(folder/f'{f:02}.png');bpy.ops.render.render(write_still=True)
print('PASS crawl seams')
