"""Smooth the arm/chest junction in the standing pose, preserving skin weights."""
import argparse
import importlib.util
import json
import shutil
from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp/smooth-first-shoulders'
SOURCE = ROOT / 'Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx'
spec = importlib.util.spec_from_file_location('arms', ROOT / 'Tools/plump_first_arms.py')
arms = importlib.util.module_from_spec(spec)
spec.loader.exec_module(arms)
smooth = arms.hold.smooth


def refine(body, rig):
    scene = bpy.context.scene
    scene.frame_set(1)
    bpy.context.view_layer.update()
    keys = body.data.shape_keys
    key_action = keys.animation_data.action if keys.animation_data else None
    keys.animation_data_clear()
    for key in keys.key_blocks:
        key.value = 0
    bpy.context.view_layer.update()
    to_rig = rig.matrix_world.inverted() @ body.matrix_world
    to_body = to_rig.inverted()
    transforms = {g.index: to_body @ rig.pose.bones[g.name].matrix @ rig.data.bones[g.name].matrix_local.inverted() @ to_rig
                  for g in body.vertex_groups if g.name in rig.pose.bones}
    skins = [arms.hold.skin_from_names(v, body, transforms, None) for v in body.data.vertices]
    base = [p.co.copy() for p in keys.key_blocks[0].data]
    posed = [skin @ p if skin else p.copy() for skin, p in zip(skins, base)]
    masks = []
    for v, p in zip(body.data.vertices, base):
        co = to_rig @ p
        # Fade into the chest surface, without touching the abdomen or hips.
        shoulder = smooth((abs(co.x) - .22) / .16) * smooth((co.y - .76) / .16)
        shoulder *= 1 - smooth((co.y - 1.24) / .15)
        arm_w = sum(g.weight for g in v.groups if body.vertex_groups[g.group].name.startswith(('UpperArm.', 'Arm.', 'Hand.', 'Finger_')))
        arm = smooth((arm_w - .10) / .65) * smooth((abs(co.x) - .36) / .12)
        masks.append(max(shoulder, arm))
    neighbors = [set() for _ in base]
    for e in body.data.edges:
        a, b = e.vertices
        neighbors[a].add(b)
        neighbors[b].add(a)
    result = [p.copy() for p in posed]
    # Smooth small surface ridges in pose space, with a reverse pass to retain
    # the round volume instead of shrinking the arms into narrow sticks.
    for _ in range(32):
        for amount in (.5, -.48):
            result = [p.lerp(sum((result[j] for j in neighbors[i]), Vector()) / len(neighbors[i]), amount * masks[i])
                      if neighbors[i] and masks[i] else p for i, p in enumerate(result)]
    # Fill concave creases at the arm root without thinning the surrounding
    # convex shoulder. Evaluate normals on the standing, deformed surface.
    surface = bpy.data.meshes.new('PosedShoulderNormals')
    surface.from_pydata([tuple(p) for p in posed], [], [tuple(p.vertices) for p in body.data.polygons])
    surface.update()
    normals = [v.normal.copy() for v in surface.vertices]
    bpy.data.meshes.remove(surface)
    root_masks = []
    for p in base:
        co = to_rig @ p
        root_masks.append(smooth((abs(co.x) - .24) / .14) * (1 - smooth((abs(co.x) - .58) / .17))
                          * smooth((co.y - .83) / .14) * (1 - smooth((co.y - 1.20) / .13)))
    for _ in range(90):
        updated = []
        for i, p in enumerate(result):
            if not neighbors[i] or root_masks[i] == 0:
                updated.append(p)
                continue
            lap = sum((result[j] for j in neighbors[i]), Vector()) / len(neighbors[i]) - p
            normal_amount = lap.dot(normals[i])
            # Positive curvature fills the cavity. Mild tangential smoothing
            # spreads the fill into the adjacent surface without a hard seam.
            change = lap - normals[i] * min(0, normal_amount)
            updated.append(p + change * (.5 * root_masks[i]))
        result = updated
    for i, (p, old, mask) in enumerate(zip(result, posed, masks)):
        if not mask:
            continue
        # Close the upper-arm clearance with a broad, soft inward displacement.
        world = body.matrix_world @ old
        upper = smooth((world.z - .67) / .23) * (1 - smooth((world.z - 1.16) / .14))
        arm_w = sum(g.weight for g in body.data.vertices[i].groups
                    if body.vertex_groups[g.group].name.startswith(('UpperArm.', 'Arm.')))
        inward = .008 * upper * smooth((arm_w - .15) / .7)
        shift = Vector((-inward if world.x > 0 else inward, 0, 0))
        result[i] += body.matrix_world.inverted().to_3x3() @ shift
    deltas = [skin.inverted_safe().to_3x3() @ (new - old) if skin else Vector()
              for skin, new, old in zip(skins, result, posed)]
    for v, p, delta in zip(body.data.vertices, base, deltas):
        v.co = p + delta
    for key in keys.key_blocks:
        for point, delta in zip(key.data, deltas):
            point.co += delta
    if key_action:
        keys.animation_data_create().action = key_action
        keys.animation_data.action_slot = key_action.slots[0]
    return deltas


