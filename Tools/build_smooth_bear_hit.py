"""Author a dynamic SmoothBear hit reaction in Blender.

blender --background --python Tools/build_smooth_bear_hit.py [-- --no-render] [-- --video]

Writes source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend holding:
  Idle_Reference    idle breathing, appended from the Greeting emote blend
  Hit_Current       the shipped Assets/.../Animations/Hit.anim converted back onto the rig
  Hit_MixamoSource  Combat/mixamo/Big Hit To Head.fbx retargeted onto SmoothBear (reference)
  Hit               the new reaction: frames 1-31 at 30 fps (1.0 s, same length as the shipped clip)

The new Hit keeps the shipped clip as its base so the arms and fingers stay familiar, then
1. retimes it so the impact peaks at ~0.2 s instead of 0.37 s,
2. replaces the forward fold with a backward head/torso whiplash and a dazed recovery nod,
3. shoves the hips back and down with a pelvis tilt,
4. re-solves both legs with 2-bone IK so the feet stay planted while the knees give.

Preview frames and a contact sheet land in artifacts/hit-preview/ (--video adds MP4s when the
Blender build ships FFMPEG; the bundled 5.2 here does not).
"""
from __future__ import annotations

import math
import re
import sys
from bisect import bisect_right
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
from retarget_smooth_bear_emotes import bake_clip, rest_correction, sample_source  # noqa: E402

CHARACTER = ROOT / "source/blender/characters/SmoothBear/SmoothBear.blend"
GREETING = ROOT / "source/blender/characters/SmoothBear/Emotes/SmoothBear_Greeting.blend"
HIT_ANIM = ROOT / "Assets/_Game/Content/Characters/SmoothBear/Animations/Hit.anim"
OUT_DIR = ROOT / "source/blender/characters/SmoothBear/Combat"
MIXAMO = OUT_DIR / "mixamo/Big Hit To Head.fbx"
BLEND_PATH = OUT_DIR / "SmoothBear_Hit.blend"
PREVIEW_DIR = ROOT / "artifacts/hit-preview"

FPS = 30
FIRST = 1
LAST = 31  # 31 samples = 1.0 s, matching the shipped clip and PlayerAnimationDriver.HitSeconds
FORWARD = Vector((0.0, 0.0, 1.0))  # armature space; the object rotates it to world -Y

# Unity stores each node's local TRS in a left-handed frame. For this rig one X mirror maps
# Unity local <-> Blender parent-relative (verified against Taunt / Emote_Taunt.anim: 0.002 deg).
MIRROR_X = Matrix.Diagonal((-1.0, 1.0, 1.0, 1.0))

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER = "--no-render" not in ARGS
VIDEO = "--video" in ARGS

# ----------------------------------------------------------------------------- authoring data
# Old clip time reached at each new clip time: the shipped peak (0.37 s) now lands at 0.2 s.
TIME_WARP = [(0.0, 0.0), (0.2, 0.37), (0.55, 0.67), (1.0, 1.0)]

# How much to scale each bone's rotation away from idle in the shipped clip.
# Spine/Neck/Head are 0: the shipped clip (and its Mixamo source) fold forward like a body
# blow; the new reaction whips back instead, driven by the curves below.
GAIN = {
    "Spine": 0.0, "Neck": 0.0, "Head": 0.0,
    "Shoulder.L": 1.15, "Shoulder.R": 1.15,
    "UpperArm.L": 1.15, "UpperArm.R": 1.15,
    "Arm.L": 1.0, "Arm.R": 1.0,
    "Hand.L": 1.0, "Hand.R": 1.0,
}

# Additive torso whiplash (degrees, about the bone's rest X; negative tilts back) over clip
# time (s): snap back within 4 frames, hold, then a dazed forward nod while recovering.
HEAD_SNAP = [(0.0, 0.0), (0.05, -19.0), (0.13, -22.0), (0.3, -12.0), (0.5, 0.0), (0.68, 8.0), (1.0, 0.0)]
NECK_SNAP = [(0.0, 0.0), (0.07, -14.0), (0.17, -18.0), (0.35, -10.0), (0.55, 0.0), (0.7, 5.0), (1.0, 0.0)]
SPINE_ARCH = [(0.0, 0.0), (0.1, -10.0), (0.2, -16.0), (0.35, -13.0), (0.55, -2.0), (0.7, 4.0), (1.0, 0.0)]
# Head turns away from the punch (degrees about rest Y).
HEAD_YAW = [(0.0, 0.0), (0.1, 10.0), (0.3, 7.0), (0.6, 0.0), (1.0, 0.0)]

