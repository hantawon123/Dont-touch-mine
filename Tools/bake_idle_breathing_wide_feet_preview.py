import math
from pathlib import Path

import bpy
from mathutils import Quaternion, Vector

SOURCE_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_preview.blend")
OUT_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_wide_feet_preview.blend")
OUT_FBX = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_wide_feet_preview.fbx")
FRAME_START = 1
FRAME_END = 60
SPREAD_DEGREES = 24.0


def apply_leg_spread(armature):
    axis = Vector((0, 1, 0))
    for side, sign in (("L", 1), ("R", -1)):
        bone = armature.pose.bones.get(f"UpperLeg.{side}")
        if bone is None:
            continue
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion(axis, math.radians(SPREAD_DEGREES) * sign) @ bone.rotation_quaternion


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

    sampled = {}
    for frame in range(FRAME_START, FRAME_END + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        apply_leg_spread(armature)
        bpy.context.view_layer.update()
        sampled[frame] = {
            bone.name: (
                bone.location.copy(),
                bone.rotation_quaternion.copy(),
                bone.scale.copy(),
            )
            for bone in armature.pose.bones
        }

    armature.animation_data_clear()
    action = bpy.data.actions.new("Idle_Breathing_WideFeet")
    armature.animation_data_create().action = action

    for bone in armature.pose.bones:
        bone.rotation_mode = "QUATERNION"

    for frame, bones in sampled.items():
        scene.frame_set(frame)
        for bone in armature.pose.bones:
            loc, rot, scale = bones[bone.name]
            bone.location = loc
            bone.rotation_quaternion = rot
            bone.scale = scale
            bone.keyframe_insert("location", frame=frame, group=bone.name)
            bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
            bone.keyframe_insert("scale", frame=frame, group=bone.name)

    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    export_selected_fbx(OUT_FBX)
    print("SAVED_BLEND", OUT_BLEND)
    print("EXPORTED_FBX", OUT_FBX)
    print("SPREAD_DEGREES", SPREAD_DEGREES)


if __name__ == "__main__":
    main()
