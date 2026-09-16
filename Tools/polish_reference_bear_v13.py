import bpy,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'source/blender/characters/ReferenceBear_V13'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'),use_scripts=False)
s=bpy.context.scene;r=bpy.data.objects['DGN_Armature'];b=bpy.data.objects['Body'];basis=b.data.shape_keys.key_blocks[0]
# Source-specific corrective geometry. Old mesh deltas are not transferable by nearest vertex.
for k in list(b.data.shape_keys.key_blocks)[1:]:
    for i,v in enumerate(k.data):
        v.co=basis.data[i].co
        if k.name=='Belly_Breath':
            p=v.co;w=math.exp(-((p.y-.78)/.24)**4)*math.exp(-(abs(p.x)/.32)**8)
            v.co.x*=1+.012*w;v.co.z+=.005*w
    k.value=0
m=b.modifiers.new('Pose surface relaxation','CORRECTIVE_SMOOTH');m.factor=.55;m.iterations=8;m.use_only_smooth=True
bpy.context.view_layer.objects.active=b
bpy.ops.object.modifier_move_up(modifier=m.name)
# The source-specific empty correction channels retain action paths for future sculpting.
b['corrective_notes']='Belly_Breath rebuilt for this sculpt; other First correction channels reserved with neutral deltas. Source-specific pose relaxation active.'
t=bpy.data.texts['START_HERE.txt'];t.write('\nFINAL NOTE: Old body corrective deltas were removed after pose review. Belly_Breath is rebuilt; remaining correction channels are neutral, with pose surface relaxation.\nRebuild order: build_reference_bear_v13.py, finish_reference_bear_v13.py, repair_reference_bear_v13.py, polish_reference_bear_v13.py.\n')
s.camera=bpy.data.objects['Front'];s.render.resolution_percentage=100;s.cycles.samples=32;s.render.filepath=str(OUT/'preview-front.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'));bpy.ops.render.render(write_still=True)
print('POLISH COMPLETE',flush=True)
