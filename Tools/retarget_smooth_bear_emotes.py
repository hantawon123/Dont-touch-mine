"""Bake Mixamo DGN-rig emote FBXs onto SmoothBear for Blender playback.

blender --background --python Tools/retarget_smooth_bear_emotes.py
"""
from __future__ import annotations

import json
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
CHARACTER = ROOT / "source/blender/characters/SmoothBear/SmoothBear.blend"
DOWNLOADS = Path(r"C:\Users\SSAFY\Downloads")
OUT_DIR = ROOT / "source/blender/characters/SmoothBear/Emotes"
MIXAMO_DIR = OUT_DIR / "mixamo"
FRAMES_DIR = ROOT / "artifacts/emote-preview/frames"
BLEND_PATH = OUT_DIR / "SmoothBear_EmotePreview.blend"
REPORT_PATH = ROOT / "artifacts/emote-preview/bake-report.json"

CLIPS = (
    ("Slide Hip Hop Dance.fbx", "Dance_HipHop", True),
    ("Chicken Dance.fbx", "Dance_Chicken", True),
    ("Northern Soul Spin Combo.fbx", "Dance_SoulSpin", True),
    ("Waving.fbx", "Greeting", False),
    ("Insult.fbx", "Insult", False),
    ("Taunt Gesture.fbx", "Taunt", False),
)

PREVIEW_SCRIPT = """import bpy

CLIPS = [
    ("Dance_HipHop", 1, 520, True),
    ("Dance_Chicken", 1, 144, True),
    ("Dance_SoulSpin", 1, 266, True),
    ("Greeting", 1, 17, False),
    ("Insult", 1, 81, False),
    ("Taunt", 1, 60, False),
]


def bind_playback():
    try:
        bpy.context.preferences.keymap.spacebar_action = "PLAY"
    except Exception:
        pass


def assign(index):
    name, start, end, loop = CLIPS[index]
    scene = bpy.context.scene
    scene.frame_start = start
    scene.frame_end = end
    scene.frame_current = start
    scene.render.fps = 30
    rig = bpy.data.objects.get("DGN_Armature")
    action = bpy.data.actions.get(name)
    if rig is None or action is None:
        return
    rig.data.pose_position = "POSE"
    animation = rig.animation_data_create()
    animation.action = action
    if action.slots:
        animation.action_slot = action.slots[0]
    print("EMOTE", name, start, end, "loop" if loop else "oneshot")


def register():
    bind_playback()
    assign(0)

    keymap = bpy.context.window_manager.keyconfigs.addon
    if keymap is None:
        return
    km = keymap.keymaps.get("3D View") or keymap.keymaps.new(
        name="3D View", space_type="VIEW_3D"
    )
    for item in list(km.keymap_items):
        if item.idname == "wm.context_set_int":
            km.keymap_items.remove(item)
    for i, key in enumerate("ONE TWO THREE FOUR FIVE SIX".split()):
        km.keymap_items.new("screen.animation_cancel", key, "PRESS")

    def on_key(scene):
        pass

    # Keys 1-6 are handled by the modal below if available; otherwise
    # switch clips from the Action Editor.
    assign(0)


register()
"""


def linear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag is None:
                    continue
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:
                        key.interpolation = "LINEAR"


def copy_sources():
    MIXAMO_DIR.mkdir(parents=True, exist_ok=True)
    for filename, _, _ in CLIPS:
        src = DOWNLOADS / filename
        if not src.is_file():
            raise FileNotFoundError(src)
        dest = MIXAMO_DIR / filename
        shutil.copy2(src, dest)


def rest_correction(target, source):
    shared = [bone.name for bone in target.data.bones if bone.name in source.data.bones]
    correction = {}
    for name in shared:
        bone = target.data.bones[name]
        src = source.data.bones[name]
        aligned = (bone.tail_local - bone.head_local).normalized().rotation_difference(
            (src.tail_local - src.head_local).normalized()
        )
        correction[name] = (
            src.matrix_local.to_quaternion().inverted()
            @ aligned
            @ bone.matrix_local.to_quaternion()
        ).to_matrix().to_4x4()
    return shared, correction


def bake_clip(target, samples, shared, correction, action_name, first, last):
    if target.animation_data:
        target.animation_data_clear()
    action = bpy.data.actions.get(action_name) or bpy.data.actions.new(action_name)
    action.use_fake_user = True
    target.animation_data_create().action = action
    if action.slots:
        target.animation_data.action_slot = action.slots[0]

    ordered = [bone for bone in target.pose.bones if bone.name in shared]
    ordered.sort(key=lambda bone: len(bone.parent_recursive))
    for bone in ordered:
        bone.rotation_mode = "QUATERNION"

    previous = {}
    scene = bpy.context.scene
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        pose = samples[frame]
        for bone in ordered:
            bone.matrix = pose[bone.name] @ correction[bone.name]
        bpy.context.view_layer.update()
        for bone in ordered:
            rotation = bone.rotation_quaternion.copy()
            if bone.name in previous and rotation.dot(previous[bone.name]) < 0:
                rotation.negate()
            bone.rotation_quaternion = rotation
            previous[bone.name] = rotation.copy()
            for prop in ("location", "rotation_quaternion", "scale"):
                bone.keyframe_insert(prop, frame=frame, group=bone.name)
    linear(action)
    return action


