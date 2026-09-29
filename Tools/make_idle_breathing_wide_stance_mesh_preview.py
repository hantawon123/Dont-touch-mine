from pathlib import Path

import bpy

SOURCE_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_preview.blend")
OUT_BLEND = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_wide_stance_preview.blend")
OUT_FBX = Path("/private/tmp/PurplePlayerCapsule_Idle_Breathing_wide_stance_preview.fbx")
SIDE_OFFSET = 0.12

LEFT_GROUPS = {
    "UpperLeg.L": 0.45,
    "Leg.L": 0.8,
    "Foot.L": 1.0,
    "FootToe1.L": 1.0,
}
RIGHT_GROUPS = {
    "UpperLeg.R": 0.45,
    "Leg.R": 0.8,
    "Foot.R": 1.0,
    "FootToe1.R": 1.0,
}


def group_weight(obj, vertex, names):
    weights = []
    group_by_index = {group.index: group.name for group in obj.vertex_groups}
    for item in vertex.groups:
        name = group_by_index.get(item.group)
        if name in names:
            weights.append(item.weight * names[name])
    return min(max(weights, default=0.0), 1.0)


def widen_body_mesh():
    obj = bpy.data.objects.get("Body")
    if obj is None or obj.type != "MESH":
        raise RuntimeError("Body mesh missing")

    moved = 0
    for vertex in obj.data.vertices:
        left = group_weight(obj, vertex, LEFT_GROUPS)
        right = group_weight(obj, vertex, RIGHT_GROUPS)
        influence = left - right
        if abs(influence) < 0.001:
            continue
        vertex.co.x += SIDE_OFFSET * influence
        moved += 1
    obj.data.update()
    print("MOVED_VERTICES", moved, "SIDE_OFFSET", SIDE_OFFSET)


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
    widen_body_mesh()
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 60
    bpy.context.scene.render.fps = 30
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT_BLEND))
    export_selected_fbx(OUT_FBX)
    print("SAVED_BLEND", OUT_BLEND)
    print("EXPORTED_FBX", OUT_FBX)


if __name__ == "__main__":
    main()
