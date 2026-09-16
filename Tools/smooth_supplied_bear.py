"""Inspect/smooth the supplied Blender character while retaining its arm design."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Temp/supplied-bear-smooth'
DELIVERY = Path('C:/Users/SSAFY/.codex/visualizations/2026/09/15/01a0a3bb-bf50-70a3-b969-beb1f5f21188/supplied-bear-smooth')


def smooth_mesh(obj, iterations, boundary_pin=False):
    mesh = obj.data
    coords = np.array([v.co[:] for v in mesh.vertices], dtype=np.float64)
    before = coords.copy()
    neighbors = [[] for _ in coords]
    edge_faces = {}
    for poly in mesh.polygons:
        for edge in poly.edge_keys:
            edge_faces[edge] = edge_faces.get(edge, 0) + 1
    for edge in mesh.edges:
        a, b = edge.vertices
        neighbors[a].append(b)
        neighbors[b].append(a)
    mobility = np.ones(len(coords))
    src = np.array([i for i, n in enumerate(neighbors) for _ in n])
    dst = np.array([j for n in neighbors for j in n])
    degree = np.maximum(np.bincount(src, minlength=len(coords)), 1)
    if boundary_pin:
        for (a, b), count in edge_faces.items():
            if count == 1:
                mobility[a] = mobility[b] = 0
    # Alternating low-pass steps remove short ripples without ordinary
    # Laplacian smoothing's progressive shrinkage of arms and fingers.
    for _ in range(iterations):
        for strength in (.45, -.47):
            mean = np.stack([np.bincount(src, weights=coords[dst, axis], minlength=len(coords))
                             for axis in range(3)], axis=1) / degree[:, None]
            coords += strength * mobility[:, None] * (mean - coords)
    for vertex, co in zip(mesh.vertices, coords):
        vertex.co = co
    mesh.update()
    mesh.update_tag()
    obj.update_tag(refresh={'DATA'})
    bpy.context.view_layer.update()
    return {'vertices': len(coords), 'max_displacement': float(np.linalg.norm(coords-before, axis=1).max()),
            'mean_displacement': float(np.linalg.norm(coords-before, axis=1).mean()),
            'bounds_before': [before.min(axis=0).tolist(), before.max(axis=0).tolist()],
            'bounds_after': [coords.min(axis=0).tolist(), coords.max(axis=0).tolist()]}


def rebuild_surface():
    body = bpy.data.objects['Body']
    rig = bpy.data.objects['DGN_Armature']
    old_pose = rig.data.pose_position
    rig.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    source_mesh = body.data
    source_mesh.calc_loop_triangles()
    triangles = [tuple(t.vertices) for t in source_mesh.loop_triangles]
    positions = [v.co.copy() for v in source_mesh.vertices]
    weights = [{g.group:g.weight for g in v.groups} for v in source_mesh.vertices]
    uv_layers = {layer.name: [v.uv.copy() for v in layer.data] for layer in source_mesh.uv_layers}
    triangle_loops = [tuple(t.loops) for t in source_mesh.loop_triangles]
    tree = BVHTree.FromPolygons(positions, triangles, all_triangles=True)
    deps = bpy.context.evaluated_depsgraph_get()
    new_mesh = bpy.data.meshes.new_from_object(body.evaluated_get(deps), depsgraph=deps)
    body.modifiers.clear()
    body.data = new_mesh
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    remesh = body.modifiers.new('Even Surface', 'REMESH')
    remesh.mode = 'VOXEL'
    remesh.voxel_size = .012
    remesh.use_smooth_shade = True
    bpy.ops.object.modifier_apply(modifier=remesh.name)
    stats = smooth_mesh(body, 80)
    # Transfer the original deformation weights by closest triangle, keeping
    # the same armature, groups, actions and bind transform.
    nearest = []
    for vertex in body.data.vertices:
        location, normal, index, distance = tree.find_nearest(vertex.co)
        tri = triangles[index]
        bary = barycentric_transform(location, *[positions[i] for i in tri],
                                     Vector((1,0,0)), Vector((0,1,0)), Vector((0,0,1)))
        nearest.append((index, bary))
        result = {}
        for i, influence in zip(tri, bary):
            for group, weight in weights[i].items():
                result[group] = result.get(group,0) + max(0, influence) * weight
        total = sum(result.values())
        for group, weight in result.items():
            if weight > 1e-7:
                body.vertex_groups[group].add([vertex.index], weight / total, 'REPLACE')
    for name, old_uvs in uv_layers.items():
        layer = body.data.uv_layers.new(name=name)
        for loop in body.data.loops:
            tri_index, bary = nearest[loop.vertex_index]
            loops = triangle_loops[tri_index]
            layer.data[loop.index].uv = sum((old_uvs[i] * w for i,w in zip(loops,bary)), Vector((0,0)))
    arm = body.modifiers.new('DGN_Armature', 'ARMATURE')
    arm.object = rig
    add_deformation_smoothing(body)
    sub = body.modifiers.new('Subdivision', 'SUBSURF')
    sub.levels = sub.render_levels = 1
    rig.data.pose_position = old_pose
    return stats


def add_deformation_smoothing(body):
    mod = body.modifiers.new('Gentle Joint Smoothing', 'CORRECTIVE_SMOOTH')
    mod.factor = .6
    mod.iterations = 16
    mod.smooth_type = 'LENGTH_WEIGHTED'
    mod.use_only_smooth = False
    mod.rest_source = 'ORCO'
    # Corrective smoothing restores the rest-shape detail after smoothing
    # the posed surface, reducing skin-weight creases during the walk.
    return mod


def rig_snapshot():
    rig = bpy.data.objects['DGN_Armature']
    scene = bpy.context.scene
    frame = scene.frame_current
    pose = rig.data.pose_position
    rig.data.pose_position = 'POSE'
    frames = {}
    for f in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(f)
        frames[f] = {b.name: [list(row) for row in b.matrix] for b in rig.pose.bones}
    scene.frame_set(frame)
    rig.data.pose_position = pose
    return {'rest': {b.name: [list(row) for row in b.matrix_local] for b in rig.data.bones},
            'frames': frames,
            'actions': [(a.name, list(a.frame_range)) for a in bpy.data.actions]}


def validate():
    body = bpy.data.objects['Body']
    rig = bpy.data.objects['DGN_Armature']
    scene = bpy.context.scene
    frame, pose = scene.frame_current, rig.data.pose_position
    groups = {g.index for g in body.vertex_groups if g.name in rig.data.bones}
    totals = [sum(g.weight for g in v.groups if g.group in groups) for v in body.data.vertices]
    assert min(totals) > .9999 and max(totals) < 1.0001, (min(totals), max(totals))
    edge_use = {}
    for polygon in body.data.polygons:
        for key in polygon.edge_keys:
            edge_use[key] = edge_use.get(key, 0) + 1
    assert all(count == 2 for count in edge_use.values()), 'Open or nonmanifold body'
    adjacent = [[] for _ in body.data.vertices]
    for a,b in edge_use:
        adjacent[a].append(b)
        adjacent[b].append(a)
    remaining = set(range(len(adjacent)))
    components = 0
    while remaining:
        stack = [remaining.pop()]
        components += 1
        while stack:
            for neighbor in adjacent[stack.pop()]:
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    stack.append(neighbor)
    assert components == 1, f'Disconnected body: {components}'
    rig.data.pose_position = 'POSE'
    sampled = 0
    for f in range(scene.frame_start, scene.frame_end + 1):
        scene.frame_set(f)
        mesh = body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
        co = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
        mesh.vertices.foreach_get('co', co)
        assert np.isfinite(co).all()
        assert np.max(np.abs(co)) < 5, 'Exploded surface'
        body.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
        sampled += 1
    scene.frame_set(frame)
    rig.data.pose_position = pose
    return {'vertices': len(body.data.vertices), 'faces': len(body.data.polygons),
            'closed_body': True, 'connected_components': components,
            'weight_min': min(totals), 'weight_max': max(totals),
            'animation_frames_checked': sampled, 'materials': [m.name for m in body.data.materials],
            'uv_layers': [l.name for l in body.data.uv_layers]}


def render(label, walk=False):
    scene = bpy.context.scene
    for obj in scene.objects:
        if obj.type == 'ARMATURE':
            obj.data.pose_position = 'POSE' if walk else 'REST'
        obj.hide_render = obj.type != 'MESH'
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (.65, .67, .7)
    cam = bpy.data.objects.new('Smooth_QA', bpy.data.cameras.new('Smooth_QA'))
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 2.7
    views = [('front', (0, -6, 1),1), ('side', (6, 0, 1),1)]
    if walk:
        views = [(f'walk-{f:02}', (2.2, -6, 1.8),f) for f in (1,7,13,19)]
    for name, location, frame in views:
        scene.frame_set(frame)
        cam.location = location
        cam.rotation_euler = (Vector((0, 0, 1)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(OUT / (label + '-' + name + '.png'))
        bpy.ops.render.render(write_still=True)


def finish_saved():
    target = DELIVERY / 'BasicPlayerCapsule_WithWalk_Bear_Ears_Smooth.blend'
    bpy.ops.wm.open_mainfile(filepath=str(target), use_scripts=False)
    rig_before = rig_snapshot()
    body = bpy.data.objects['Body']
    rig = bpy.data.objects['DGN_Armature']
    old_pose = rig.data.pose_position
    rig.data.pose_position = 'REST'
    def evaluated_coords():
        bpy.context.view_layer.update()
        obj = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = obj.to_mesh()
        coords = np.empty(len(mesh.vertices)*3, dtype=np.float32)
        mesh.vertices.foreach_get('co',coords)
        obj.to_mesh_clear()
        return coords
    before = evaluated_coords()
    add_deformation_smoothing(body)
    body.modifiers.move(len(body.modifiers)-1,1)
    rest_error = float(np.max(np.abs(before-evaluated_coords())))
    assert rest_error < .0001, rest_error
    rig.data.pose_position = old_pose
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(target),check_existing=False)
    bpy.ops.wm.open_mainfile(filepath=str(target), use_scripts=False)
    assert rig_before == rig_snapshot()
    result = json.loads((DELIVERY/'validation.json').read_text())
    result['reopened_validation'] = validate()
    result['deformation_smoothing'] = {'factor':.6, 'iterations':16,
                                       'rest_shape_max_error':rest_error,
                                       'rig_and_actions_unchanged':True}
    (DELIVERY/'validation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print('FINAL_VALIDATED',json.dumps(result['reopened_validation']),flush=True)


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    if args[0] == 'finish':
        finish_saved()
        return
    bpy.ops.wm.open_mainfile(filepath=args[0], use_scripts=False)
    source_hash = hashlib.sha256(Path(args[0]).read_bytes()).hexdigest()
    original_rig = rig_snapshot()
    OUT.mkdir(parents=True, exist_ok=True)
    report = []
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        info = {'name': obj.name, 'vertices': len(obj.data.vertices),
                'smooth_faces': sum(p.use_smooth for p in obj.data.polygons),
                'custom_normals': obj.data.has_custom_normals,
                'modifiers': []}
        for mod in obj.modifiers:
            values = {'name': mod.name, 'type': mod.type, 'viewport': mod.show_viewport, 'render': mod.show_render}
            for attr in ('levels', 'render_levels', 'factor', 'iterations', 'smooth_type', 'rest_source', 'use_only_smooth', 'use_pin_boundary', 'thickness', 'offset'):
                if hasattr(mod, attr):
                    values[attr] = getattr(mod, attr)
            info['modifiers'].append(values)
        info['shape_keys'] = [k.name for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []
        report.append(info)
    print(json.dumps(report, indent=2), flush=True)
    (OUT / 'inspection.json').write_text(json.dumps(report, indent=2))
    iterations = int(args[1]) if len(args) > 1 else 0
    if 'rebuild' in args:
        changes = {'Body': rebuild_surface(),
                   'Hood': smooth_mesh(bpy.data.objects['Hood'], 16, boundary_pin=True)}
        print('REBUILD', json.dumps(changes), flush=True)
    if iterations:
        changes = {'Body': smooth_mesh(bpy.data.objects['Body'], iterations),
                   'Hood': smooth_mesh(bpy.data.objects['Hood'], 8, boundary_pin=True)}
        print(json.dumps(changes), flush=True)
        (OUT / f'changes-{iterations}.json').write_text(json.dumps(changes, indent=2))
    label = f'smooth-{iterations}'
    if 'rebuild' in args:
        label = 'final'
    if 'no-post' in args:
        bpy.data.objects['Body'].modifiers['Surface Smooth'].show_render = False
        bpy.data.objects['Body'].modifiers['Surface Smooth'].show_viewport = False
        label += '-no-post'
    if 'save' in args:
        DELIVERY.mkdir(parents=True, exist_ok=True)
        result = validate()
        assert original_rig == rig_snapshot(), 'Rig or animation changed'
        result['original_rig_and_animation_unchanged'] = True
        result['source_sha256'] = source_hash
        result['surface_changes'] = changes
        target = DELIVERY / 'BasicPlayerCapsule_WithWalk_Bear_Ears_Smooth.blend'
        bpy.ops.wm.save_as_mainfile(filepath=str(target), check_existing=False)
        bpy.ops.wm.open_mainfile(filepath=str(target), use_scripts=False)
        assert original_rig == rig_snapshot(), 'Saved rig or animation changed'
        result['reopened_validation'] = validate()
        assert hashlib.sha256(Path(args[0]).read_bytes()).hexdigest() == source_hash
        result['original_file_unchanged'] = True
        (DELIVERY / 'validation.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    render(label)
    if 'save' in args:
        render(label, walk=True)


if __name__ == '__main__':
    main()
