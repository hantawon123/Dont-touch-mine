"""Rebuild Knocked_Out: hips plant, head rests, feet fall naturally.

Blender --background --python Tools/rebuild_knocked_out.py -- FIRST_DIR
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

first_dir = Path(sys.argv[sys.argv.index("--") + 1])
idle_path = first_dir / "FirstPlayerCapsule_Idle_Breathing_2s.fbx"
out_path = first_dir / "FirstPlayerCapsule_Knocked_Out.fbx"
LAST = 48
FLOOR = 0.07


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def pose_of(rig):
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def mesh_z(name=None, y_min=None, y_max=None):
    deps = bpy.context.evaluated_depsgraph_get()
    lo, hi = 1e9, -1e9
    found = False
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        if name and obj.name != name:
            continue
        ev = obj.evaluated_get(deps)
        mesh = ev.to_mesh()
        for v in mesh.vertices:
            p = obj.matrix_world @ v.co
            if y_min is not None and p.y < y_min:
                continue
            if y_max is not None and p.y > y_max:
                continue
            lo = min(lo, p.z)
            hi = max(hi, p.z)
            found = True
        ev.to_mesh_clear()
    return (lo, hi) if found else (0.0, 0.0)


def world_head(rig, name):
    return rig.matrix_world @ rig.pose.bones[name].head


def rotate_world(rig, bone, axis, degrees):
    rot = Matrix.Rotation(math.radians(degrees), 3, axis)
    axes = bone.matrix.to_3x3()
    local = axes.inverted() @ rot @ axes
    bone.matrix_basis = bone.matrix_basis @ local.to_4x4()
    bpy.context.view_layer.update()


def apply_pose(rig, matrices):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = matrices[bone.name]
    bpy.context.view_layer.update()


def plant_hips(rig, target=FLOOR):
    lo, _ = mesh_z("Body", y_min=0.12, y_max=0.55)
    drop = lo - target
    if abs(drop) < 0.0005:
        return
    hips = rig.pose.bones["Hips"]
    hips.matrix_basis = Matrix.Translation(Vector((0.0, -drop, 0.0))) @ hips.matrix_basis
    bpy.context.view_layer.update()


def rest_head(rig, target=0.05):
    for _ in range(8):
        hood_lo, _ = mesh_z("Hood")
        error = hood_lo - target
        if abs(error) < 0.015:
            break
        step = max(-3.0, min(3.0, error * 18.0))
        rotate_world(rig, rig.pose.bones["Neck"], "X", -step)
        rotate_world(rig, rig.pose.bones["Head"], "X", -step * 0.4)
    plant_hips(rig)


def rest_feet(rig, target=0.08):
    """Small gravity drop only. Large thigh pitch becomes a stiff Matrix pose."""
    for _ in range(10):
        foot_lo, _ = mesh_z("Body", y_max=0.05)
        error = foot_lo - target
        if error <= 0.02:
            break
        step = max(1.5, min(3.5, error * 12.0))
        for side in ("L", "R"):
            rotate_world(rig, rig.pose.bones[f"UpperLeg.{side}"], "X", step)
            rotate_world(rig, rig.pose.bones[f"Leg.{side}"], "X", step * 0.35)
            rotate_world(rig, rig.pose.bones[f"Foot.{side}"], "X", step * 0.2)


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(idle_path))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
bpy.context.scene.frame_set(int(rig.animation_data.action.frame_range[0]))
bpy.context.view_layer.update()
idle = pose_of(rig)
keys = bpy.data.objects["Body"].data.shape_keys

stagger = {name: matrix.copy() for name, matrix in idle.items()}
loc, rot, scale = stagger["Hips"].decompose()
stagger["Hips"] = Matrix.LocRotScale(
    loc + Vector((0.0, 0.0, -0.04)),
    Quaternion((1.0, 0.0, 0.0), math.radians(-18.0)) @ rot,
    scale,
)
loc, rot, scale = stagger["Spine"].decompose()
stagger["Spine"] = Matrix.LocRotScale(
    loc,
    Quaternion((1.0, 0.0, 0.0), math.radians(-8.0)) @ rot,
    scale,
)

slump = {name: matrix.copy() for name, matrix in idle.items()}
loc, rot, scale = slump["Hips"].decompose()
slump["Hips"] = Matrix.LocRotScale(
    loc + Vector((0.0, -0.08, -0.12)),
    Quaternion((1.0, 0.0, 0.0), -math.pi / 2.0) @ rot,
    scale,
)

apply_pose(rig, slump)
plant_hips(rig)
rest_head(rig)
rest_feet(rig)
plant_hips(rig)
slump = pose_of(rig)

print(
    "SLUMP hips", mesh_z("Body", 0.12, 0.55),
    "hood", mesh_z("Hood"),
    "feet", mesh_z("Body", y_max=0.05),
    "footBone", tuple(round(c, 3) for c in world_head(rig, "Foot.L")),
)

points = [(0, idle), (5, idle), (12, stagger), (18, stagger), (32, slump), (48, slump)]
rig.animation_data.action = bpy.data.actions.new("Knocked_Out")
if keys:
    keys.animation_data_clear()

for frame in range(LAST + 1):
    for (fa, a), (fb, b) in zip(points, points[1:]):
        if fa <= frame <= fb:
            t = smooth((frame - fa) / max(1, fb - fa))
            break
    apply_pose(rig, {name: mix(a[name], b[name], t) for name in idle})
    if keys:
        for key in keys.key_blocks[1:]:
            key.value = 0.0
            key.keyframe_insert("value", frame=frame)
    for bone in rig.pose.bones:
        bone.keyframe_insert("location", frame=frame)
        bone.keyframe_insert("rotation_quaternion", frame=frame)
        bone.keyframe_insert("scale", frame=frame)

bpy.context.scene.frame_set(48)
print(
    "HOLD hips", mesh_z("Body", 0.12, 0.55),
    "hood", mesh_z("Hood"),
    "feet", mesh_z("Body", y_max=0.05),
)

scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 0
scene.frame_end = LAST
scene.frame_set(0)
bpy.ops.export_scene.fbx(
    filepath=str(out_path),
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
print("EXPORTED", out_path)
