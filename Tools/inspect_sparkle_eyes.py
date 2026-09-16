import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(root/'source/blender/characters/SmoothBear/Shoes/BasicPlayerCapsule_WithWalk_Bear_Ears_Shoes_Compact.blend'),use_scripts=False)
rig=bpy.data.objects['DGN_Armature'];rig.data.pose_position='REST';bpy.context.view_layer.update()
data={}
for obj in bpy.data.objects:
    if any(k in obj.name.lower() for k in ['eye','mouth','hood']):
        if obj.type!='MESH':continue
        pts=[obj.matrix_world@v.co for v in obj.data.vertices]
        data[obj.name]={'min':[min(p[k] for p in pts) for k in range(3)],'max':[max(p[k] for p in pts) for k in range(3)],'matrix':[list(row) for row in obj.matrix_world],'groups':[g.name for g in obj.vertex_groups],'modifiers':[(m.type,m.name) for m in obj.modifiers],'materials':[m.name if m else None for m in obj.data.materials],'parent':obj.parent.name if obj.parent else None}
data['head_bones']=[b.name for b in rig.data.bones if any(k in b.name.lower() for k in ['head','neck','eye'])]
print('EYE_INSPECT '+json.dumps(data),flush=True)
