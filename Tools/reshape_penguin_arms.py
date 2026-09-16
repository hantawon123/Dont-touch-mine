"""Reshape First character arms into blunt penguin flippers.

blender --background --python Tools/reshape_penguin_arms.py -- INPUT.blend
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
ARM_BONES = (
    "Shoulder.{side}",
    "UpperArm.{side}",
    "Arm.{side}",
    "Hand.{side}",
    "Finger_M1.{side}",
    "Finger_M2.{side}",
    "Finger_T1.{side}",
    "Finger_T2.{side}",
)
TORSO_BONES = ("Hips", "Spine", "Neck", "Head", "UpperLeg.L", "UpperLeg.R")


def world_bone(rig, name):
    pose = rig.pose.bones[name]
    return rig.matrix_world @ pose.head, rig.matrix_world @ pose.tail


def arm_polyline(rig, side):
    points = [world_bone(rig, f"UpperArm.{side}")[0]]
    points.append(world_bone(rig, f"Arm.{side}")[0])
    points.append(world_bone(rig, f"Hand.{side}")[0])
    points.append(world_bone(rig, f"Hand.{side}")[1])
    return points


def project_polyline(point, points):
    best_dist = 1e9
    closest = points[0]
    tangent = (points[1] - points[0]).normalized()
    t_hit = 0.0
    total = sum((points[i + 1] - points[i]).length for i in range(len(points) - 1))
    acc = 0.0
    for i in range(len(points) - 1):
        start, end = points[i], points[i + 1]
        span = end - start
        length = span.length
        if length < 1e-8:
            continue
        u = max(0.0, min(1.0, (point - start).dot(span) / span.length_squared))
        candidate = start + span * u
        dist = (point - candidate).length
        if dist < best_dist:
            best_dist = dist
            closest = candidate
            tangent = span.normalized()
            t_hit = (acc + u * length) / total
        acc += length
    return closest, t_hit, tangent, best_dist


def radius_pair(t):
    wide = 0.050 + 0.155 * (t ** 1.05)
    thin = 0.078 + 0.070 * (t ** 1.12)
    if t > 0.55:
        club = ((t - 0.55) / 0.45) ** 0.85
        wide += 0.070 * club
        thin += 0.045 * club
    return wide, thin


def group_weight(vertex, index_by_name, names):
    total = 0.0
    for name in names:
        index = index_by_name.get(name)
        if index is None:
            continue
        for group in vertex.groups:
            if group.group == index:
                total += group.weight
    return total


def reshape_side(body, rig, side):
    mesh = body.data
    inverse = body.matrix_world.inverted()
    names = [bone.format(side=side) for bone in ARM_BONES]
    index_by_name = {
        group.name: group.index
        for group in body.vertex_groups
        if group.name in names or group.name in TORSO_BONES
    }
    line = arm_polyline(rig, side)
    world = body.matrix_world
    moved = []
    for vertex in mesh.vertices:
        arm_w = group_weight(vertex, index_by_name, names)
        if arm_w < 0.18:
            continue
        torso_w = group_weight(vertex, index_by_name, TORSO_BONES)
        point = world @ vertex.co
        closest, t, tangent, dist = project_polyline(point, line)
        if dist > 0.42 and arm_w < 0.55:
            continue
        influence = max(0.0, min(1.0, (arm_w - 0.18) / 0.62))
        influence *= 1.0 - 0.75 * max(0.0, min(1.0, torso_w / 0.55))
        if t < 0.12:
            influence *= t / 0.12
        if influence < 0.04:
            continue

        along = tangent
        major = Vector((0.0, 1.0, 0.0))
        major = (major - along * major.dot(along))
        if major.length < 1e-6:
            major = Vector((0.0, 0.0, 1.0))
            major = (major - along * major.dot(along))
        major.normalize()
        minor = along.cross(major)
        if minor.length < 1e-6:
            continue
        minor.normalize()

        offset = point - closest
        along_amt = offset.dot(along)
        wide_amt = offset.dot(major)
        thin_amt = offset.dot(minor)
        wide_r, thin_r = radius_pair(t)
        scale = hypot_safe(wide_amt / wide_r, thin_amt / thin_r)
        if scale < 1e-6:
            new_wide, new_thin = 0.0, 0.0
        else:
            new_wide = wide_r * (wide_amt / wide_r) / scale
            new_thin = thin_r * (thin_amt / thin_r) / scale
        along_keep = 0.12 + 0.18 * (1.0 - t)
        if t > 0.58:
            along_keep *= 1.0 - 0.92 * ((t - 0.58) / 0.42)
        new_point = (
            closest
            + major * new_wide
            + minor * new_thin
            + along * (along_amt * along_keep)
        )
        vertex.co = inverse @ point.lerp(new_point, influence)
        moved.append(vertex.index)
    return moved


def hypot_safe(a, b):
    return math.sqrt(a * a + b * b)


def smooth_indices(mesh, indices, iterations=10):
    index_set = set(indices)
    neighbors = {i: set() for i in index_set}
    for edge in mesh.edges:
        a, b = edge.vertices
        if a in index_set and b in index_set:
            neighbors[a].add(b)
            neighbors[b].add(a)
    coords = [vertex.co.copy() for vertex in mesh.vertices]
    for _ in range(iterations):
        nxt = coords[:]
        for i in index_set:
            linked = neighbors[i]
            if not linked:
                continue
            acc = Vector((0.0, 0.0, 0.0))
            for other in linked:
                acc += coords[other]
            nxt[i] = coords[i].lerp(acc / len(linked), 0.55)
        coords = nxt
    for i in index_set:
        mesh.vertices[i].co = coords[i]


def render_side(path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 900
    scene.render.filepath = str(path)
    scene.render.film_transparent = False
    if scene.world:
        scene.world.use_nodes = False
        scene.world.color = (0.16, 0.15, 0.15)
    bpy.ops.object.camera_add(location=(0.55, -2.55, 0.95))
    cam = bpy.context.object
    target = Vector((0.42, 0.0, 0.88))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.lens = 50
    scene.camera = cam
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


def reshape(blend_path: Path):
    bpy.ops.wm.open_mainfile(filepath=str(blend_path), use_scripts=False)
    rig = bpy.data.objects["DGN_Armature"]
    body = bpy.data.objects["Body"]
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    bpy.context.view_layer.objects.active = body
    body.select_set(True)

    moved = reshape_side(body, rig, "L") + reshape_side(body, rig, "R")
    body.data.update()
    smooth_indices(body.data, moved, 8)
    body.data.update()

    out_png = ROOT / "docs" / "design" / "character" / "modeling" / "penguin-arm-side.png"
    out_png.parent.mkdir(parents=True, exist_ok=True)
    render_side(out_png)
    bpy.ops.wm.save_mainfile(filepath=str(blend_path), compress=False)
    print("RESHAPED", len(set(moved)), "arm verts", "SAVED", blend_path)
    print("RENDER", out_png)


if __name__ == "__main__":
    reshape(Path(sys.argv[sys.argv.index("--") + 1]))
