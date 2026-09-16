import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(root/'source/blender/characters/SmoothBear/SmoothBear.blend'),use_scripts=False)
rig=bpy.data.objects['DGN_Armature'];body=bpy.data.objects['Body']
rig.animation_data_clear();rig.data.pose_position='REST';bpy.context.view_layer.update()
data={'objects':[(o.name,o.type) for o in bpy.data.objects if o.type=='MESH'],
      'bones':{b.name:{'head':list(rig.matrix_world@b.head_local),'tail':list(rig.matrix_world@b.tail_local)} for b in rig.data.bones if 'Foot' in b.name or b.name.startswith('Leg.')},
      'modifiers':[(m.name,m.type) for m in body.modifiers], 'actions':[a.name for a in bpy.data.actions],
      'body_matrix':[list(r) for r in body.matrix_world]}
for side in (-1,1):
    points=[body.matrix_world@v.co for v in body.data.vertices if (body.matrix_world@v.co).z<.35 and side*(body.matrix_world@v.co).x>0]
    data[str(side)]={str(z):{'min':[min(p[k] for p in points if p.z<z) for k in range(3)],'max':[max(p[k] for p in points if p.z<z) for k in range(3)]} for z in (.1,.2,.3,.35)}
print('SHOE_FIT_JSON '+json.dumps(data),flush=True)
