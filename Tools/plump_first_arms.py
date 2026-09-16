"""Add 15% radial fullness to the shared First character's arms.

The Idle FBX supplies the character mesh for every controller state. Skin
weights, body shape, animation curves and shape-key offsets remain unchanged.
Run with Blender --background --python-exit-code 1 --python this_file.py.
"""
import importlib.util
import json
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp/plump-first-arms'
SOURCE = ROOT / 'Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx'
spec = importlib.util.spec_from_file_location('hold', ROOT / 'Tools/hold_crawl_torso.py')
hold = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hold)


def offsets(body, rig):
    to_rig = rig.matrix_world.inverted() @ body.matrix_world
    to_body = to_rig.inverted().to_3x3()
    result = []
    for vertex in body.data.vertices:
        co = to_rig @ vertex.co
        side = 'L' if co.x > 0 else 'R'
        weight = sum(g.weight for g in vertex.groups
                     if body.vertex_groups[g.group].name.endswith('.' + side)
                     and body.vertex_groups[g.group].name.startswith(('UpperArm.', 'Arm.', 'Hand.', 'Finger_')))
        mask = hold.smooth((weight - .15) / .65) * hold.smooth((abs(co.x) - .37) / .18)
        if mask == 0:
            result.append(Vector())
            continue
        nearest = None
        for name in ('UpperArm.' + side, 'Arm.' + side, 'Hand.' + side):
            bone = rig.data.bones[name]
            axis = (bone.tail_local - bone.head_local).normalized()
            distance = (co - bone.head_local).dot(axis)
            center = bone.head_local + axis * max(0, min(bone.length, distance))
            candidate = ((co - center).length_squared, axis, bone.head_local)
            if nearest is None or candidate[0] < nearest[0]:
                nearest = candidate
        _, axis, origin = nearest
        radial = co - origin - axis * (co - origin).dot(axis)
        result.append(to_body @ (radial * (.15 * mask)))
    return result


def render(rig, label):
    scene = bpy.context.scene
    scene.frame_set(1)
    for obj in scene.objects:
        obj.hide_render = obj.type != 'MESH'
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 700
    scene.render.resolution_percentage = 100
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (.7, .65, .35)
    scene.display.shading.show_cavity = True
    cam = bpy.data.objects.new('QA', bpy.data.cameras.new('QA'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 2.7
    cam.location = (2.2, -6, 2.1)
    cam.rotation_euler = (Vector((0, 0, 1)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = str(OUT / (label + '.png'))
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    rig, body, meshes = hold.import_fbx(SOURCE)
    start, end = (round(x) for x in rig.animation_data.action.frame_range)
    delta = offsets(body, rig)
    untouched = [i for i, d in enumerate(delta) if d.length < 1e-9]
    original = [v.co.copy() for v in body.data.vertices]
    render(rig, 'before')
    for vertex, d in zip(body.data.vertices, delta):
        vertex.co += d
    for key in body.data.shape_keys.key_blocks:
        for point, d in zip(key.data, delta):
            point.co += d
    assert all((body.data.vertices[i].co - original[i]).length < 1e-9 for i in untouched)
    render(rig, 'after')
    destination = OUT / SOURCE.name
    hold.export_current(rig, meshes, destination, start, end)
    count = len(original)
    expected = [v.co.copy() for v in body.data.vertices]
    expected_weights = [[(g.group, g.weight) for g in v.groups] for v in body.data.vertices]
    rig, body, _ = hold.import_fbx(destination)
    assert len(body.data.vertices) == count
    error = max((v.co - p).length for v, p in zip(body.data.vertices, expected))
    assert error < 1e-5, error
    assert all([(g.group, g.weight) for g in v.groups] == w for v, w in zip(body.data.vertices, expected_weights))
    actual = tuple(float(x) for x in rig.animation_data.action.frame_range)
    assert abs((actual[1] - actual[0]) - (end - start)) < .001, (start, end, actual)
    backup = OUT / 'before.fbx'
    if not backup.exists():
        shutil.copy2(SOURCE, backup)
    shutil.copy2(destination, SOURCE)
    report = {'changed_vertices': sum(d.length > 1e-9 for d in delta),
              'max_radial_scale': 1.15, 'max_offset': max(d.length for d in delta),
              'unchanged_vertices': len(untouched), 'roundtrip_error': error,
              'skin_weights_unchanged': True, 'installed': str(SOURCE)}
    (OUT / 'report.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
