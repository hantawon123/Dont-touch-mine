"""Smooth and reshape bumpy cat ears on the Hood mesh.

blender --background --python Tools/polish_cat_hood_ears.py -- INPUT.blend
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

BASE_Z = 1.90
TIP_Z = 2.03
BASE_X = 0.12
TIP_X = 0.36


def clamp(value, low, high):
    return max(low, min(high, value))


def hypot2(a, b):
    return math.sqrt(a * a + b * b)


def ear_target(world_point, side):
    sign = 1.0 if side == "L" else -1.0
    x, y, z = world_point.x, world_point.y, world_point.z
    t = clamp((z - BASE_Z) / (TIP_Z - BASE_Z), 0.0, 1.0)

    center_x = sign * (BASE_X + (TIP_X - BASE_X) * (t ** 0.75))
    center_z = BASE_Z + (TIP_Z - BASE_Z) * (t ** 0.88) - 0.012 * t

    rel_x = x - center_x
    rel_y = y
    rel_z = z - center_z

    width_x = 0.088 * (1.0 - 0.35 * t) + 0.014
    width_y = 0.055 * (1.0 - 0.25 * t)
    depth = 0.042 * (1.0 - 0.35 * t)

    nx = rel_x / width_x if width_x > 1e-6 else 0.0
    ny = rel_y / width_y if width_y > 1e-6 else 0.0
    radial = hypot2(nx, ny)
    if radial > 1e-6:
        nx /= radial
        ny /= radial
    radial = min(radial, 1.0)

    if t > 0.78:
        blunt = 1.0 - 0.55 * ((t - 0.78) / 0.22)
        radial *= blunt

    front_cup = -sign * depth * max(0.0, 1.0 - radial) * (0.35 + 0.65 * t)
    return Vector(
        (
            center_x + nx * width_x * radial,
            ny * width_y * radial * 0.85,
            center_z + rel_z * 0.08 + front_cup,
        )
    )


def collect_ear_indices(hood, side):
    mesh = hood.data
    world = hood.matrix_world
    indices = []
    for vertex in mesh.vertices:
        point = world @ vertex.co
        if point.z < BASE_Z - 0.02:
            continue
        if side == "L" and point.x < 0.08:
            continue
        if side == "R" and point.x > -0.08:
            continue
        if abs(point.x) < 0.10:
            continue
        indices.append(vertex.index)
    return indices


def reshape_ear(hood, side):
    mesh = hood.data
    world = hood.matrix_world
    inverse = world.inverted()
    indices = set(collect_ear_indices(hood, side))
    if not indices:
        return 0

    for index in indices:
        vertex = mesh.vertices[index]
        point = world @ vertex.co
        t = clamp((point.z - BASE_Z) / (TIP_Z - BASE_Z), 0.0, 1.0)
        influence = clamp((point.z - (BASE_Z - 0.02)) / 0.14, 0.0, 1.0)
        influence *= clamp((abs(point.x) - 0.06) / 0.08, 0.0, 1.0)
        if point.z > 1.94 and abs(point.x) > 0.14:
            influence = 1.0
        elif t > 0.35:
            influence = min(1.0, influence + 0.45)
        target = ear_target(point, side)
        vertex.co = inverse @ point.lerp(target, influence)
    return len(indices)


def bmesh_smooth(hood, indices, repeat=12, factor=0.65):
    mesh = hood.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    selected = [bm.verts[i] for i in indices if i < len(bm.verts)]
    for _ in range(repeat):
        bmesh.ops.smooth_vert(
            bm,
            verts=selected,
            factor=factor,
        )
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def smooth_indices(mesh, indices, iterations=14, factor=0.58):
    index_set = set(indices)
    neighbors = {index: set() for index in index_set}
    for edge in mesh.edges:
        a, b = edge.vertices
        if a in index_set and b in index_set:
            neighbors[a].add(b)
            neighbors[b].add(a)
    coords = [vertex.co.copy() for vertex in mesh.vertices]
    for _ in range(iterations):
        nxt = coords[:]
        for index in index_set:
            linked = neighbors[index]
            if not linked:
                continue
            avg = Vector((0.0, 0.0, 0.0))
            for other in linked:
                avg += coords[other]
            avg /= len(linked)
            nxt[index] = coords[index].lerp(avg, factor)
        coords = nxt
    for index in index_set:
        mesh.vertices[index].co = coords[index]


def render_preview(hood, output_path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.render.filepath = str(output_path)
    if scene.world:
        scene.world.color = (0.14, 0.13, 0.14)
    bpy.ops.object.camera_add(location=(0.0, -2.2, 1.72))
    camera = bpy.context.object
    target = Vector((0.0, 0.0, 1.78))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 55
    scene.camera = camera
    hood.hide_set(False)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def delete_bumpy_ear_faces(hood):
    mesh = hood.data
    world = hood.matrix_world
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    doomed = []
    for face in bm.faces:
        for vert in face.verts:
            point = world @ vert.co
            if point.z > 1.915 and abs(point.x) > 0.085:
                doomed.append(face)
                break
    if doomed:
        bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return len(doomed)


def add_clean_ear(side):
    sign = 1.0 if side == "L" else -1.0
    bpy.ops.mesh.primitive_cone_add(
        vertices=24,
        radius1=0.11,
        radius2=0.03,
        depth=0.18,
        location=(sign * 0.30, -0.01, 1.96),
        rotation=(math.radians(18), 0.0, sign * math.radians(-22)),
    )
    ear = bpy.context.object
    ear.name = f"HoodEar_{side}"
    bpy.ops.object.shade_smooth()
    bevel = ear.modifiers.new("Soft", "BEVEL")
    bevel.width = 0.012
    bevel.segments = 2
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    return ear


def rebuild_ears(hood):
    deleted = delete_bumpy_ear_faces(hood)
    ears = [add_clean_ear("L"), add_clean_ear("R")]
    bpy.context.view_layer.objects.active = hood
    hood.select_set(True)
    for ear in ears:
        ear.select_set(True)
    bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = "Hood"
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.remove_doubles(threshold=0.008)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    joined.data.update()
    return deleted


def polish(blend_path: Path):
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), use_scripts=False)
    hood = bpy.data.objects.get("Hood")
    if hood is None:
        raise RuntimeError("Hood object not found")

    bpy.context.view_layer.objects.active = hood
    hood.select_set(True)

    deleted = rebuild_ears(hood)
    indices = collect_ear_indices(hood, "L") + collect_ear_indices(hood, "R")
    left = reshape_ear(hood, "L")
    right = reshape_ear(hood, "R")
    hood.data.update()
    bmesh_smooth(hood, indices, repeat=10, factor=0.55)
    hood.data.update()

    preview = blend_path.with_name(blend_path.stem + "_ears.png")
    render_preview(hood, preview)
    bpy.ops.wm.save_mainfile(filepath=str(blend_path), compress=False)
    print("POLISHED deleted_faces", deleted, "ears L", left, "R", right, "SAVED", blend_path)
    print("PREVIEW", preview)


if __name__ == "__main__":
    polish(Path(sys.argv[sys.argv.index("--") + 1]))
