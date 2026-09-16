"""Transfer the reference arm surface onto the current shared character mesh."""
import importlib.util
import json
import shutil
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp/arm-reference'
SOURCE = ROOT / 'Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx'
spec = importlib.util.spec_from_file_location('arms', ROOT / 'Tools/plump_first_arms.py')
arms = importlib.util.module_from_spec(spec)
spec.loader.exec_module(arms)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=sys.argv[-1], use_scripts=False)
    reference = bpy.data.objects['Body']
    rig = bpy.data.objects['DGN_Armature']
    rig.data.pose_position = 'REST'
    if reference.data.shape_keys:
        reference.data.shape_keys.animation_data_clear()
        for key in reference.data.shape_keys.key_blocks:
            key.value = 0
    bpy.context.view_layer.update()
    evaluated = reference.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    matrix = rig.matrix_world.inverted() @ reference.matrix_world
    positions = [matrix @ v.co for v in mesh.vertices]
    faces = [tuple(p.vertices) for p in mesh.polygons]
    evaluated.to_mesh_clear()
    rig, body, meshes = arms.hold.import_fbx(SOURCE)
    transform = rig.matrix_world.inverted() @ body.matrix_world
    current = [transform @ v.co for v in body.data.vertices]
    current_faces = [tuple(p.vertices) for p in body.data.polygons]
    print('TOPOLOGY', len(positions), len(current), len(faces), len(current_faces), flush=True)
    if len(positions) != len(current):
        raise ValueError('Reference subdivision topology differs')
    same_faces = sum(set(a) == set(b) for a, b in zip(faces, current_faces))
    near = sum((a - b).length < .005 for a, b in zip(positions, current))
    print('CORRESPONDENCE', same_faces, 'near', near, 'of', len(current), flush=True)
    assert same_faces == len(faces), 'Reference vertex correspondence not proven'
    start, end = (round(x) for x in rig.animation_data.action.frame_range)
    arms.OUT = OUT
    arms.render(rig, 'current-arms')
    offsets = []
    inverse = transform.inverted().to_3x3()
    for vertex, now, wanted in zip(body.data.vertices, current, positions):
        arm_weight = sum(g.weight for g in vertex.groups
                         if body.vertex_groups[g.group].name.startswith(('UpperArm.', 'Arm.', 'Hand.', 'Finger_')))
        mask = arms.hold.smooth((arm_weight - .1) / .6) * arms.hold.smooth((abs(now.x) - .36) / .17)
        offsets.append(inverse @ ((wanted - now) * mask))
    original = [v.co.copy() for v in body.data.vertices]
    weights = [[(g.group, g.weight) for g in v.groups] for v in body.data.vertices]
    for vertex, delta in zip(body.data.vertices, offsets):
        vertex.co += delta
    for key in body.data.shape_keys.key_blocks:
        for point, delta in zip(key.data, offsets):
            point.co += delta
    expected = [v.co.copy() for v in body.data.vertices]
    arms.render(rig, 'matched-arms')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'FirstCharacter_ReferenceArms.blend'))
    dest = OUT / SOURCE.name
    arms.hold.export_current(rig, meshes, dest, start, end)
    rig, body, _ = arms.hold.import_fbx(dest)
    assert len(body.data.vertices) == len(expected)
    error = max((v.co - p).length for v, p in zip(body.data.vertices, expected))
    assert error < 1e-5
    assert all([(g.group, g.weight) for g in v.groups] == w for v, w in zip(body.data.vertices, weights))
    actual = tuple(float(x) for x in rig.animation_data.action.frame_range)
    assert abs(actual[1] - actual[0] - (end - start)) < .001
    report = {'matched_reference_faces': same_faces, 'changed_vertices': sum(d.length > 1e-8 for d in offsets),
              'maximum_delta': max(d.length for d in offsets), 'roundtrip_error': error,
              'skin_weights_unchanged': True, 'non_arm_vertices_unchanged': True}
    (OUT / 'arm-match-report.json').write_text(json.dumps(report, indent=2))
    backup = OUT / 'before-reference-arms.fbx'
    if not backup.exists():
        shutil.copy2(SOURCE, backup)
    shutil.copy2(dest, SOURCE)
    print('INSTALLED', report, flush=True)


if __name__ == '__main__':
    main()
