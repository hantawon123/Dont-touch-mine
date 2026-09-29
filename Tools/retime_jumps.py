"""Resample Jump_Cute and Carry_Jump to a shorter clip.

Blender --background --python Tools/retime_jumps.py -- FIRST_DIR LAST
"""
import sys
from pathlib import Path

import bpy

root = Path(sys.argv[sys.argv.index("--") + 1])
last = int(sys.argv[sys.argv.index("--") + 2])


def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root / f"FirstPlayerCapsule_{name}.fbx"))
    return bpy.data.objects["DGN_Armature"]


def read(rig, time):
    bpy.context.scene.frame_set(int(time), subframe=time - int(time))
    bpy.context.view_layer.update()
    keys = bpy.data.objects["Body"].data.shape_keys
    pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
    shapes = {key.name: key.value for key in keys.key_blocks[1:]} if keys else {}
    return pose, shapes


def export(name, frames):
    rig = load("Idle_Breathing_2s")
    keys = bpy.data.objects["Body"].data.shape_keys
    rig.animation_data.action = bpy.data.actions.new(name)
    keys.animation_data_clear()
    for frame, (pose, shapes) in enumerate(frames):
        for bone in rig.pose.bones:
            bone.matrix_basis = pose[bone.name]
            bone.rotation_mode = "QUATERNION"
            bone.keyframe_insert("location", frame=frame)
            bone.keyframe_insert("rotation_quaternion", frame=frame)
            bone.keyframe_insert("scale", frame=frame)
        for key in keys.key_blocks[1:]:
            key.value = shapes.get(key.name, 0.0)
            key.keyframe_insert("value", frame=frame)
    scene = bpy.context.scene
    scene.frame_start = 0
    scene.frame_end = last
    scene.render.fps = 30
    scene.frame_set(0)
    path = root / f"FirstPlayerCapsule_{name}.fbx"
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
    print("RETIMED", name, last)


for name in ("Jump_Cute", "Carry_Jump"):
    rig = load(name)
    start, end = rig.animation_data.action.frame_range
    samples = [read(rig, start + (end - start) * i / last) for i in range(last + 1)]
    export(name, samples)
