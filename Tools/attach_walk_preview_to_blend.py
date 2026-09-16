"""Bake Walk_Forward onto a First-rig body .blend and bind Space to play.

blender --background --python Tools/attach_walk_preview_to_blend.py -- BODY.blend
"""
from __future__ import annotations

import sys
from pathlib import Path

import bpy
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[1]
WALK_FBX = ROOT / "Assets" / "Scenes" / "CharacterTest" / "First" / "FirstPlayerCapsule_Walk_Forward.fbx"
PREVIEW_SCRIPT = """import bpy

WALK_START = 1
WALK_END = 24


def bind_space_play():
    try:
        bpy.context.preferences.keymap.spacebar_action = "PLAY"
    except Exception:
        pass
    wm = bpy.context.window_manager
    addon = wm.keyconfigs.addon
    if addon is None:
        return
    keymap = addon.keymaps.get("3D View") or addon.keymaps.new(
        name="3D View", space_type="VIEW_3D"
    )
    for item in list(keymap.keymap_items):
        if item.idname == "screen.animation_play" and item.type == "SPACE":
            keymap.keymap_items.remove(item)
    keymap.keymap_items.new("screen.animation_play", "SPACE", "PRESS")


def assign_walk():
    scene = bpy.context.scene
    scene.frame_start = WALK_START
    scene.frame_end = WALK_END
    scene.frame_current = WALK_START
    scene.render.fps = 30
    rig = bpy.data.objects.get("DGN_Armature")
    action = bpy.data.actions.get("Walk_Forward_Preview")
    if rig is None or action is None:
        return
    rig.data.pose_position = "POSE"
    animation = rig.animation_data_create()
    animation.action = action
    if action.slots:
        animation.action_slot = action.slots[0]


def register():
    bind_space_play()
    assign_walk()


register()
"""


def bake_walk(target, samples, source_rest, source_directions, last_frame):
    scene = bpy.context.scene
    target_names = {bone.name for bone in target.data.bones}
    missing = sorted(target_names.difference(source_rest))
    if missing:
        raise RuntimeError(f"Walk rig is missing bones: {missing}")

    correction = {}
    for bone in target.data.bones:
        aligned = (bone.tail_local - bone.head_local).normalized().rotation_difference(
            source_directions[bone.name]
        )
        correction[bone.name] = (
            source_rest[bone.name].to_quaternion().inverted()
            @ aligned
            @ bone.matrix_local.to_quaternion()
        ).to_matrix().to_4x4()

    if target.animation_data:
        target.animation_data_clear()
    baked = bpy.data.actions.get("Walk_Forward_Preview") or bpy.data.actions.new(
        "Walk_Forward_Preview"
    )
    baked.use_fake_user = True
    target.animation_data_create().action = baked
    if baked.slots:
        target.animation_data.action_slot = baked.slots[0]

    ordered = sorted(target.pose.bones, key=lambda bone: len(bone.parent_recursive))
    for bone in ordered:
        bone.rotation_mode = "QUATERNION"

    previous = {}
    for frame, source_pose in samples.items():
        scene.frame_set(frame)
        for bone in ordered:
            bone.matrix = source_pose[bone.name] @ correction[bone.name]
        bpy.context.view_layer.update()
        for bone in ordered:
            rotation = bone.rotation_quaternion.copy()
            if bone.name in previous and rotation.dot(previous[bone.name]) < 0:
                rotation.negate()
            bone.rotation_quaternion = rotation
            previous[bone.name] = rotation.copy()
            for property_name in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(property_name, frame=frame, group=bone.name)

    for layer in baked.layers:
        for strip in layer.strips:
            for slot in baked.slots:
                bag = strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.interpolation = "LINEAR"
    return baked


def attach(body_path: Path):
    bpy.ops.wm.open_mainfile(filepath=str(body_path), use_scripts=False)
    target = bpy.data.objects["DGN_Armature"]
    existing = set(bpy.data.objects)

    bpy.ops.import_scene.fbx(filepath=str(WALK_FBX))
    imported = [obj for obj in bpy.data.objects if obj not in existing]
    source = next(obj for obj in imported if obj.type == "ARMATURE")
    source_rest = {bone.name: bone.matrix_local.copy() for bone in source.data.bones}
    source_directions = {
        bone.name: (bone.tail_local - bone.head_local).normalized()
        for bone in source.data.bones
    }
    action = source.animation_data.action
    first, last = (int(value) for value in action.frame_range)
    samples = {}
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[frame] = {bone.name: bone.matrix.copy() for bone in source.pose.bones}

    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)

    loop_end = last - 1 if last > first else last
    baked = bake_walk(target, samples, source_rest, source_directions, loop_end)

    scene = bpy.context.scene
    scene.frame_start = first
    scene.frame_end = loop_end
    scene.frame_current = first
    scene.render.fps = 30
    target.data.pose_position = "POSE"
    target.hide_viewport = False
    target.hide_set(False)

    animation = target.animation_data
    while animation.nla_tracks:
        animation.nla_tracks.remove(animation.nla_tracks[0])
    track = animation.nla_tracks.new()
    track.name = "Walk_Forward"
    track.mute = False
    nla = track.strips.new("Walk_Forward", first, baked)
    nla.frame_end = loop_end
    nla.repeat = 8
    animation.action = baked
    if baked.slots:
        animation.action_slot = baked.slots[0]

    leftover = bpy.data.actions.get("DGN_Armature.001|Scene")
    if leftover:
        bpy.data.actions.remove(leftover)

    text = bpy.data.texts.get("preview_walk.py") or bpy.data.texts.new("preview_walk.py")
    text.clear()
    text.write(PREVIEW_SCRIPT)
    text.use_module = True

    preview_path = body_path.with_name(body_path.stem + "_WalkPreview.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(preview_path), copy=True)
    bpy.ops.wm.save_mainfile(filepath=str(body_path))
    print("ATTACHED Walk_Forward_Preview", first, loop_end, "SAVED", body_path)
    print("PREVIEW_COPY", preview_path)


if __name__ == "__main__":
    body = Path(sys.argv[sys.argv.index("--") + 1])
    attach(body)
