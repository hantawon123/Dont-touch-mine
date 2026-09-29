from pathlib import Path

import bpy

TARGET_BLEND = Path("/Users/barleymilk/Downloads/PurplePlayerCapsule.blend")
SOURCE_BLEND = Path("/Users/barleymilk/Projects/ssafy/DTM_블렌더/blender_work/움직임최종본/PlayerCapsule_Idle_Breathing_2s.blend")
OUT_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_preview.blend")
OUT_FBX = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_preview.fbx")
ACTION_NAME = "Idle_Breathing_2s"
FRAME_START = 1
FRAME_END = 60


def assign_action(armature, action):
    anim = armature.animation_data_create()
    anim.action = action
    if hasattr(action, "slots") and action.slots:
        slot = None
        for candidate in action.slots:
            if getattr(candidate, "identifier", "") == "OBDGN_Armature":
                slot = candidate
                break
        anim.action_slot = slot or action.slots[0]


def main():
    bpy.ops.wm.open_mainfile(filepath=str(TARGET_BLEND))
    target = bpy.data.objects.get("DGN_Armature")
    if target is None:
        raise RuntimeError("DGN_Armature missing in target blend")

    with bpy.data.libraries.load(str(SOURCE_BLEND), link=False) as (data_from, data_to):
        if ACTION_NAME not in data_from.actions:
            raise RuntimeError(f"{ACTION_NAME} missing. Available: {list(data_from.actions)}")
        data_to.actions = [ACTION_NAME]

    action = bpy.data.actions[ACTION_NAME]
    action.name = "Idle_Breathing"
    assign_action(target, action)

    scene = bpy.context.scene
    scene.frame_start = FRAME_START
    scene.frame_end = FRAME_END
    scene.render.fps = 30

    for obj in bpy.data.objects:
        obj.select_set(obj.type in {"MESH", "ARMATURE"})
    bpy.context.view_layer.objects.active = target

    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    bpy.ops.export_scene.fbx(
        filepath=str(OUT_FBX),
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

    print("SAVED_BLEND", OUT_BLEND)
    print("EXPORTED_FBX", OUT_FBX)
    print("ACTION_RANGE", tuple(action.frame_range), "FCURVES", len(getattr(action, "fcurves", [])))


if __name__ == "__main__":
    main()
