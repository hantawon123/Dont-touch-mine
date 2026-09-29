"""Bake Stun_End: get up from the Knocked_Out slump into Idle.

Start pose is the last Knocked_Out frame. The character sits up on the
floor, then pushes into a squat and stands. Hip location stays down
until the sit finishes so the back does not hover.

blender --background --python Tools/make_first_stun_end.py -- FIRST_DIR
"""
import sys
from pathlib import Path

import bpy
from mathutils import Matrix

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
LAST = 36
NAME = "Stun_End"


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def pose_at(rig, frame):
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    return {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}


def sample_last(path):
    rig = load(path)
    last = int(rig.animation_data.action.frame_range[1])
    return pose_at(rig, last)


def sample_first(path):
    rig = load(path)
    first = int(rig.animation_data.action.frame_range[0])
    return pose_at(rig, first)


def mix(a, b, t):
    a_loc, a_rot, a_scale = a.decompose()
    b_loc, b_rot, b_scale = b.decompose()
    return Matrix.LocRotScale(a_loc.lerp(b_loc, t), a_rot.slerp(b_rot, t), a_scale.lerp(b_scale, t))


def mix_pose(a, b, t, extra=None):
    out = {name: mix(a[name], b[name], t) for name in a}
    if extra:
        for name, weight in extra.items():
            if name in a and name in b:
                out[name] = mix(a[name], b[name], weight)
    return out


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3.0 - 2.0 * t)


def with_hip_location(pose, source):
    out = {name: matrix.copy() for name, matrix in pose.items()}
    loc, rot, scale = out["Hips"].decompose()
    source_loc, _, _ = source["Hips"].decompose()
    out["Hips"] = Matrix.LocRotScale(source_loc.copy(), rot, scale)
    return out


def mesh_min_y(rig, groups=None, min_weight=0.35):
    deps = bpy.context.evaluated_depsgraph_get()
    body = bpy.data.objects["Body"]
    ev = body.evaluated_get(deps)
    ev_mesh = ev.to_mesh()
    local = rig.matrix_world.inverted() @ body.matrix_world
    if not groups:
        minimum = min((local @ vert.co).y for vert in ev_mesh.vertices)
        ev.to_mesh_clear()
        return minimum
    indices = {body.vertex_groups[name].index for name in groups if name in body.vertex_groups}
    minimum = 100.0
    for index, vert in enumerate(body.data.vertices):
        weight = sum(group.weight for group in vert.groups if group.group in indices)
        if weight >= min_weight:
            minimum = min(minimum, (local @ ev_mesh.vertices[index].co).y)
    ev.to_mesh_clear()
    return minimum


def apply_pose(rig, source):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.matrix_basis = source[bone.name]
    bpy.context.view_layer.update()


def ground(rig, pin_torso, pin_feet=False):
    if pin_torso:
        minimum = mesh_min_y(rig, ("Hips", "Spine", "Chest"))
    else:
        minimum = mesh_min_y(rig)
        if not pin_feet and minimum >= 0.0:
            return
    rig.pose.bones["Hips"].location.y -= minimum
    bpy.context.view_layer.update()


slump = sample_last(FIRST / "FirstPlayerCapsule_Stun_Start.fbx")
crouch = sample_first(FIRST / "FirstPlayerCapsule_Crouch_Idle.fbx")
idle = sample_first(FIRST / "FirstPlayerCapsule_Idle.fbx")

arm_names = [name for name in slump if any(part in name for part in ("Shoulder", "Arm", "Hand", "Finger"))]
head_names = [name for name in slump if name in ("Neck", "Head")]
leg_names = [name for name in slump if any(part in name for part in ("UpperLeg", "Leg", "Foot", "Toe"))]
sit = with_hip_location(
    mix_pose(
        slump,
        crouch,
        0.40,
        {
            **{name: 0.70 for name in arm_names + head_names},
            **{name: 0.88 for name in leg_names},
        },
    ),
    slump,
)
rise = mix_pose(slump, crouch, 0.94)

points = [
    (0, slump),
    (6, slump),
    (18, sit),
    (26, rise),
    (LAST, idle),
]
SIT_END = 18

rig = load(FIRST / "FirstPlayerCapsule_Idle.fbx")
keys = bpy.data.objects["Body"].data.shape_keys
rig.animation_data.action = bpy.data.actions.new(NAME)
keys.animation_data_clear()

for frame in range(LAST + 1):
    for (start, a), (end, b) in zip(points, points[1:]):
        if start <= frame <= end:
            t = smooth((frame - start) / (end - start))
            break
    current = {name: mix(a[name], b[name], t) for name in a}
    if frame <= SIT_END:
        current = with_hip_location(current, slump)
    apply_pose(rig, current)
    ground(rig, pin_torso=frame <= SIT_END, pin_feet=SIT_END < frame <= 26)
    for bone in rig.pose.bones:
        for prop in ("location", "rotation_quaternion", "scale"):
            bone.keyframe_insert(prop, frame=frame)
    for key in keys.key_blocks[1:]:
        key.value = 0.0
        key.keyframe_insert("value", frame=frame)

scene = bpy.context.scene
scene.render.fps = 30
scene.frame_start = 0
scene.frame_end = LAST
scene.frame_set(0)
bpy.ops.export_scene.fbx(
    filepath=str(FIRST / f"FirstPlayerCapsule_{NAME}.fbx"),
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
print("BAKED", NAME, LAST)
