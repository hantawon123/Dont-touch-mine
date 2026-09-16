"""Round distal thumb geometry in the authored blend; keep topology and skin data.

Run with Blender --background --python Tools/refine_smooth_bear_thumb_source.py.
Outputs are staged for comparison before installation into the project.
"""
import bpy
import hashlib
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Tools'))
from hold_crawl_torso import FBX_KW

SOURCE = ROOT / 'source/blender/characters/SmoothBear/SmoothBear.blend'
OUT = ROOT / 'artifacts/smooth-bear-unity/thumb-source-round'
OUT.mkdir(parents=True, exist_ok=True)


def digest(value):
    return hashlib.sha256(repr(value).encode()).hexdigest()


def invariant(body, rig):
    return {
        'weights': digest([[(g.group, g.weight) for g in v.groups] for v in body.data.vertices]),
        'groups': [g.name for g in body.vertex_groups],
        'topology': digest([tuple(p.vertices) for p in body.data.polygons]),
        'bones': digest([(b.name, b.parent.name if b.parent else None, b.use_deform,
                          [tuple(row) for row in b.matrix_local]) for b in rig.data.bones]),
        'uvs': digest([[tuple(p.uv) for p in uv.data] for uv in body.data.uv_layers]),
        'modifiers': [(m.name, m.type) for m in body.modifiers],
        'key_names': [k.name for k in body.data.shape_keys.key_blocks],
    }


def render_closeup(body, phase):
    scene = bpy.context.scene
    rig = bpy.data.objects['DGN_Armature']
    previous_pose_position = rig.data.pose_position
    rig.data.pose_position = 'REST'
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (.83, .67, .19)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.background_type = 'WORLD'
    scene.world.color = (.14, .14, .14)
    group = body.vertex_groups['Finger_T2.L'].index
    vertices = [v for v in body.data.vertices if any(g.group == group and g.weight > .5 for g in v.groups)]
    bpy.context.view_layer.update()
    evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    center = evaluated.matrix_world @ (sum((evaluated.data.vertices[v.index].co for v in vertices), Vector()) / len(vertices))
    camera = bpy.data.objects.new('ThumbReviewCamera', bpy.data.cameras.new('ThumbReviewCamera'))
    scene.collection.objects.link(camera)
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = .17
    scene.camera = camera
    for view, offset in [('front', Vector((.25, -1, .1))), ('side', Vector((1, -.12, .12)))]:
        camera.location = center + offset
        camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(OUT / f'{phase}-{view}.png')
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    rig.data.pose_position = previous_pose_position


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE), use_scripts=False)
    body = bpy.data.objects['Body']
    rig = bpy.data.objects['DGN_Armature']
    before = invariant(body, rig)
    keys = body.data.shape_keys.key_blocks
    original = [p.co.copy() for p in keys[0].data]
    deltas = {k.name: [p.co - q.co for p, q in zip(k.data, keys[0].data)] for k in keys}
    render_closeup(body, 'before')
    neighbours = [set() for _ in original]
    for edge in body.data.edges:
        a, b = edge.vertices
        neighbours[a].add(b)
        neighbours[b].add(a)
    result = [p.copy() for p in original]
    regions = {}
    for side in ['L', 'R']:
        name = 'Finger_T2.' + side
        bone = rig.data.bones[name]
        to_body = body.matrix_world.inverted() @ rig.matrix_world
        axis = (to_body.to_3x3() @ (bone.tail_local - bone.head_local)).normalized()
        group = body.vertex_groups[name].index
        region = [v.index for v in body.data.vertices
                  if any(g.group == group and g.weight >= .5 for g in v.groups)]
        distances = {i: original[i].dot(axis) for i in region}
        start, tip = min(distances.values()), max(distances.values())
        # Preserve the thumb base; progressively round only the distal cap.
        threshold = start + (tip - start) * .52
        factors = {i: max(0, min(1, (distances[i] - threshold) / ((tip - threshold) * .55))) for i in region}
        ring = sorted(region, key=lambda i: abs(distances[i] - threshold))[:12]
        center = sum((original[i] - axis * distances[i] for i in ring), Vector()) / len(ring)
        radius = max((original[i] - axis * distances[i] - center).length for i in ring)
        for i in region:
            if distances[i] <= threshold:
                continue
            radial = original[i] - axis * distances[i] - center
            r = min(1.0, radial.length / radius)
            rounded_distance = threshold + radius * math.sqrt(max(0.0, 1.0 - r * r))
            blend = min(1.0, (distances[i] - threshold) / .006)
            blend = blend * blend * (3 - 2 * blend)
            result[i] = original[i] + axis * ((rounded_distance - distances[i]) * blend)
        for _ in range(2):
            previous = [p.copy() for p in result]
            for i in region:
                t = factors[i]
                if t == 0 or not neighbours[i]:
                    continue
                smooth = t * t * (3 - 2 * t)
                average = sum((previous[j] for j in neighbours[i]), Vector()) / len(neighbours[i])
                result[i] = previous[i].lerp(average, .45 * smooth)
        regions[name] = {'axis': list(axis), 'selected': len(region), 'cap_start': threshold, 'tip': tip}

    changed = [i for i in range(len(result)) if (result[i] - original[i]).length > 1e-9]
    assert changed, 'No thumb vertices were changed'
    for i in changed:
        shift = result[i] - original[i]
        for key in keys:
            key.data[i].co += shift
        body.data.vertices[i].co = result[i]
    body.data.update()
    assert invariant(body, rig) == before, 'Rig, topology, weights, UVs or key names changed'
    max_delta_error = max((p.co - q.co - deltas[k.name][i]).length
                          for k in keys for i, (p, q) in enumerate(zip(k.data, keys[0].data)))
    assert max_delta_error < 1e-7, max_delta_error
    for i in range(len(original)):
        if i not in set(changed):
            assert (keys[0].data[i].co - original[i]).length < 1e-9
    render_closeup(body, 'after')
    # Reopen the authoring file so review cameras, world and render settings do
    # not become part of the delivered source. Reapply only the verified deltas.
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE), use_scripts=False)
    body = bpy.data.objects['Body']
    rig = bpy.data.objects['DGN_Armature']
    for i in changed:
        shift = result[i] - original[i]
        for key in body.data.shape_keys.key_blocks:
            key.data[i].co += shift
        body.data.vertices[i].co = result[i]
    body.data.update()
    assert invariant(body, rig) == before
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'SmoothBear.blend'), check_existing=False)

    # Preserve the saved export pose: resetting it changes the working FBX's
    # evaluated skin mesh and shifts all head attachments by 0.111 units.
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH' or obj == rig:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    kwargs = FBX_KW.copy()
    kwargs.update(bake_anim=False, embed_textures=False, path_mode='AUTO')
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'SmoothBear.fbx'), **kwargs)
    report = {'source': str(SOURCE), 'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
              'invariants': before, 'changed_vertex_count': len(changed), 'changed_vertices': changed,
              'maximum_shift': max((result[i] - original[i]).length for i in changed),
              'shape_delta_error': max_delta_error, 'regions': regions}
    (OUT / 'report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print('THUMB_SOURCE_ROUND_COMPLETE', len(changed), report['maximum_shift'], flush=True)


if __name__ == '__main__':
    main()