# Hips: knockback along -FORWARD (m), drop (m), pitch back (deg), yaw (deg).
HIPS_BACK = [(0.0, 0.0), (0.1, -0.06), (0.2, -0.12), (0.33, -0.11), (0.6, -0.03), (0.8, 0.01), (1.0, 0.0)]
HIPS_DOWN = [(0.0, 0.0), (0.1, -0.03), (0.23, -0.07), (0.4, -0.06), (0.7, -0.01), (1.0, 0.0)]
HIPS_PITCH = [(0.0, 0.0), (0.13, -8.0), (0.3, -7.0), (0.6, -1.0), (0.8, 1.0), (1.0, 0.0)]
HIPS_YAW = [(0.0, 0.0), (0.15, 7.0), (0.4, 5.0), (0.7, 0.0), (1.0, 0.0)]

SHEET_FRAMES = [1, 3, 5, 7, 10, 13, 17, 22, 31]
TILE = 360


# ----------------------------------------------------------------------------- helpers
def curve(points):
    """Cubic Hermite through (t, v) points with auto-clamped tangents, like Blender's F-curves."""
    ts = [p[0] for p in points]
    vs = [p[1] for p in points]
    n = len(points)
    m = [0.0] * n
    for i in range(1, n - 1):
        s0 = (vs[i] - vs[i - 1]) / (ts[i] - ts[i - 1])
        s1 = (vs[i + 1] - vs[i]) / (ts[i + 1] - ts[i])
        if s0 * s1 <= 0:
            continue
        m[i] = (vs[i + 1] - vs[i - 1]) / (ts[i + 1] - ts[i - 1])
        m[i] = math.copysign(min(abs(m[i]), 3 * min(abs(s0), abs(s1))), m[i])

    def evaluate(t):
        if t <= ts[0]:
            return vs[0]
        if t >= ts[-1]:
            return vs[-1]
        i = bisect_right(ts, t) - 1
        h = ts[i + 1] - ts[i]
        u = (t - ts[i]) / h
        h00 = 2 * u ** 3 - 3 * u ** 2 + 1
        h10 = u ** 3 - 2 * u ** 2 + u
        h01 = -2 * u ** 3 + 3 * u ** 2
        h11 = u ** 3 - u ** 2
        return h00 * vs[i] + h10 * h * m[i] + h01 * vs[i + 1] + h11 * h * m[i + 1]

    return evaluate


def mix(a, b, t):
    al, aq, asc = a.decompose()
    bl, bq, bsc = b.decompose()
    return Matrix.LocRotScale(al.lerp(bl, t), aq.slerp(bq, t), asc.lerp(bsc, t))


def amplify(idle_m, cur_m, gain):
    """Scale cur's rotation/translation away from idle by gain (gain > 1 extrapolates)."""
    iq = idle_m.to_quaternion().normalized()
    cq = cur_m.to_quaternion().normalized()
    delta = iq.inverted() @ cq
    if delta.w < 0:
        delta.negate()
    scaled = Quaternion(delta.axis, delta.angle * gain) if delta.angle > 1e-6 else Quaternion()
    il = idle_m.to_translation()
    cl = cur_m.to_translation()
    return Matrix.LocRotScale(il + (cl - il) * gain, iq @ scaled, Vector((1.0, 1.0, 1.0)))


def rest_local(bone):
    if bone.parent:
        return bone.parent.matrix_local.inverted() @ bone.matrix_local
    return bone.matrix_local


NUMBER = r"[-+\d.eE]+"


