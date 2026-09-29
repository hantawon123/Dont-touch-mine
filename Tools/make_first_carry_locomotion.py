"""Overlay Carry_Idle's right-hand hold onto walk / run clips.

The legs, hips, spine, and left arm stay on the locomotion clip.
Shoulder.R and its children use Carry_Idle so the object stay in the
right hand.

Blender --background --python Tools/make_first_carry_locomotion.py -- CARRY.fbx OUT_DIR
"""
import sys
from pathlib import Path

import bpy


carry_path, out_dir = sys.argv[sys.argv.index("--") + 1 :]
out_dir = Path(out_dir)
out_dir.mkdir(parents=True, exist_ok=True)

CLIPS = (
    ("FirstPlayerCapsule_Walk_Forward.fbx", "Carry_Walk_Forward"),
    ("FirstPlayerCapsule_Walk_Back.fbx", "Carry_Walk_Back"),
    ("FirstPlayerCapsule_Walk_Left.fbx", "Carry_Walk_Left"),
    ("FirstPlayerCapsule_Walk_Right.fbx", "Carry_Walk_Right"),
    ("FirstPlayerCapsule_Run_Forward.fbx", "Carry_Run_Forward"),
    ("FirstPlayerCapsule_Run_Back.fbx", "Carry_Run_Back"),
    ("FirstPlayerCapsule_Run_Left.fbx", "Carry_Run_Left"),
    ("FirstPlayerCapsule_Run_Right.fbx", "Carry_Run_Right"),
)


def descendants(bone):
    names = [bone.name]
    for child in bone.children:
        names.extend(descendants(child))
    return names


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
bpy.ops.import_scene.fbx(filepath=carry_path, use_anim=True, ignore_leaf_bones=False)
carry_rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
carry_bones = descendants(carry_rig.pose.bones["Shoulder.R"])
frame = int(carry_rig.animation_data.action.frame_range[0])
bpy.context.scene.frame_set(frame)
bpy.context.view_layer.update()
held = {name: carry_rig.pose.bones[name].matrix_basis.copy() for name in carry_bones}
print("CARRY_BONES", carry_bones)

for source_name, clip_name in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(out_dir / source_name), use_anim=True, ignore_leaf_bones=False)
    scene = bpy.context.scene
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    action = rig.animation_data.action
    first, last = (int(action.frame_range[0]), int(action.frame_range[1]))
    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    samples = {}
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[frame] = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}

    clip = bpy.data.actions.new(clip_name)
    clip.use_fake_user = True
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    for frame in range(first, last + 1):
        for bone in ordered:
            bone.matrix_basis = samples[frame][bone.name]
        bpy.context.view_layer.update()
        for name in carry_bones:
            if name in rig.pose.bones:
                rig.pose.bones[name].matrix_basis = held[name]
        bpy.context.view_layer.update()
        key_pose(rig, frame)
    export_fbx(rig, out_dir / f"FirstPlayerCapsule_{clip_name}.fbx", first, last)

print("CARRY_LOCOMOTION_CLIPS", out_dir)
