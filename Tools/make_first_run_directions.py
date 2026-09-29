"""Build Run_Back / Run_Left / Run_Right from Run_SideArms.

Facing stays forward. Foot swing is remapped onto back / left / right,
matching Walk_* and Crouch_Walk_* (Left = +localX). Knees stay forward
on lateral runs so the legs do not bow outward.

Blender --background --python Tools/make_first_run_directions.py -- RUN.fbx OUT_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


run_path, out_dir = sys.argv[sys.argv.index("--") + 1 :]
out_dir = Path(out_dir)
out_dir.mkdir(parents=True, exist_ok=True)


def aim(bone, direction):
    current = bone.tail - bone.head
    if current.length < 1e-8 or direction.length < 1e-8:
        return
    rotation = current.normalized().rotation_difference(direction.normalized())
    bone.matrix = Matrix.LocRotScale(
        bone.head,
        rotation @ bone.matrix.to_quaternion(),
        bone.matrix.to_scale(),
    )
    bpy.context.view_layer.update()


def project_pole(hint, axis):
    pole = hint - axis * hint.dot(axis)
    if pole.length < 1e-6:
        fallback = Vector((0.0, 0.0, 1.0))
        pole = fallback - axis * fallback.dot(axis)
    return pole.normalized() if pole.length > 1e-6 else Vector((0.0, 0.0, 1.0))


def solve_leg(rig, side, target, rotation, pole_hint, flatten_x=False):
    thigh = rig.pose.bones[f"UpperLeg.{side}"]
    shin = rig.pose.bones[f"Leg.{side}"]
    hip = thigh.head.copy()
    reach = target - hip
    if reach.length < 1e-8:
        return
    axis = reach.normalized()
    upper_len = thigh.bone.length
    lower_len = shin.bone.length
    length = min(upper_len + lower_len - 0.001, max(abs(upper_len - lower_len) + 0.001, reach.length))
    along = (upper_len * upper_len - lower_len * lower_len + length * length) / (2.0 * length)
    pole = pole_hint.copy()
    if flatten_x:
        pole.x = 0.0
    pole = project_pole(pole, axis)
    if flatten_x:
        pole.x = 0.0
        pole = project_pole(pole, axis)
    knee = hip + axis * along + pole * math.sqrt(max(0.0, upper_len * upper_len - along * along))
    aim(thigh, knee - hip)
    aim(shin, target - shin.head)
    foot = rig.pose.bones[f"Foot.{side}"]
    foot.matrix = Matrix.LocRotScale(foot.head, rotation, Vector((1.0, 1.0, 1.0)))
    bpy.context.view_layer.update()


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def export_fbx(rig, path, first, last):
    scene = bpy.context.scene
    scene.frame_start = first
    scene.frame_end = last
    scene.frame_set(first)
    bpy.ops.object.select_all(action="DESELECT")
    meshes = [obj for obj in scene.objects if obj.type == "MESH"]
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=False,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1,
        bake_anim_simplify_factor=0,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        armature_nodetype="NULL",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )
    print("EXPORTED", path, first, last)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=run_path, use_anim=True, ignore_leaf_bones=False)
scene = bpy.context.scene
rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
action = rig.animation_data.action
first, last = (int(action.frame_range[0]), int(action.frame_range[1]))
ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
samples = {}
for frame in range(first, last + 1):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    samples[frame] = {
        "basis": {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones},
        "hips": {side: rig.pose.bones[f"UpperLeg.{side}"].head.copy() for side in ("L", "R")},
        "knees": {side: rig.pose.bones[f"Leg.{side}"].head.copy() for side in ("L", "R")},
        "feet": {
            side: (
                rig.pose.bones[f"Foot.{side}"].head.copy(),
                rig.pose.bones[f"Foot.{side}"].matrix.to_quaternion().copy(),
            )
            for side in ("L", "R")
        },
    }

homes = {}
for side in ("L", "R"):
    xs = [samples[frame]["feet"][side][0].x for frame in samples]
    zs = [samples[frame]["feet"][side][0].z for frame in samples]
    homes[side] = Vector((sum(xs) / len(xs), 0.0, sum(zs) / len(zs)))


def original_knee_pole(frame, side):
    hip = samples[frame]["hips"][side]
    knee = samples[frame]["knees"][side]
    foot = samples[frame]["feet"][side][0]
    axis = foot - hip
    if axis.length < 1e-6:
        return Vector((0.0, 0.0, 1.0))
    axis.normalize()
    offset = knee - hip - axis * axis.dot(knee - hip)
    return offset if offset.length > 1e-6 else Vector((0.0, 0.0, 1.0))


def keep_lane(x, home_x):
    inner = abs(home_x) * 0.75
    if home_x >= 0.0:
        return max(x, inner)
    return min(x, -inner)


def apply_direction(frame, direction):
    lateral = abs(direction.x) > 0.5
    for bone in ordered:
        bone.matrix_basis = samples[frame]["basis"][bone.name]
    bpy.context.view_layer.update()
    for side in ("L", "R"):
        head, rotation = samples[frame]["feet"][side]
        home = homes[side]
        swing = head.z - home.z
        if lateral:
            target = Vector((keep_lane(home.x + direction.x * swing * 0.5, home.x), head.y, home.z))
        else:
            target = Vector((home.x, head.y, home.z)) + direction * swing
        pole = Vector((0.0, 0.0, 1.0)) if lateral else original_knee_pole(frame, side)
        solve_leg(rig, side, target, rotation, pole, flatten_x=lateral)


clips = (
    ("Run_Back", Vector((0.0, 0.0, -1.0))),
    ("Run_Left", Vector((1.0, 0.0, 0.0))),
    ("Run_Right", Vector((-1.0, 0.0, 0.0))),
)

for name, direction in clips:
    clip = bpy.data.actions.new(name)
    clip.use_fake_user = True
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    for frame in range(first, last + 1):
        apply_direction(frame, direction)
        key_pose(rig, frame)
    xs = {side: [] for side in ("L", "R")}
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        for side in ("L", "R"):
            xs[side].append(rig.pose.bones[f"Foot.{side}"].head.x)
    print(
        name,
        "Foot.L",
        f"{min(xs['L']):+.3f}..{max(xs['L']):+.3f}",
        "Foot.R",
        f"{min(xs['R']):+.3f}..{max(xs['R']):+.3f}",
    )
    export_fbx(rig, out_dir / f"FirstPlayerCapsule_{name}.fbx", first, last)

print("RUN_DIRECTION_CLIPS", out_dir, first, last)
