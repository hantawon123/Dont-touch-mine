"""Author the knockout transition (Stun_Start) on top of the new Hit reaction.

blender --background --python Tools/build_smooth_bear_stun_start.py [-- --no-render]

Opens source/blender/characters/SmoothBear/Combat/SmoothBear_Hit.blend (built by
Tools/build_smooth_bear_hit.py), adds two actions and saves the blend in place:
  Stun_Start_Current  the shipped Assets/.../Animations/Stun_Start.anim converted onto the rig
  Stun_Start          frames 1-67 at 30 fps (2.2 s, same as PlayerAnimationDriver.StunStartSeconds)

Timeline of the new Stun_Start:
  0.00-0.30 s  the new Hit impact (head whips back, hips shoved back, knees give)
  0.30-0.60 s  tries to straighten up, but the knees give, the arms go limp and the head drops
  0.60-1.25 s  falls backward under gravity onto the back
  1.25-1.60 s  lands with a small pelvis bounce and head bob
  1.60-2.20 s  holds the exact first frame of Stun_Idle so the loop takes over seamlessly
"""
from __future__ import annotations

import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
import build_smooth_bear_hit as hit_tools  # noqa: E402

STUN_START_ANIM = ROOT / "Assets/_Game/Content/Characters/SmoothBear/Animations/Stun_Start.anim"
STUN_IDLE_ANIM = ROOT / "Assets/_Game/Content/Characters/SmoothBear/Animations/Stun_Idle.anim"
BLEND_PATH = hit_tools.BLEND_PATH
PREVIEW_DIR = hit_tools.PREVIEW_DIR

FPS = 30
FIRST = 1
LAST = 67  # 2.2 s
T_IMPACT = 0.30
T_BUCKLE = 0.60
T_LAND = 1.25
T_SETTLE = 1.60

# Extra hips offset reached at T_BUCKLE, on top of the Hit pose at T_IMPACT (armature axes).
BUCKLE_HIPS_OFFSET = Vector((0.0, -0.04, -0.03))
BUCKLE_HIPS_PITCH = -4.0
BUCKLE_TORSO_TOWARD_IDLE = 0.6  # the failed attempt to straighten up
BUCKLE_HEAD_DROOP = 28.0  # deg forward: lights out
BUCKLE_ARM_TOWARD_LYING = 0.5
LAND_BOUNCE = 0.02  # m, pelvis lift right after touching down
LAND_HEAD_BOB = 8.0  # deg, head rocks forward on impact

SHEET_FRAMES = [1, 4, 7, 10, 16, 22, 28, 34, 40, 46, 67]
TILE = 360

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RENDER = "--no-render" not in ARGS


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def bump(t):
    """0 -> 1 -> 0 over [0, 1]."""
    t = max(0.0, min(1.0, t))
    return math.sin(math.pi * t)


def is_arm(name):
    return name.startswith(("Shoulder", "UpperArm", "Arm.", "Hand", "Finger", "Volume_Arm", "Volume_UpperArm"))


def anim_pose(rig, path, t):
    rotation, position = hit_tools.parse_unity_anim(path)
    pose = {}
    for bone in rig.pose.bones:
        if bone.name not in rotation:
            continue
        q = hit_tools.sample_keys(rotation[bone.name], t, slerp=True)
        p = hit_tools.sample_keys(position[bone.name], t, slerp=False) if bone.name in position else None
        pose[bone.name] = hit_tools.unity_to_basis(bone.bone, q, p)
    return pose


def bake_anim(rig, path, name, last):
    hit_tools.new_action(rig, name)
    previous = {}
    for frame in range(FIRST, last + 1):
        bpy.context.scene.frame_set(frame)
        hit_tools.set_basis(rig, anim_pose(rig, path, (frame - FIRST) / FPS))
        hit_tools.key_pose(rig, frame, previous)
    hit_tools.linear(rig.animation_data.action)


def sample_action(rig, action, first, last):
    hit_tools.assign(rig, action)
    frames = {}
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        frames[frame] = hit_tools.current_pose(rig)
    return frames


def pose_at(frames, first, t):
    frame = first + t * FPS
    lo = int(math.floor(frame))
    hi = min(lo + 1, max(frames))
    if hi == lo:
        return {name: m.copy() for name, m in frames[lo].items()}
    return {name: hit_tools.mix(frames[lo][name], frames[hi][name], frame - lo) for name in frames[lo]}


