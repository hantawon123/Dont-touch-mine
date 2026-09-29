"""Reweight the torso capsule independently of swinging limbs and verify FBX output.

Run with Blender --background --python Tools/stabilize_crawl_torso.py -- [--install].
Original FBXs are backed up before installation; bone animations are not edited.
"""
import argparse
import importlib.util
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Temp" / "crawl-torso-stable"
UNITY = ROOT / "Assets/Scenes/CharacterTest/First"
spec = importlib.util.spec_from_file_location("hold", ROOT / "Tools/hold_crawl_torso.py")
hold = importlib.util.module_from_spec(spec)
spec.loader.exec_module(hold)


def stabilize(body, rig):
    """Rigid central capsule, smooth joins into the articulated arms and thighs."""
    spine = body.vertex_groups['Spine']
    changed = 0
    core = []
    for vertex in body.data.vertices:
        co = vertex.co
        mask = 1.0 - hold.limb_mask(co, rig)
        mask *= hold.smooth((co.y - .44) / .15)
        mask *= 1.0 - hold.smooth((co.y - 1.27) / .20)
        if mask < 1e-6:
            continue
        transfer = 0.0
        for group in list(vertex.groups):
            if group.group == spine.index:
                continue
            name = body.vertex_groups[group.group].name
            if name not in rig.data.bones or not rig.data.bones[name].use_deform:
                continue
            take = group.weight * mask
            body.vertex_groups[group.group].add([vertex.index], group.weight - take, 'REPLACE')
            transfer += take
        spine.add([vertex.index], transfer, 'ADD')
        if transfer > 1e-6:
            changed += 1
        if mask > .99999:
            core.append(vertex.index)
    # These old asymmetric offsets reproduced the thigh motion on the torso.
    # Keep the channels for animation compatibility, with neutral geometry.
    keys = body.data.shape_keys
    if keys:
        for name in ('Crawl_Waist_Round', 'Crawl_Follow_L', 'Crawl_Follow_R'):
            key = keys.key_blocks.get(name)
            if key:
                for point, base in zip(key.data, keys.key_blocks[0].data):
                    point.co = base.co
    return changed, core


def snapshot(rig, body, frames, core):
    scene = bpy.context.scene
    result = []
    for frame in frames:
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        to_spine = (rig.matrix_world @ rig.pose.bones['Spine'].matrix).inverted() @ body.matrix_world
        result.append({
            'bones': {b.name: b.matrix.copy() for b in rig.pose.bones},
            'core': [to_spine @ mesh.vertices[i].co for i in core],
            'vertices': [v.co.copy() for v in mesh.vertices],
        })
        evaluated.to_mesh_clear()
    return result


def drift(samples):
    return max(((p - q).length for s in samples for p, q in zip(s['core'], samples[0]['core'])), default=0)


def render(rig, body, label, frames=(1, 10, 19, 28)):
    scene = bpy.context.scene
    for obj in scene.objects:
        obj.hide_render = obj.type != 'MESH'
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 640
    scene.render.resolution_y = 560
    scene.render.resolution_percentage = 100
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (.75, .67, .25)
    scene.display.shading.show_cavity = True
    camera = bpy.data.objects.new('QA_Camera', bpy.data.cameras.new('QA_Camera'))
    scene.collection.objects.link(camera)
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 2.7
    camera.location = (3.8, 5, 3.0)
    camera.rotation_euler = (Vector((0, 0, .6)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    for frame in frames:
        scene.frame_set(frame)
        scene.render.filepath = str(OUT / f'{label}-{frame:02d}.png')
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    OUT.mkdir(parents=True, exist_ok=True)
    report = []
    # Idle supplies the shared mesh for CharacterTest and the player prefab.
    files = [UNITY / 'FirstPlayerCapsule_Crawl_Forward.fbx']
    if args.install:
        files = sorted(path for path in UNITY.glob('FirstPlayerCapsule_*.fbx')
                       if path.stem == 'FirstPlayerCapsule_Idle' or 'Crawl' in path.stem or 'Prone' in path.stem)
    staged = OUT / 'fbx'
    staged.mkdir(exist_ok=True)
    for path in files:
        rig, body, meshes = hold.import_fbx(path)
        scene = bpy.context.scene
        start, end = (int(x) for x in rig.animation_data.action.frame_range)
        frames = list(range(start, end + 1))
        # Determine the mask on a copy, keeping the original for comparison.
        old_mesh = body.data.copy()
        changed, core = stabilize(body, rig)
        new_mesh = body.data
        body.data = old_mesh
        before = snapshot(rig, body, frames, core)
        if path.stem == 'FirstPlayerCapsule_Crawl_Forward':
            render(rig, body, 'before')
        body.data = new_mesh
        after = snapshot(rig, body, frames, core)
        error = max(abs(s['bones'][n][j][k] - t['bones'][n][j][k])
                    for s, t in zip(before, after) for n in s['bones'] for j in range(4) for k in range(4))
        assert error < 1e-6, (path.name, error)
        if path.stem == 'FirstPlayerCapsule_Crawl_Forward':
            render(rig, body, 'after')
            scene.frame_start, scene.frame_end = start, end
            scene.frame_set(10)
            bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Crawl_Torso_Stable.blend'))
        dest = staged / path.name
        hold.export_current(rig, meshes, dest, start, end)
        vertex_count = len(new_mesh.vertices)
        checked_rig, checked_body, _ = hold.import_fbx(dest)
        checked_start, checked_end = (int(x) for x in checked_rig.animation_data.action.frame_range)
        assert checked_end - checked_start == end - start, path.name
        # FBX exporter preserves this triangulated mesh's vertex ordering.
        assert len(checked_body.data.vertices) == vertex_count, path.name
        checked = snapshot(checked_rig, checked_body, range(checked_start, checked_end + 1), core)
        roundtrip_error = max(abs(s['bones'][n][j][k] - t['bones'][n][j][k])
                              for s, t in zip(before, checked) for n in s['bones'] for j in range(4) for k in range(4))
        assert roundtrip_error < 1e-4, (path.name, roundtrip_error)
        assert all(math.isfinite(c) for s in checked for v in s['vertices'] for c in v), path.name
        source_closure = max((p - q).length for p, q in zip(before[0]['vertices'], before[-1]['vertices']))
        closure = max((p - q).length for p, q in zip(checked[0]['vertices'], checked[-1]['vertices']))
        if source_closure < .002:
            assert closure < .002, (path.name, closure)
        if 'Crawl' in path.name:
            assert drift(checked) < .0001, (path.name, drift(checked))
        report.append({'file': path.name, 'start': start, 'end': end, 'changed_vertices': changed,
                       'core_vertices': len(core), 'before_core_drift': drift(before),
                       'after_core_drift': drift(after), 'bone_error': error,
                       'roundtrip_bone_error': roundtrip_error, 'roundtrip_core_drift': drift(checked),
                       'loop_closure': closure})
        (OUT / 'report.json').write_text(json.dumps(report, indent=2))
        print('CHECKED', report[-1], flush=True)
    if args.install:
        backup = OUT / 'backup'
        backup.mkdir(exist_ok=True)
        for path in files:
            if not (backup / path.name).exists():
                shutil.copy2(path, backup / path.name)
            shutil.copy2(staged / path.name, path)
        print('INSTALLED', len(files), flush=True)


if __name__ == '__main__':
    main()