def sample_source(source, first, last, shared):
    samples = {}
    scene = bpy.context.scene
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[frame] = {name: source.pose.bones[name].matrix.copy() for name in shared}
    return samples


def setup_preview_camera():
    scene = bpy.context.scene
    for name in ("EmotePreviewCamera", "EmotePreviewKey"):
        obj = bpy.data.objects.get(name)
        if obj:
            bpy.data.objects.remove(obj, do_unlink=True)
    camera_data = bpy.data.cameras.new("EmotePreviewCamera")
    camera = bpy.data.objects.new("EmotePreviewCamera", camera_data)
    scene.collection.objects.link(camera)
    center = Vector((0.0, 0.0, 0.95))
    camera.location = Vector((2.4, -3.8, 1.35))
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.lens = 45
    scene.camera = camera

    light_data = bpy.data.lights.new("EmotePreviewKey", "AREA")
    light = bpy.data.objects.new("EmotePreviewKey", light_data)
    scene.collection.objects.link(light)
    light.location = Vector((2.0, -2.4, 4.0))
    light_data.energy = 500
    light_data.size = 4

    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    return camera


def render_frames(rig, actions):
    FRAMES_DIR.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    animation = rig.animation_data_create()
    for name, action, first, last, loop in actions:
        animation.action = action
        if action.slots:
            animation.action_slot = action.slots[0]
        count = 8 if loop else 5
        span = max(last - first, 1)
        for index in range(count):
            frame = first + round(span * index / (count - 1))
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            path = FRAMES_DIR / f"{name}_{index:02d}.png"
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            print("FRAME", path.name, "frame", frame, flush=True)


def write_preview_text(ranges):
    lines = [
        "import bpy",
        "",
        f"CLIPS = {ranges!r}",
        "",
        "def assign(index):",
        "    name, start, end, loop = CLIPS[index]",
        "    scene = bpy.context.scene",
        "    scene.frame_start = start",
        "    scene.frame_end = end",
        "    scene.frame_current = start",
        "    scene.render.fps = 30",
        "    rig = bpy.data.objects.get('DGN_Armature')",
        "    action = bpy.data.actions.get(name)",
        "    if rig is None or action is None:",
        "        return",
        "    rig.data.pose_position = 'POSE'",
        "    animation = rig.animation_data_create()",
        "    animation.action = action",
        "    if action.slots:",
        "        animation.action_slot = action.slots[0]",
        "    print('EMOTE', name)",
        "",
        "def register():",
        "    try:",
        "        bpy.context.preferences.keymap.spacebar_action = 'PLAY'",
        "    except Exception:",
        "        pass",
        "    assign(0)",
        "",
        "register()",
        "",
    ]
    text = bpy.data.texts.get("preview_emotes.py") or bpy.data.texts.new("preview_emotes.py")
    text.clear()
    text.write("\n".join(lines))
    text.use_module = True


def main():
    copy_sources()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    FRAMES_DIR.mkdir(parents=True, exist_ok=True)

    bpy.ops.wm.open_mainfile(filepath=str(CHARACTER), use_scripts=False)
    target = bpy.data.objects["DGN_Armature"]
    target.hide_viewport = False
    target.hide_set(False)
    target.data.pose_position = "POSE"
    if target.animation_data:
        target.animation_data_clear()

    helper = bpy.data.texts.get("preview_walk.py")
    if helper:
        helper.use_module = False

    baked = []
    report = []
    for filename, action_name, loop in CLIPS:
        existing = set(bpy.data.objects)
        existing_actions = set(bpy.data.actions)
        bpy.ops.import_scene.fbx(filepath=str(MIXAMO_DIR / filename), use_anim=True)
        imported = [obj for obj in bpy.data.objects if obj not in existing]
        source = next(obj for obj in imported if obj.type == "ARMATURE")
        shared, correction = rest_correction(target, source)
        source_action = source.animation_data.action
        first, last = (int(value) for value in source_action.frame_range)
        samples = sample_source(source, first, last, shared)
        for obj in imported:
            bpy.data.objects.remove(obj, do_unlink=True)
        for action in list(bpy.data.actions):
            if action not in existing_actions and action.name != action_name:
                bpy.data.actions.remove(action)
        action = bake_clip(target, samples, shared, correction, action_name, first, last)
        baked.append((action_name, action, first, last, loop))
        report.append(
            {
                "file": filename,
                "action": action_name,
                "loop": loop,
                "frames": [first, last],
                "shared_bones": len(shared),
            }
        )
        print("BAKED", action_name, first, last, "bones", len(shared), flush=True)

    scene = bpy.context.scene
    scene.render.fps = 30
    scene.frame_start = baked[0][2]
    scene.frame_end = baked[0][3]
    scene.frame_set(baked[0][2])
    animation = target.animation_data_create()
    animation.action = baked[0][1]
    if baked[0][1].slots:
        animation.action_slot = baked[0][1].slots[0]

    write_preview_text(
        [(name, first, last, loop) for name, _, first, last, loop in baked]
    )
    setup_preview_camera()
    render_frames(target, baked)

    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("SAVED", BLEND_PATH, flush=True)


if __name__ == "__main__":
    main()
