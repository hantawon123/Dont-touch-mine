"""Compare staged thumb FBX against the working Unity FBX, including posed skinning."""
import bpy
import json
import numpy as np
from pathlib import Path
from mathutils import Quaternion

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/smooth-bear-unity/thumb-source-round'


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    meshes = {o.name: o for o in bpy.data.objects if o.type == 'MESH'}
    result = {'bones': {b.name: np.array(rig.matrix_world @ b.matrix_local) for b in rig.data.bones},
              'meshes': {}, 'poses': {}}
    for name, obj in meshes.items():
        result['meshes'][name] = {
            'vertices': np.array([tuple(obj.matrix_world @ v.co) for v in obj.data.vertices]),
            'polygons': [tuple(p.vertices) for p in obj.data.polygons],
            'weights': [[(obj.vertex_groups[g.group].name, g.weight) for g in v.groups] for v in obj.data.vertices],
            'keys': {k.name: np.array([tuple(p.co - q.co) for p, q in zip(k.data, obj.data.shape_keys.key_blocks[0].data)])
                     for k in obj.data.shape_keys.key_blocks} if obj.data.shape_keys else {},
            'uvs': [np.array([tuple(p.uv) for p in layer.data]) for layer in obj.data.uv_layers],
        }
    for pose in ['rest', 'grip', 'bend']:
        for bone in rig.pose.bones:
            bone.matrix_basis.identity()
            bone.rotation_mode = 'QUATERNION'
            if pose == 'grip' and bone.name.startswith('Finger_'):
                bone.rotation_quaternion = Quaternion((1, 0, 0), .7)
            elif pose == 'bend' and bone.name.startswith(('Arm.', 'UpperLeg.', 'Leg.')):
                bone.rotation_quaternion = Quaternion((1, 0, 0), .9)
        bpy.context.view_layer.update()
        graph = bpy.context.evaluated_depsgraph_get()
        result['poses'][pose] = {name: np.array([tuple(obj.matrix_world @ v.co)
                                    for v in obj.evaluated_get(graph).data.vertices]) for name, obj in meshes.items()}
    return result


old = load(ROOT / 'Assets/_Game/Content/Characters/SmoothBear/SmoothBear.fbx')
new = load(OUT / 'SmoothBear.fbx')
assert old['bones'].keys() == new['bones'].keys(), 'Bone names differ'
bone_error = max(float(np.max(np.abs(old['bones'][n] - new['bones'][n]))) for n in old['bones'])
assert bone_error < 1e-5, ('Bind matrices differ', bone_error)
assert old['meshes'].keys() == new['meshes'].keys(), 'Mesh names differ'
report = {'bone_count': len(old['bones']), 'bind_matrix_max_error': bone_error, 'meshes': {}}
for name, before in old['meshes'].items():
    after = new['meshes'][name]
    assert before['polygons'] == after['polygons'], ('Topology changed', name)
    assert before['weights'] == after['weights'], ('Weights changed', name)
    assert before['keys'].keys() == after['keys'].keys(), ('Shape keys changed', name)
    for key in before['keys']:
        assert np.max(np.abs(before['keys'][key] - after['keys'][key])) < 1e-6, ('Shape delta changed', name, key)
    assert len(before['uvs']) == len(after['uvs']), ('UV layers changed', name)
    for a, b in zip(before['uvs'], after['uvs']):
        assert np.max(np.abs(a - b)) < 1e-6, ('UV changed', name)
    distances = np.linalg.norm(before['vertices'] - after['vertices'], axis=1)
    changed = np.where(distances > 1e-5)[0]
    if name == 'Body':
        assert 0 < len(changed) < 200, ('Unexpected vertex changes', len(changed))
        assert all(any(g.startswith('Finger_T2.') and w >= .5 for g, w in before['weights'][i]) for i in changed)
    else:
        assert len(changed) == 0, ('Non-body geometry changed', name, len(changed))
    pose_errors = {}
    for pose in old['poses']:
        errors = np.linalg.norm(old['poses'][pose][name] - new['poses'][pose][name], axis=1)
        unchanged = distances <= 1e-5
        assert np.max(errors[unchanged]) < 1e-4, ('Skin deformation outside thumb', name, pose)
        assert np.max(errors) < .025, ('Skin mesh corrupted', name, pose)
        pose_errors[pose] = float(np.max(errors))
    report['meshes'][name] = {'vertices': len(distances), 'changed': len(changed), 'pose_errors': pose_errors}
report['passed'] = True
(OUT / 'fbx-validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('THUMB_FBX_VALIDATED', json.dumps(report), flush=True)
