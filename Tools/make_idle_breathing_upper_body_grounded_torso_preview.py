import math
from pathlib import Path

import bpy

SOURCE_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_wide_stance_preview.blend")
OUT_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_grounded_torso_preview.blend")
OUT_FBX = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_grounded_torso_preview.fbx")
FRAME_START = 1
FRAME_END = 60
TORSO_SCALE = 0.035

LOCKED_BONES = {
    "Hips",
    "UpperLeg.L",
    "Leg.L",
    "Foot.L",
    "FootToe1.L",
    "UpperLeg.R",
    "Leg.R",
    "Foot.R",
    "FootToe1.R",
}


def pose_snapshot(armature):
    return {
        bone.name: (
            bone.location.copy(),
            bone.rotation_quaternion.copy(),
            bone.scale.copy(),
        )
        for bone in armature.pose.bones
    }


def restore_bone(bone, values):
    loc, rot, scale = values
    bone.location = loc.copy()
    bone.rotation_quaternion = rot.copy()
    bone.scale = scale.copy()


def breath_amount(frame):
    return math.sin(math.pi * (frame - FRAME_START) / (FRAME_END - FRAME_START))


def export_selected_fbx(path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        obj.select_set(obj.type in {"ARMATURE", "MESH"})
    bpy.ops.export_scene.fbx(
        filepath=str(path),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        armature_nodetype="NULL",
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        mesh_smooth_type="FACE",
    )


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND))
    armature = bpy.data.objects.get("DGN_Armature")
    if armature is None:
        raise RuntimeError("DGN_Armature missing")

    scene = bpy.context.scene
    scene.frame_start = FRAME_START
    scene.frame_end = FRAME_END
    scene.render.fps = 30

    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"

    scene.frame_set(FRAME_START)
    bpy.context.view_layer.update()
    locked_pose = pose_snapshot(armature)
    armature_location = armature.location.copy()

    sampled = {}
    for frame in range(FRAME_START, FRAME_END + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        armature.location = armature_location.copy()
        for bone in armature.pose.bones:
            if bone.name in LOCKED_BONES:
                restore_bone(bone, locked_pose[bone.name])

        spine = armature.pose.bones.get("Spine")
        if spine is not None:
            amount = breath_amount(frame)
            base_scale = locked_pose["Spine"][2]
            spine.scale.x = base_scale.x * (1.0 + TORSO_SCALE * amount)
            spine.scale.y = base_scale.y * (1.0 + TORSO_SCALE * amount)
            spine.scale.z = base_scale.z * (1.0 + TORSO_SCALE * 0.45 * amount)

        bpy.context.view_layer.update()
        sampled[frame] = pose_snapshot(armature)

    armature.animation_data_clear()
    action = bpy.data.actions.new("Idle_Breathing_Grounded_Torso")
    armature.animation_data_create().action = action

    for frame, bones in sampled.items():
        scene.frame_set(frame)
        armature.location = armature_location.copy()
        armature.keyframe_insert("location", frame=frame)
        for bone in armature.pose.bones:
            restore_bone(bone, bones[bone.name])
            bone.keyframe_insert("location", frame=frame, group=bone.name)
            bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
            bone.keyframe_insert("scale", frame=frame, group=bone.name)

    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    export_selected_fbx(OUT_FBX)
    print("SAVED_BLEND", OUT_BLEND)
    print("EXPORTED_FBX", OUT_FBX)
    print("TORSO_SCALE", TORSO_SCALE)
    print("LOCKED_BONES", sorted(LOCKED_BONES))


if __name__ == "__main__":
    main()
