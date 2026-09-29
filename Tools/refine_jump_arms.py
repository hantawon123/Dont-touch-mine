"""Lower jump arm raise and snap the second half of the drop.

Blender --background --python Tools/refine_jump_arms.py -- FIRST_DIR
"""
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

root = Path(sys.argv[sys.argv.index("--") + 1])
RAISE = 0.68
FAST = 0.42
ARM = ("Shoulder", "Arm", "Hand", "Finger")


def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root / f"FirstPlayerCapsule_{name}.fbx"))
    return bpy.data.objects["DGN_Armature"]


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def is_arm(name, sides=None):
    if not any(token in name for token in ARM):
        return False
    if sides is None:
        return True
    return any(name.endswith(f".{side}") for side in sides)


def sample(name):
    rig = load(name)
    first, last = map(int, rig.animation_data.action.frame_range)
    keys = bpy.data.objects["Body"].data.shape_keys
    frames = []
    for frame in range(first, last + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
        shapes = {key.name: key.value for key in keys.key_blocks[1:]} if keys else {}
        frames.append((pose, shapes, rig.pose.bones["Hand.L"].head.y))
    return frames


def lerp_pose(frames, time, sides):
    last = len(frames) - 1
    time = max(0.0, min(float(last), time))
    index = int(time)
    frac = time - index
    pose_a = frames[index][0]
    pose_b = frames[min(last, index + 1)][0]
    idle = frames[0][0]
    out = {name: matrix.copy() for name, matrix in pose_a.items()}
    for name in out:
        if not is_arm(name, sides):
            continue
        current = mix(pose_a[name], pose_b[name], frac)
        out[name] = mix(idle[name], current, RAISE)
    return out


def reshape(frames, sides):
    idle_y = frames[0][2]
    peak = max(range(len(frames)), key=lambda i: frames[i][2])
    peak_y = frames[peak][2]
    mid_y = idle_y + 0.5 * (peak_y - idle_y)
    mid = next((i for i in range(peak, len(frames)) if frames[i][2] <= mid_y), peak)
    rest = next((i for i in range(mid, len(frames)) if frames[i][2] <= idle_y + 0.08 * (peak_y - idle_y)), len(frames) - 1)
    fast_end = mid + max(2, int(round((rest - mid) * FAST)))
    out = []
    for i, (pose, shapes, _) in enumerate(frames):
        if i < mid:
            time = float(i)
        elif i <= fast_end:
            t = (i - mid) / max(1, fast_end - mid)
            time = mid + (t * t) * (rest - mid)
        else:
            time = float(rest)
        mixed = lerp_pose(frames, time, sides)
        for name, matrix in pose.items():
            if not is_arm(name, sides):
                mixed[name] = matrix.copy()
        out.append((mixed, shapes))
    print("ARM_REMAP", "sides", sides, "peak", peak, "mid", mid, "rest", rest, "fast_end", fast_end)
    return out


def export(name, frames):
    rig = load("Idle_Breathing_2s")
    keys = bpy.data.objects["Body"].data.shape_keys
    rig.animation_data.action = bpy.data.actions.new(name)
    keys.animation_data_clear()
    last = len(frames) - 1
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
    bpy.ops.export_scene.fbx(
        filepath=str(root / f"FirstPlayerCapsule_{name}.fbx"),
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
    print("EXPORTED", name, last)


jump = reshape(sample("Jump_Cute"), ("L", "R"))
export("Jump_Cute", jump)

carry_idle = sample("Carry_Idle")[0][0]
carry = reshape(sample("Carry_Jump"), ("L",))
for pose, _ in carry:
    for name, matrix in carry_idle.items():
        if is_arm(name, ("R",)):
            pose[name] = matrix.copy()
export("Carry_Jump", carry)
