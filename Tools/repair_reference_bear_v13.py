import bpy,math
from mathutils import Matrix,Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'source/blender/characters/ReferenceBear_V13'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'),use_scripts=False)
s=bpy.context.scene;r=bpy.data.objects['DGN_Armature'];b=bpy.data.objects['Body'];s.frame_set(1);bpy.context.view_layer.update()
sm={bone.name:r.pose.bones[bone.name].matrix@bone.matrix_local.inverted() for bone in r.data.bones}
for i,v in enumerate(b.data.vertices):
    weights=[(b.vertex_groups[g.group].name,g.weight) for g in v.groups];total=sum(w for n,w in weights)
    D=sum((sm[n]*(w/total) for n,w in weights),Matrix(((0,0,0,0),)*4));W=r.matrix_world@D
    p=W@b.data.shape_keys.key_blocks[0].data[i].co;sgn=1 if p.x>0 else -1
    dx=-.04*sgn if p.z<.43 else 0
    originalx=p.x+dx
    leg=max(0,min(1,(.32-abs(originalx))/.08));height=max(0,min(1,(.5-p.z)/.12))
    dx+=.04*sgn*leg*height
    delta=W.inverted_safe().to_3x3()@Vector((dx,0,0))
    for k in b.data.shape_keys.key_blocks:k.data[i].co+=delta
    v.co=b.data.shape_keys.key_blocks[0].data[i].co
s.camera=bpy.data.objects['Front'];s.cycles.samples=32;s.render.resolution_percentage=100;s.render.filepath=str(OUT/'preview-front.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'));bpy.ops.render.render(write_still=True)
print('REPAIR COMPLETE',flush=True)