def mix_pose(a, b, t):
    return {name: hit_tools.mix(a[name], b[name], t) for name in a}


def pose_angle(a, b, skip_volume=True):
    worst = (0.0, "")
    for name, m in a.items():
        if skip_volume and name.startswith("Volume_"):
            continue
        qa, qb = m.to_quaternion(), b[name].to_quaternion()
        angle = math.degrees(2 * math.acos(min(1.0, abs(qa.dot(qb)))))
        if angle > worst[0]:
            worst = (angle, name)
    return worst


def author_stun_start(rig, hit_frames, lying):
    hit_tools.new_action(rig, "Stun_Start")
    bpy.context.scene.frame_set(FIRST)
    hit_tools.set_basis(rig, hit_frames[FIRST])
    idle_world = {bone.name: bone.matrix.copy() for bone in rig.pose.bones}

    impact = pose_at(hit_frames, FIRST, T_IMPACT)
    idle = hit_frames[FIRST]
    buckle = {name: m.copy() for name, m in impact.items()}
    for name in buckle:
        if is_arm(name):
            buckle[name] = hit_tools.mix(impact[name], lying[name], BUCKLE_ARM_TOWARD_LYING)
    for name in ("Spine", "Neck"):
        buckle[name] = hit_tools.mix(impact[name], idle[name], BUCKLE_TORSO_TOWARD_IDLE)
    buckle["Head"] = hit_tools.pitch(BUCKLE_HEAD_DROOP).to_matrix().to_4x4() @ idle["Head"]
    hips = impact["Hips"]
    buckle["Hips"] = Matrix.LocRotScale(
        hips.to_translation() + BUCKLE_HIPS_OFFSET,
        hit_tools.pitch(BUCKLE_HIPS_PITCH) @ hips.to_quaternion(),
        Vector((1.0, 1.0, 1.0)),
    )

    lying_hips = lying["Hips"]
    lying_loc, lying_rot, _ = lying_hips.decompose()
    buckle_full = None
    previous = {}
    for frame in range(FIRST, LAST + 1):
        t = (frame - FIRST) / FPS
        bpy.context.scene.frame_set(frame)
        phase = ""
        if t <= T_IMPACT + 1e-6:
            phase = "impact"
            hit_tools.set_basis(rig, pose_at(hit_frames, FIRST, t))
        elif t <= T_BUCKLE + 1e-6:
            phase = "buckle"
            u = smooth((t - T_IMPACT) / (T_BUCKLE - T_IMPACT))
            pose = mix_pose(impact, buckle, u)
            hit_tools.set_basis(rig, pose)
            for side in ("L", "R"):
                hit_tools.solve_leg(rig, side, idle_world)
            hit_tools.apply_volume_bones(rig)
            if abs(t - T_BUCKLE) < 1e-6:
                buckle_full = hit_tools.current_pose(rig)
        elif t <= T_LAND + 1e-6:
            phase = "fall"
            assert buckle_full is not None
            u = (t - T_BUCKLE) / (T_LAND - T_BUCKLE)
            fall = u * u  # gravity: slow start, fast finish
            pose = mix_pose(buckle_full, lying, fall)
            start_loc, start_rot, _ = buckle_full["Hips"].decompose()
            loc = Vector((
                start_loc.x + (lying_loc.x - start_loc.x) * smooth(u),
                start_loc.y + (lying_loc.y - start_loc.y) * fall,
                start_loc.z + (lying_loc.z - start_loc.z) * smooth(u),
            ))
            rot = start_rot.slerp(lying_rot, u ** 1.5)
            pose["Hips"] = Matrix.LocRotScale(loc, rot, Vector((1.0, 1.0, 1.0)))
            hit_tools.set_basis(rig, pose)
            hit_tools.apply_volume_bones(rig)
        else:
            phase = "land" if t < T_SETTLE else "hold"
            pose = {name: m.copy() for name, m in lying.items()}
            if t < T_SETTLE:
                u = (t - T_LAND) / (T_SETTLE - T_LAND)
                pose["Hips"] = Matrix.LocRotScale(
                    lying_loc + Vector((0.0, LAND_BOUNCE * bump(min(1.0, u * 1.6)), 0.0)),
                    lying_rot,
                    Vector((1.0, 1.0, 1.0)),
                )
                pose["Head"] = hit_tools.pitch(LAND_HEAD_BOB * bump(u)).to_matrix().to_4x4() @ pose["Head"]
            hit_tools.set_basis(rig, pose)
            if t < T_SETTLE:
                hit_tools.apply_volume_bones(rig)
        hips_now = rig.pose.bones["Hips"]
        print(
            f"STUN f{frame:02d} t={t:.2f} {phase:6s} hips=({hips_now.location.x:+.3f},{hips_now.location.y:+.3f},"
            f"{hips_now.location.z:+.3f}) hipsPitch={math.degrees(hips_now.rotation_quaternion.angle):5.1f}"
        )
        hit_tools.key_pose(rig, frame, previous)
    hit_tools.linear(rig.animation_data.action)
    return bpy.data.actions["Stun_Start"]


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
            hit_tools.assign(rig, action)
            tiles = []
            for frame in SHEET_FRAMES:
                scene.frame_set(frame)
                bpy.context.view_layer.update()
                scene.camera = camera
                path = frames_dir / f"{action.name}_{camera_name}_{frame:02d}.png"
                scene.render.filepath = str(path)
                bpy.ops.render.render(write_still=True)
                image = bpy.data.images.load(str(path))
                tiles.append(np.array(image.pixels[:], dtype=np.float32).reshape(TILE, TILE, 4))
                bpy.data.images.remove(image)
            rows.append(np.concatenate(tiles, axis=1))
    sheet = np.concatenate(rows[::-1], axis=0)
    height, width = sheet.shape[:2]
    out = bpy.data.images.new("StunContactSheet", width=width, height=height, alpha=True)
    out.pixels[:] = sheet.ravel().tolist()
    out.filepath_raw = str(PREVIEW_DIR / "stun-start-contact-sheet.png")
    out.file_format = "PNG"
    out.save()
    bpy.data.images.remove(out)
    print("SHEET", out.filepath_raw if False else PREVIEW_DIR / "stun-start-contact-sheet.png", flush=True)


