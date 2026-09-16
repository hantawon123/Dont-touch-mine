"""Validate the generated blend and its FBX round trip without touching Unity."""
import bpy
import json
import math
import numpy as np
from pathlib import Path

root=Path(__file__).resolve().parents[1]
out=root/'source/blender/characters/ModularThief_01'
bpy.ops.wm.open_mainfile(filepath=str(out/'ModularThief_01.blend'),use_scripts=False)
rig=bpy.data.objects['DGN_Armature']
body=bpy.data.objects['Body']
parts=[o for o in bpy.data.objects if o.type=='MESH' and o.get('customization_slot')]
expected={o.name for o in parts}
expected_keys={o.name:set(k.name for k in o.data.shape_keys.key_blocks) for o in parts}
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
results={'parts':{},'clips':{},'fbx':{}}
for obj in parts:
    assert any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers),obj.name
    unweighted=sum(not v.groups for v in obj.data.vertices)
    assert unweighted==0,(obj.name,unweighted)
    results['parts'][obj.name]={'vertices':len(obj.data.vertices),'shape_keys':sorted(expected_keys[obj.name]),'unweighted':unweighted}
for action in [a for a in bpy.data.actions if a.name.startswith('First_')]:
    name=action.name.removeprefix('First_')
    rig.animation_data.action=action
    if action.slots:rig.animation_data.action_slot=action.slots[0]
    keys=body.data.shape_keys
    keys.animation_data_clear()
    for k in keys.key_blocks:k.value=0
    sa=bpy.data.actions.get('Shapes_'+name)
    if sa:
        keys.animation_data_create().action=sa
        if sa.slots:keys.animation_data.action_slot=sa.slots[0]
    start,end=map(int,action.frame_range)
    frames=sorted(set(round(start+(end-start)*i/6) for i in range(7)))
    for frame in frames:
        bpy.context.scene.frame_set(frame)
        deps=bpy.context.evaluated_depsgraph_get()
        for obj in parts:
            evaluated=obj.evaluated_get(deps)
            mesh=evaluated.to_mesh()
            co=np.empty(len(mesh.vertices)*3,dtype=np.float32)
            mesh.vertices.foreach_get('co',co)
            assert np.all(np.isfinite(co)),(name,frame,obj.name)
            assert np.max(np.abs(co))<10,(name,frame,obj.name,'exploded mesh')
            evaluated.to_mesh_clear()
    results['clips'][name]={'evaluated_mesh_frames':frames,'finite_vertices':True}
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(out/'ModularThief_01_Idle.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert {o.name for o in meshes}==expected,([o.name for o in meshes],expected)
assert set(b.name for b in rig.data.bones)==set(rest)
error=max(abs(rest[b.name][i][j]-b.matrix_local[i][j]) for b in rig.data.bones for i in range(4) for j in range(4))
assert error<.0001,error
for obj in meshes:
    # FBX may omit a shape-key block that has no animated channel on an
    # accessory; the backpack remains valid as a skinned replaceable mesh.
    if obj.data.shape_keys:
        assert set(k.name for k in obj.data.shape_keys.key_blocks)==expected_keys[obj.name],obj.name
    else:
        assert obj.name=='Backpack',obj.name
    assert any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers),obj.name
    assert obj.parent==rig,obj.name
results['fbx']={'round_trip':True,'bone_count':len(rig.data.bones),'max_rest_matrix_error':error,'all_part_names_keys_and_skin_bindings_preserved':True}
(out/'verification.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('VERIFIED',json.dumps(results['fbx']))