def parse_unity_anim(path):
    text = path.read_text(encoding="utf-8")
    rotation = {}
    section = text.split("m_RotationCurves:")[1].split("m_CompressedRotationCurves:")[0]
    for block in re.split(r"\n  - curve:", section)[1:]:
        name = re.search(r"path: (\S+)", block).group(1).split("/")[-1]
        keys = re.findall(
            rf"time: ({NUMBER})\s+value: \{{x: ({NUMBER}), y: ({NUMBER}), z: ({NUMBER}), w: ({NUMBER})\}}", block
        )
        rotation[name] = [(float(t), Quaternion((float(w), float(x), float(y), float(z)))) for t, x, y, z, w in keys]
    position = {}
    section = text.split("m_PositionCurves:")[1].split("m_ScaleCurves:")[0]
    for block in re.split(r"\n  - curve:", section)[1:]:
        name = re.search(r"path: (\S+)", block).group(1).split("/")[-1]
        keys = re.findall(rf"time: ({NUMBER})\s+value: \{{x: ({NUMBER}), y: ({NUMBER}), z: ({NUMBER})\}}", block)
        position[name] = [(float(t), Vector((float(x), float(y), float(z)))) for t, x, y, z in keys]
    return rotation, position


def sample_keys(keys, t, slerp):
    if t <= keys[0][0]:
        return keys[0][1].copy()
    if t >= keys[-1][0]:
        return keys[-1][1].copy()
    times = [k[0] for k in keys]
    i = bisect_right(times, t) - 1
    (t0, a), (t1, b) = keys[i], keys[i + 1]
    u = (t - t0) / (t1 - t0)
    if slerp:
        if a.dot(b) < 0:
            b = -b
        return a.slerp(b, u)
    return a.lerp(b, u)


def unity_to_basis(bone, q_unity, p_unity):
    local_rest = rest_local(bone)
    if p_unity is None:
        p_unity = (MIRROR_X @ local_rest).to_translation()
    unity_local = Matrix.LocRotScale(p_unity, q_unity, None)
    parent_relative = MIRROR_X @ unity_local @ MIRROR_X
    return local_rest.inverted() @ parent_relative


# ----------------------------------------------------------------------------- rig plumbing
def new_action(rig, name):
    action = bpy.data.actions.get(name)
    if action is not None:
        bpy.data.actions.remove(action)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    animation = rig.animation_data_create()
    animation.action = action
    if action.slots:
        animation.action_slot = action.slots[0]
    return action


def assign(rig, action):
    animation = rig.animation_data_create()
    animation.action = action
    if action.slots:
        animation.action_slot = action.slots[0]


def linear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fcurve in bag.fcurves:
                    for key in fcurve.keyframe_points:
                        key.interpolation = "LINEAR"


def key_pose(rig, frame, previous):
    for bone in rig.pose.bones:
        q = bone.rotation_quaternion.copy()
        if bone.name in previous and q.dot(previous[bone.name]) < 0:
            q.negate()
            bone.rotation_quaternion = q
        previous[bone.name] = q.copy()
        for prop in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(prop, frame=frame, group=bone.name)


def set_basis(rig, pose):
    for bone in rig.pose.bones:
        if bone.name in pose:
            bone.matrix_basis = pose[bone.name]
    bpy.context.view_layer.update()


def apply_volume_bones(rig):
    """Match SmoothBearEmoteClipImporter.BakeVolumeBones: half of the driving bone's local rotation."""
    for bone in rig.pose.bones:
        if not bone.name.startswith("Volume_"):
            continue
        driver = rig.pose.bones[bone.name[len("Volume_"):]]
        bone.rotation_quaternion = Quaternion().slerp(driver.rotation_quaternion, 0.5)
        bone.location = driver.location.copy()
        bone.scale = driver.scale.copy()
    bpy.context.view_layer.update()


def current_pose(rig):
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


# ----------------------------------------------------------------------------- stages
def open_character():
    bpy.ops.wm.open_mainfile(filepath=str(CHARACTER), use_scripts=False)
    rig = bpy.data.objects["DGN_Armature"]
    rig.hide_viewport = False
    rig.hide_set(False)
    rig.data.pose_position = "POSE"
    if rig.animation_data:
        rig.animation_data_clear()
    for obj in bpy.data.objects:
        if obj.type == "MESH" and obj.data.shape_keys:
            obj.data.shape_keys.animation_data_clear()
            for block in obj.data.shape_keys.key_blocks[1:]:
                block.value = 0.0
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    helper = bpy.data.texts.get("preview_walk.py")
    if helper:
        helper.use_module = False
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    return rig


def append_idle_reference():
    with bpy.data.libraries.load(str(GREETING), link=False) as (source, target):
        target.actions = ["Idle_Reference"]
    action = bpy.data.actions["Idle_Reference"]
    action.use_fake_user = True
    return action