def main():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH), use_scripts=False)
    rig = bpy.data.objects["DGN_Armature"]
    scene = bpy.context.scene
    scene.render.fps = FPS
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"

    hit_action = bpy.data.actions["Hit"]
    hit_frames = sample_action(rig, hit_action, hit_tools.FIRST, hit_tools.LAST)

    lying = anim_pose(rig, STUN_IDLE_ANIM, 0.0)
    shipped_end = anim_pose(rig, STUN_START_ANIM, 2.2)
    angle, bone = pose_angle(shipped_end, lying)
    print(f"STUN_IDLE_CHECK shipped Stun_Start end vs Stun_Idle start: worst {angle:.1f} deg on {bone}")
    loc = lying["Hips"].to_translation()
    print(f"LYING hips loc=({loc.x:+.3f},{loc.y:+.3f},{loc.z:+.3f}) pitch={math.degrees(lying['Hips'].to_quaternion().angle):.1f}")

    bake_anim(rig, STUN_START_ANIM, "Stun_Start_Current", LAST)
    stun_action = author_stun_start(rig, hit_frames, lying)

    hit_tools.assign(rig, hit_action)
    scene.frame_start, scene.frame_end = hit_tools.FIRST, hit_tools.LAST
    scene.frame_set(hit_tools.FIRST)
    hit_tools.write_preview_text(
        [
            ("Hit", hit_tools.FIRST, hit_tools.LAST, False),
            ("Stun_Start", FIRST, LAST, False),
            ("Hit_Current", hit_tools.FIRST, hit_tools.LAST, False),
            ("Stun_Start_Current", FIRST, LAST, False),
            ("Hit_MixamoSource", 1, 40, False),
            ("Idle_Reference", 1, 61, True),
        ]
    )
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH))
    print("SAVED", BLEND_PATH, flush=True)

    if RENDER:
        front = bpy.data.objects["HitPreviewCamera"]
        side = bpy.data.objects["HitPreviewSide"]
        render_sheet(
            rig,
            [("side", side), ("front", front)],
            [bpy.data.actions["Stun_Start_Current"], stun_action],
        )
        hit_tools.assign(rig, hit_action)
        scene.frame_set(hit_tools.FIRST)


if __name__ == "__main__":
    main()