def render_crawl(idle_path, label):
    # Apply the crawl action to the shared Idle body, as Unity does.
    crawl_rig, crawl_body, _ = arms.hold.import_fbx(SOURCE.parent / 'FirstPlayerCapsule_Crawl_Forward.fbx')
    action = crawl_rig.animation_data.action
    shape_action = crawl_body.data.shape_keys.animation_data.action
    action.use_fake_user = True
    shape_action.use_fake_user = True
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(idle_path))
    rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    body = bpy.data.objects['Body']
    rig.animation_data.action = action
    rig.animation_data.action_slot = action.slots[0]
    body.data.shape_keys.animation_data.action = shape_action
    body.data.shape_keys.animation_data.action_slot = shape_action.slots[0]
    stable_spec = importlib.util.spec_from_file_location('stable', ROOT / 'Tools/stabilize_crawl_torso.py')
    stable = importlib.util.module_from_spec(stable_spec)
    stable_spec.loader.exec_module(stable)
    stable.OUT = OUT
    stable.render(rig, body, label)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    OUT.mkdir(parents=True, exist_ok=True)
    rig, body, meshes = arms.hold.import_fbx(SOURCE)
    start, end = (round(x) for x in rig.animation_data.action.frame_range)
    arms.OUT = OUT
    arms.render(rig, 'before')
    original_weights = [[(g.group, g.weight) for g in v.groups] for v in body.data.vertices]
    deltas = refine(body, rig)
    print('REBUILD_NORMALS', body.data.has_custom_normals, flush=True)
    for polygon in body.data.polygons:
        polygon.use_smooth = True
    for edge in body.data.edges:
        edge.use_edge_sharp = False
    # Old FBX custom normals describe the previous arm surface. Zero normals
    # request the automatic smooth normals of the reshaped geometry.
    body.data.normals_split_custom_set([(0, 0, 0)] * len(body.data.loops))
    body.data.update()
    arms.render(rig, 'after')
    expected = [v.co.copy() for v in body.data.vertices]
    dest = OUT / SOURCE.name
    arms.hold.export_current(rig, meshes, dest, start, end)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'FirstCharacter_SmoothShoulders.blend'))
    rig, body, _ = arms.hold.import_fbx(dest)
    error = max((v.co - p).length for v, p in zip(body.data.vertices, expected))
    assert error < 1e-5, error
    assert all([(g.group, g.weight) for g in v.groups] == w for v, w in zip(body.data.vertices, original_weights))
    render_crawl(dest, 'crawl')
    report = {'changed_vertices': sum(d.length > 1e-8 for d in deltas),
              'max_delta': max(d.length for d in deltas), 'roundtrip_error': error, 'weights_unchanged': True}
    (OUT / 'report.json').write_text(json.dumps(report, indent=2))
    print(report, flush=True)
    if args.install:
        backup = OUT / 'before.fbx'
        if not backup.exists():
            shutil.copy2(SOURCE, backup)
        shutil.copy2(dest, SOURCE)
        print('INSTALLED', SOURCE, flush=True)


if __name__ == '__main__':
    main()
