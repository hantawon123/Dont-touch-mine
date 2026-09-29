"""Bake posture-matched Pickup / PutDown for crouch and prone.

Standing Pickup_Low keeps the idle body and blends arms into Carry_Idle.
These clips do the same from Crouch_Idle / Prone_Idle into the matching
Carry pose so the character does not stand up mid-interaction.

blender --background --python Tools/make_first_posture_pickup.py -- FIRST_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
LAST = 48
BLEND_END = 32

ARM_WEIGHTS = {
    "Shoulder.R": 0.55,
    "UpperArm.R": 0.85,
    "Arm.R": 0.85,
    "Hand.R": 0.80,
    "Finger_M1.R": 0.60,
    "Finger_M2.R": 0.60,
    "Finger_T1.R": 0.60,
    "Finger_T2.R": 0.60,
    "Shoulder.L": 0.12,
    "UpperArm.L": 0.25,
    "Arm.L": 0.25,
    "Hand.L": 0.25,
    "Finger_M1.L": 0.30,
    "Finger_M2.L": 0.30,
    "Finger_T1.L": 0.30,
    "Finger_T2.L": 0.30,
}

JOBS = (
    ("Pickup_Crouch", "Crouch_Idle", "Carry_Crouch_Idle", True),
    ("PutDown_Crouch", "Crouch_Idle", "Carry_Crouch_Idle", False),
    ("Pickup_Prone", "Prone_Idle", "Carry_Prone_Idle", True),
    ("PutDown_Prone", "Prone_Idle", "Carry_Prone_Idle", False),
)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def pose_at(rig, frame):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


def sample_first(path):
    rig = load(path)
    first = int(rig.animation_data.action.frame_range[0])
    return pose_at(rig, first)


def shapes_at(frame_path):
    load(frame_path)
    body = bpy.data.objects.get("Body")
    if body is None or body.data.shape_keys is None:
        return {}
    first = int(next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
                .animation_data.action.frame_range[0])
    bpy.context.scene.frame_set(first)
    bpy.context.view_layer.update()
    return {key.name: key.value for key in body.data.shape_keys.key_blocks[1:]}


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def export(rig, path, last):
    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = 0
    scene.frame_end = last
    scene.frame_set(0)
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=False,
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
        mesh_smooth_type="FACE",
        path_mode="COPY",
        embed_textures=True,
    )


for name, base_name, carry_name, pickup in JOBS:
    base = sample_first(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
    base_shapes = shapes_at(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
    carry = sample_first(FIRST / f"FirstPlayerCapsule_{carry_name}.fbx")
    carry_shapes = shapes_at(FIRST / f"FirstPlayerCapsule_{carry_name}.fbx")

    rig = load(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
    body = bpy.data.objects["Body"]
    keys = body.data.shape_keys
    rig.animation_data.action = bpy.data.actions.new(name)
    if keys is not None:
        keys.animation_data_clear()
        keys.animation_data_create()
        keys.animation_data.action = bpy.data.actions.new(f"{name}_shapes")

    arm_weights = ARM_WEIGHTS
    if "Prone" in name:
        # Keep the planted left hand; only the free right arm reaches.
        arm_weights = {
            name: weight for name, weight in ARM_WEIGHTS.items() if name.endswith(".R")
        }

    for frame in range(LAST + 1):
        if pickup:
            progress = smooth(min(1.0, frame / BLEND_END))
        else:
            progress = 1.0 - smooth(min(1.0, frame / BLEND_END))

        for bone in rig.pose.bones:
            bone.rotation_mode = "QUATERNION"
            weight = arm_weights.get(bone.name, 0.0) * progress
            if weight > 0.0:
                bone.matrix_basis = mix(base[bone.name], carry[bone.name], weight)
            else:
                # Keep the posture body; only arms travel toward / from carry.
                bone.matrix_basis = base[bone.name]
        bpy.context.view_layer.update()
        for bone in rig.pose.bones:
            for prop in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(prop, frame=frame)

        if keys is not None:
            for key in keys.key_blocks[1:]:
                a = base_shapes.get(key.name, 0.0)
                b = carry_shapes.get(key.name, 0.0)
                key.value = a + (b - a) * progress
                key.keyframe_insert("value", frame=frame)

    export(rig, FIRST / f"FirstPlayerCapsule_{name}.fbx", LAST)
    print("BAKED", name, LAST)