def bake_current(rig):
    """Convert the shipped Unity clip back onto the rig; returns per-frame basis dicts."""
    rotation, position = parse_unity_anim(HIT_ANIM)
    new_action(rig, "Hit_Current")
    frames = {}
    previous = {}
    for frame in range(FIRST, LAST + 1):
        t = (frame - FIRST) / FPS
        # frame_set first: a later depsgraph update would otherwise re-evaluate the action
        # (tagged by the previous frame's keyframe_insert) and overwrite the pose set below.
        bpy.context.scene.frame_set(frame)
        for bone in rig.pose.bones:
            if bone.name not in rotation:
                continue
            q = sample_keys(rotation[bone.name], t, slerp=True)
            p = sample_keys(position[bone.name], t, slerp=False) if bone.name in position else None
            bone.matrix_basis = unity_to_basis(bone.bone, q, p)
        bpy.context.view_layer.update()
        frames[frame] = current_pose(rig)
        key_pose(rig, frame, previous)
    linear(rig.animation_data.action)
    return frames


def check_idle_match(rig, idle_action, idle_pose):
    assign(rig, idle_action)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    worst = (0.0, "")
    for bone in rig.pose.bones:
        if bone.name.startswith("Volume_"):
            continue
        a = bone.matrix_basis.to_quaternion()
        b = idle_pose[bone.name].to_quaternion()
        angle = math.degrees(2 * math.acos(min(1.0, abs(a.dot(b)))))
        if angle > worst[0]:
            worst = (angle, bone.name)
    print(f"IDLE_CHECK Hit.anim frame 0 vs Idle_Reference frame 1: worst {worst[0]:.1f} deg on {worst[1]}")


def bake_mixamo(rig):
    existing_objects = set(bpy.data.objects)
    existing_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=str(MIXAMO), use_anim=True)
    imported = [obj for obj in bpy.data.objects if obj not in existing_objects]
    source = next(obj for obj in imported if obj.type == "ARMATURE")
    shared, correction = rest_correction(rig, source)
    first, last = (int(value) for value in source.animation_data.action.frame_range)
    samples = sample_source(source, first, last, shared)
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        if action not in existing_actions:
            bpy.data.actions.remove(action)
    action = bake_clip(rig, samples, shared, correction, "Hit_MixamoSource", first, last)
    print("BAKED Hit_MixamoSource", first, last, "bones", len(shared))
    return action, first, last


