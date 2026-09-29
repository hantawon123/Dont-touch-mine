"""Overlay Punch/Hit upper body onto walk / run / crouch bases.

blender --background --python Tools/make_first_punch_locomotion.py -- FIRST_DIR
"""
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

FIRST = Path(sys.argv[sys.argv.index("--") + 1])

WEIGHTS = {
    "Shoulder.R": 1.0,
    "UpperArm.R": 1.0,
    "Arm.R": 1.0,
    "Hand.R": 1.0,
    "Finger_M1.R": 1.0,
    "Finger_M2.R": 1.0,
    "Finger_T1.R": 1.0,
    "Finger_T2.R": 1.0,
    "Spine": 0.45,
    "Neck": 0.35,
    "Head": 0.30,
    "Shoulder.L": 0.35,
    "UpperArm.L": 0.45,
    "Arm.L": 0.45,
    "Hand.L": 0.35,
}

# Hit uses more upper-body weight (reaction). Punch keeps stronger right arm.
PUNCH_WEIGHTS = {
    **WEIGHTS,
    "Shoulder.L": 0.15,
    "UpperArm.L": 0.20,
    "Arm.L": 0.20,
    "Hand.L": 0.15,
    "Spine": 0.35,
    "Neck": 0.25,
    "Head": 0.15,
}

BASES = (
    ("Walk", "Walk_Forward"),
    ("Run", "Run_Forward"),
    ("Crouch", "Crouch_Idle"),
    ("Crouch_Walk", "Crouch_Walk_Forward"),
)

JOBS = (
    ("Punch", 24, PUNCH_WEIGHTS),
    ("Hit", 30, WEIGHTS),
)


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def sample_clip(path):
    rig = load(path)
    action = rig.animation_data.action
    first, last = int(action.frame_range[0]), int(action.frame_range[1])
    body = bpy.data.objects.get("Body")
    keys = body.data.shape_keys if body is not None else None
    frames = []
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
        shapes = {}
        if keys is not None:
            shapes = {key.name: key.value for key in keys.key_blocks[1:]}
        frames.append((pose, shapes))
    return frames


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(
        a_loc.lerp(b_loc, t),
        a_rot.slerp(b_rot, t),
        a_scale.lerp(b_scale, t),
    )


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def export_fbx(rig, path, last):
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
    print("EXPORTED", path.name, last)


for source_name, last_frame, weights in JOBS:
    source = sample_clip(FIRST / f"FirstPlayerCapsule_{source_name}.fbx")
    source_len = len(source)
    for suffix, base_name in BASES:
        # Skip re-baking punch variants that already exist unless Hit.
        clip_name = f"{source_name}_{suffix}"
        base = sample_clip(FIRST / f"FirstPlayerCapsule_{base_name}.fbx")
        base_len = len(base)
        rig = load(FIRST / "FirstPlayerCapsule_Idle.fbx")
        body = bpy.data.objects["Body"]
        keys = body.data.shape_keys
        action = bpy.data.actions.new(clip_name)
        action.use_fake_user = True
        rig.animation_data_create().action = action
        if action.slots:
            rig.animation_data.action_slot = action.slots[0]
        keys.animation_data_clear()

        ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
        for frame in range(last_frame + 1):
            source_pose, _ = source[min(frame, source_len - 1)]
            base_pose, base_shapes = base[frame % base_len]
            for bone in ordered:
                name = bone.name
                weight = weights.get(name, 0.0)
                if weight > 0.0 and name in source_pose:
                    bone.matrix_basis = mix(base_pose[name], source_pose[name], weight)
                else:
                    bone.matrix_basis = base_pose[name]
            bpy.context.view_layer.update()
            key_pose(rig, frame)
            for key in keys.key_blocks[1:]:
                key.value = base_shapes.get(key.name, 0.0)
                key.keyframe_insert("value", frame=frame)

        export_fbx(rig, FIRST / f"FirstPlayerCapsule_{clip_name}.fbx", last_frame)

print("COMBAT_LOCOMOTION_DONE")
