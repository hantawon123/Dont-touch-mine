"""Bake Idle_Breathing onto the Unity PlayerCapsule rest pose and export mesh+armature."""
from pathlib import Path

import bpy
from mathutils import Matrix


UNITY_FBX = Path("/Users/barleymilk/Projects/ssafy/S15P21D205/Assets/_Game/Content/Models/PlayerCapsule.fbx")
OUT_FBX = Path(
    "/Users/barleymilk/Projects/ssafy/S15P21D205/Assets/Scenes/CharacterTest/BakedUnity/PlayerCapsule_Idle_Breathing.fbx"
)
ACTION_NAME = "Idle_Breathing_2s"
CLIP_NAME = "Idle_Breathing"
FRAME_START = 1
FRAME_END = 60


def find_idle_blend() -> Path:
    root = Path("/Users/barleymilk/Projects/ssafy")
    matches = []
    for path in root.rglob("PlayerCapsule_Idle_Breathing_2s.blend"):
        if "blender_work" in path.parts:
            matches.append(path)
    if not matches:
        raise RuntimeError("PlayerCapsule_Idle_Breathing_2s.blend not found")
    matches.sort(key=lambda item: (item.parent.name[:1].isdigit(), str(item)))
    return matches[0]


def assign_action(arm, action):
    ad = arm.animation_data_create()
    ad.action = action
    slot = None
    for candidate in action.slots:
        ident = getattr(candidate, "identifier", "")
        display = getattr(candidate, "name_display", "")
        if ident == "OBDGN_Armature" or display == "DGN_Armature":
            slot = candidate
            break
    if slot is None and action.slots:
        slot = action.slots[0]
    if slot is not None:
        ad.action_slot = slot


def parent_first(arm):
    remaining = list(arm.pose.bones)
    ordered = []
    seen = set()
    while remaining:
        progressed = False
        nxt = []
        for bone in remaining:
            if bone.parent is None or bone.parent.name in seen:
                ordered.append(bone)
                seen.add(bone.name)
                progressed = True
            else:
                nxt.append(bone)
        if not progressed:
            ordered.extend(nxt)
            break
        remaining = nxt
    return ordered


def copy_world_pose(src, tgt):
    src_bones = {bone.name: bone for bone in src.pose.bones}
    src_world = src.matrix_world
    tgt_inv = tgt.matrix_world.inverted()
    for bone in parent_first(tgt):
        other = src_bones.get(bone.name)
        if other is None:
            continue
        bone.matrix = tgt_inv @ src_world @ other.matrix


def count_action_curves(action):
    total = 0
    varying = 0
    if action is None:
        return 0, 0
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            bags = list(getattr(strip, "channelbags", []))
            if not bags:
                for slot in action.slots:
                    bag = strip.channelbag(slot)
                    if bag is not None:
                        bags.append(bag)
            for bag in bags:
                for fc in getattr(bag, "fcurves", []):
                    total += 1
                    vals = [kp.co.y for kp in fc.keyframe_points]
                    if vals and max(vals) - min(vals) > 1e-5:
                        varying += 1
    return total, varying


def world_head(arm, bone_name):
    bone = arm.data.bones.get(bone_name)
    if bone is None:
        return None
    return arm.matrix_world @ bone.head_local


def print_armature(label, arm):
    loc, rot, scl = arm.matrix_world.decompose()
    hips = world_head(arm, "Hips")
    print(
        f"{label}: name={arm.name} loc={list(loc)} scale={list(scl)} "
        f"bones={len(arm.data.bones)} hips={list(hips) if hips else None}"
    )


def collect_skinned(arm):
    meshes = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        if obj.parent == arm:
            meshes.append(obj)
            continue
        for mod in obj.modifiers:
            if mod.type == "ARMATURE" and mod.object == arm:
                meshes.append(obj)
                break
    return meshes


def export_character(arm, meshes, path: Path):
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.ops.object.select_all(action="DESELECT")
    arm.hide_set(False)
    arm.hide_viewport = False
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    for bone in arm.data.bones:
        bone.use_deform = True
    for mesh in meshes:
        mesh.hide_set(False)
        mesh.hide_viewport = False
        mesh.select_set(True)
    path.parent.mkdir(parents=True, exist_ok=True)
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
    blend_path = find_idle_blend()
    print(f"SOURCE {blend_path}")
    print(f"TARGET {UNITY_FBX}")
    print(f"OUT {OUT_FBX}")

    bpy.ops.wm.open_mainfile(filepath=str(blend_path))

    src = bpy.data.objects.get("DGN_Armature")
    if src is None:
        raise RuntimeError("source DGN_Armature missing")

    action = bpy.data.actions.get(ACTION_NAME)
    if action is None:
        names = [item.name for item in bpy.data.actions]
        raise RuntimeError(f"action {ACTION_NAME} missing: {names}")

    assign_action(src, action)
    print_armature("SRC", src)

    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(UNITY_FBX), automatic_bone_orientation=False)
    imported = [obj for obj in bpy.data.objects if obj not in before]
    target = next((obj for obj in imported if obj.type == "ARMATURE"), None)
    if target is None:
        raise RuntimeError("Unity PlayerCapsule armature not found")

    target.name = "Unity_Target"
    if target.data:
        target.data.name = "Unity_Target"
    print_armature("TGT", target)

    src_names = {bone.name for bone in src.data.bones}
    tgt_names = {bone.name for bone in target.data.bones}
    missing = sorted(src_names - tgt_names)
    extra = sorted(tgt_names - src_names)
    print(f"MATCH {len(src_names & tgt_names)} missing_on_unity={missing} extra={extra}")

    meshes = collect_skinned(target)
    print(f"MESHES {[mesh.name for mesh in meshes]}")

    target.animation_data_clear()
    baked = bpy.data.actions.new(name=CLIP_NAME)
    baked.use_fake_user = True
    assign_action(target, baked)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = 30
    scene.frame_start = FRAME_START
    scene.frame_end = FRAME_END

    for bone in target.pose.bones:
        bone.rotation_mode = "QUATERNION"

    hips_y = []
    for frame in range(FRAME_START, FRAME_END + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        copy_world_pose(src, target)
        bpy.context.view_layer.update()
        hips = target.pose.bones.get("Hips")
        if hips is not None:
            world = target.matrix_world @ hips.matrix
            hips_y.append(world.to_translation().z)
        for bone in target.pose.bones:
            bone.keyframe_insert("location", frame=frame, group=bone.name)
            bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
            bone.keyframe_insert("scale", frame=frame, group=bone.name)

    if hips_y:
        print(f"HIPS_Z min={min(hips_y):.4f} max={max(hips_y):.4f}")

    keep = {target, *meshes}
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)

    target.name = "DGN_Armature"
    if target.data:
        target.data.name = "DGN_Armature"
    for mesh in meshes:
        if mesh.name.endswith(".001"):
            mesh.name = mesh.name[:-4]
        if mesh.data and mesh.data.name.endswith(".001"):
            mesh.data.name = mesh.data.name[:-4]
    print(f"EXPORT_MESHES {[mesh.name for mesh in meshes]}")

    baked_action = target.animation_data.action if target.animation_data else None
    total, varying = count_action_curves(baked_action)
    print(f"BAKED {CLIP_NAME} curves={total} varying={varying}")
    if total == 0:
        raise RuntimeError("no fcurves after bake")

    export_character(target, meshes, OUT_FBX)
    print(f"EXPORTED {OUT_FBX} size={OUT_FBX.stat().st_size}")
    print("DONE")


if __name__ == "__main__":
    main()