def solve_leg(rig, side, idle_world):
    upper = rig.pose.bones["UpperLeg." + side]
    lower = rig.pose.bones["Leg." + side]
    foot = rig.pose.bones["Foot." + side]
    bpy.context.view_layer.update()
    hip = upper.head.copy()
    target = idle_world["Foot." + side].to_translation()
    l1, l2 = upper.bone.length, lower.bone.length
    reach = target - hip
    d = reach.length
    limit = (l1 + l2) * 0.999
    if d > limit:
        target = hip + reach.normalized() * limit
        reach = target - hip
        d = limit
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    direction = reach.normalized()
    normal = FORWARD - FORWARD.dot(direction) * direction
    normal = normal.normalized() if normal.length > 1e-6 else FORWARD.copy()
    knee = hip + direction * a + normal * h

    def aim(bone, to, reference):
        axis = (reference.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
        rotation = axis.rotation_difference(to.normalized())
        matrix = (rotation.to_matrix() @ reference.to_3x3()).to_4x4()
        matrix.translation = bone.head
        bone.matrix = matrix
        bpy.context.view_layer.update()

    aim(upper, knee - hip, idle_world["UpperLeg." + side])
    aim(lower, target - knee, idle_world["Leg." + side])
    planted = idle_world["Foot." + side].copy()
    planted.translation = foot.head
    foot.matrix = planted
    bpy.context.view_layer.update()
    return (foot.head - idle_world["Foot." + side].to_translation()).length, math.degrees(
        (knee - hip).angle(target - knee)
    )


def pitch(degrees):
    return Quaternion((1.0, 0.0, 0.0), math.radians(degrees))


def author_hit(rig, current):
    idle = current[FIRST]
    new_action(rig, "Hit")
    bpy.context.scene.frame_set(FIRST)
    set_basis(rig, idle)
    idle_world = {bone.name: bone.matrix.copy() for bone in rig.pose.bones}

    warp = curve(TIME_WARP)
    head_snap, neck_snap, spine_arch = curve(HEAD_SNAP), curve(NECK_SNAP), curve(SPINE_ARCH)
    head_yaw = curve(HEAD_YAW)
    hips_back, hips_down = curve(HIPS_BACK), curve(HIPS_DOWN)
    hips_pitch, hips_yaw = curve(HIPS_PITCH), curve(HIPS_YAW)

    previous = {}
    max_step = 0.0
    last_world = None
    for frame in range(FIRST, LAST + 1):
        t = (frame - FIRST) / FPS
        bpy.context.scene.frame_set(frame)  # see bake_current
        # 1. time-warped, amplified copy of the shipped clip
        old = FIRST + warp(t) * (LAST - FIRST)
        lo = int(math.floor(old))
        hi = min(lo + 1, LAST)
        pose = {}
        for name, idle_m in idle.items():
            if name.startswith("Volume_"):
                continue
            sampled = mix(current[lo][name], current[hi][name], old - lo) if hi != lo else current[lo][name].copy()
            pose[name] = amplify(idle_m, sampled, GAIN.get(name, 1.0))
        # 2. additive snaps about the rest X axis (negative = tilt back)
        for name, extra in (("Head", head_snap(t)), ("Neck", neck_snap(t)), ("Spine", spine_arch(t))):
            pose[name] = pitch(extra).to_matrix().to_4x4() @ pose[name]
        head_turn = Quaternion((0.0, 1.0, 0.0), math.radians(head_yaw(t)))
        pose["Head"] = head_turn.to_matrix().to_4x4() @ pose["Head"]
        # 3. hips knockback: translation in the Hips rest frame (= armature axes), pitch then yaw
        hips = pose["Hips"]
        hips_rotation = (
            Quaternion((0.0, 1.0, 0.0), math.radians(hips_yaw(t)))
            @ pitch(hips_pitch(t))
            @ hips.to_quaternion()
        )
        hips_location = hips.to_translation() + FORWARD * hips_back(t) + Vector((0.0, hips_down(t), 0.0))
        pose["Hips"] = Matrix.LocRotScale(hips_location, hips_rotation, Vector((1.0, 1.0, 1.0)))
        set_basis(rig, pose)
        # 4. legs: keep both feet where idle put them
        foot_error = 0.0
        knees = []
        for side in ("L", "R"):
            error, knee = solve_leg(rig, side, idle_world)
            foot_error = max(foot_error, error)
            knees.append(knee)
        apply_volume_bones(rig)
        # diagnostics
        world = {bone.name: bone.matrix.to_quaternion() for bone in rig.pose.bones}
        if last_world:
            step = max(
                math.degrees(2 * math.acos(min(1.0, abs(world[name].dot(last_world[name]))))) for name in world
            )
            max_step = max(max_step, step)
        last_world = world
        head = rig.pose.bones["Head"]
        head_tilt = math.degrees((head.tail - head.head).angle(Vector((0.0, 1.0, 0.0))))
        print(
            f"HIT f{frame:02d} t={t:.2f} old={old:5.2f} hips=({hips_location.x:+.3f},{hips_location.y:+.3f},{hips_location.z:+.3f}) "
            f"headTilt={head_tilt:5.1f} knees=({knees[0]:5.1f},{knees[1]:5.1f}) footErr={foot_error:.4f}"
        )
        key_pose(rig, frame, previous)
    linear(rig.animation_data.action)
    assert max_step < 90.0, max_step  # fingertips whip ~80 deg/frame at impact; anything more is a flip
    print(f"HIT maxStep {max_step:.1f} deg/frame")
    return bpy.data.actions["Hit"]


# ----------------------------------------------------------------------------- scene, preview
def write_preview_text(clips):
    lines = [
        "import bpy",
        "",
        f"CLIPS = {clips!r}",
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
        "    print('HIT_PREVIEW', name)",
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
    text = bpy.data.texts.get("preview_hit.py") or bpy.data.texts.new("preview_hit.py")
    text.clear()
    text.write("\n".join(lines))
    text.use_module = True


def make_camera(name, location, target, lens=45):
    existing = bpy.data.objects.get(name)
    if existing:
        bpy.data.objects.remove(existing, do_unlink=True)
    data = bpy.data.cameras.new(name)
    camera = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = Vector(location)
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    data.lens = lens
    return camera


def setup_preview():
    scene = bpy.context.scene
    front = make_camera("HitPreviewCamera", (2.3, -3.5, 1.25), (0.0, 0.0, 0.85))
    side = make_camera("HitPreviewSide", (3.9, 0.15, 1.05), (0.0, 0.15, 0.85))
    for name in ("HitPreviewKey",):
        if bpy.data.objects.get(name):
            bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    light_data = bpy.data.lights.new("HitPreviewKey", "AREA")
    light = bpy.data.objects.new("HitPreviewKey", light_data)
    scene.collection.objects.link(light)
    light.location = Vector((2.0, -2.4, 4.0))
    light_data.energy = 500
    light_data.size = 4
    scene.camera = front
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = False
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    return front, side


def render_sheet(rig, cameras, actions):
    import numpy as np

    scene = bpy.context.scene
    scene.render.resolution_x = TILE
    scene.render.resolution_y = TILE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    frames_dir = PREVIEW_DIR / "frames"
    frames_dir.mkdir(parents=True, exist_ok=True)
    rows = []
    for camera_name, camera in cameras:
        for action in actions:
            assign(rig, action)
            tiles = []
            for frame in SHEET_FRAMES:
                scene.frame_set(frame)
                bpy.context.view_layer.update()
                scene.camera = camera
                path = frames_dir / f"{action.name}_{camera_name}_{frame:02d}.png"
                scene.render.filepath = str(path)
                bpy.ops.render.render(write_still=True)
                image = bpy.data.images.load(str(path))
                pixels = np.array(image.pixels[:], dtype=np.float32).reshape(TILE, TILE, 4)
                bpy.data.images.remove(image)
                tiles.append(pixels)
            rows.append(np.concatenate(tiles, axis=1))
            print("SHEET_ROW", camera_name, action.name, flush=True)
    sheet = np.concatenate(rows[::-1], axis=0)  # Blender images are bottom-up
    height, width = sheet.shape[:2]
    out = bpy.data.images.new("HitContactSheet", width=width, height=height, alpha=True)
    out.pixels[:] = sheet.ravel().tolist()
    out.filepath_raw = str(PREVIEW_DIR / "hit-contact-sheet.png")
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    print("SHEET", PREVIEW_DIR / "hit-contact-sheet.png", flush=True)


def render_videos(rig, camera, actions):
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.resolution_x = 720
    scene.render.resolution_y = 720
    try:
        scene.render.image_settings.file_format = "FFMPEG"
    except TypeError:
        print("VIDEO skipped: this Blender build has no FFMPEG output; use the contact sheet or the blend")
        return
    scene.render.ffmpeg.format = "MPEG4"
    scene.render.ffmpeg.codec = "H264"
    scene.render.ffmpeg.constant_rate_factor = "HIGH"
    for action in actions:
        assign(rig, action)
        first, last = (int(v) for v in action.frame_range)
        scene.frame_start, scene.frame_end = first, last
        scene.render.filepath = str(PREVIEW_DIR / f"{action.name}.mp4")
        bpy.ops.render.render(animation=True)
        print("VIDEO", scene.render.filepath, flush=True)


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    rig = open_character()
    scene = bpy.context.scene
    scene.render.fps = 30

    idle_action = append_idle_reference()
    current = bake_current(rig)
    check_idle_match(rig, idle_action, current[FIRST])
    mixamo_action, mixamo_first, mixamo_last = bake_mixamo(rig)
    hit_action = author_hit(rig, current)

    front, side = setup_preview()
    assign(rig, hit_action)
    scene.frame_start, scene.frame_end = FIRST, LAST
    scene.frame_set(FIRST)
    write_preview_text(
        [
            ("Hit", FIRST, LAST, False),
            ("Hit_Current", FIRST, LAST, False),
            ("Hit_MixamoSource", mixamo_first, mixamo_last, False),
            ("Idle_Reference", 1, 61, True),
        ]
    )
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print("SAVED", BLEND_PATH, flush=True)

    if RENDER:
        current_action = bpy.data.actions["Hit_Current"]
        render_sheet(rig, [("front", front), ("side", side)], [current_action, hit_action])
        if VIDEO:
            render_videos(rig, front, [current_action, hit_action])
        assign(rig, hit_action)
        scene.frame_set(FIRST)


if __name__ == "__main__":
    main()
