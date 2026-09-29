"""Two-hand head carry: natural 만세, soft elbows, hands by the hood.

blender --background --python Tools/make_first_carry_two_hands.py -- FIRST_DIR
"""
import math
import sys
from math import pi, sin
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

args = sys.argv[sys.argv.index("--") + 1 :]
FIRST = Path(args[0])
IDLE = FIRST / "FirstPlayerCapsule_Idle.fbx"
CARRY = FIRST / "FirstPlayerCapsule_Carry_Idle.fbx"
OUT_FBX = FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx"
PREVIEW_DIR = Path(args[1]) if len(args) > 1 else None
CLIP = "Carry_TwoHands"
PIT = "Armpit_Fill"
LAST = 60
FINGERS = (
    "Finger_M1.L",
    "Finger_M2.L",
    "Finger_T1.L",
    "Finger_T2.L",
    "Finger_M1.R",
    "Finger_M2.R",
    "Finger_T1.R",
    "Finger_T2.R",
)

# Character faces +Z, Y up. Right side is -X in this FBX.
# 만세: both segments mostly up, a little out, soft elbow so the mesh does not stretch.
UPPER = {
    "L": Vector((0.46, 0.87, 0.18)),
    "R": Vector((-0.46, 0.87, 0.18)),
}
FOREARM = {
    "L": Vector((0.20, 0.94, 0.14)),
    "R": Vector((-0.20, 0.94, 0.14)),
}
HAND = {
    "L": Vector((0.10, 0.80, 0.20)),
    "R": Vector((-0.10, 0.80, 0.20)),
}


def aim(bone, direction):
    current = bone.tail - bone.head
    if current.length < 1e-8 or direction.length < 1e-8:
        return
    rotation = current.normalized().rotation_difference(direction.normalized())
    loc, _, _ = bone.matrix.decompose()
    bone.matrix = Matrix.LocRotScale(loc, rotation @ bone.matrix.to_quaternion(), Vector((1, 1, 1)))
    bpy.context.view_layer.update()


def twist(bone, radians):
    axis = (bone.tail - bone.head).normalized()
    loc, rot, _ = bone.matrix.decompose()
    bone.matrix = Matrix.LocRotScale(loc, Quaternion(axis, radians) @ rot, Vector((1, 1, 1)))
    bpy.context.view_layer.update()


def key_pose(rig, frame):
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def vertex_weight(vertex, indices):
    return sum(group.weight for group in vertex.groups if group.group in indices)


def build_armpit_shape(body):
    if body.data.shape_keys is None:
        body.shape_key_add(name="Basis")
    keys = body.data.shape_keys
    key = keys.key_blocks.get(PIT) or body.shape_key_add(name=PIT)
    basis = keys.key_blocks[0]
    groups = body.vertex_groups
    arm = {
        groups[name].index
        for name in ("UpperArm.L", "UpperArm.R", "Shoulder.L", "Shoulder.R")
        if name in groups
    }
    torso = {groups[name].index for name in ("Spine", "Hips") if name in groups}
    bases = [basis.data[index].co.copy() for index in range(len(body.data.vertices))]
    samples = []
    for vertex in body.data.vertices:
        base = bases[vertex.index]
        arm_w = vertex_weight(vertex, arm)
        torso_w = vertex_weight(vertex, torso)
        radius = (base.x * base.x + base.z * base.z) ** 0.5
        if torso_w > 0.5 and arm_w < 0.2 and 0.72 < base.y < 1.42:
            samples.append((base.y, radius))

    def torso_radius(y):
        total = 0.0
        weight = 0.0
        for sample_y, sample_r in samples:
            falloff = max(0.0, 1.0 - abs(sample_y - y) / 0.14)
            total += sample_r * falloff
            weight += falloff
        return total / weight if weight > 1e-6 else 0.32

    deltas = [Vector() for _ in bases]
    for vertex in body.data.vertices:
        base = bases[vertex.index]
        x, y, z = base
        if y < 0.84 or y > 1.38:
            continue
        arm_w = vertex_weight(vertex, arm)
        torso_w = vertex_weight(vertex, torso)
        radial = Vector((x, 0.0, z))
        radius = radial.length
        if radius < 1e-5:
            continue
        height = max(0.0, 1.0 - abs(y - 1.14) / 0.28) ** 2
        mix = (max(0.0, arm_w) * max(0.0, torso_w)) ** 0.5
        influence = max(mix * 1.3, torso_w * 0.45, arm_w * 0.2) * height
        if influence < 0.03:
            continue
        target = torso_radius(y) * (1.0 + 0.06 * mix)
        extra = max(0.0, target - radius) * influence
        deltas[vertex.index] = (radial / radius) * extra

    adjacency = [[] for _ in bases]
    for polygon in body.data.polygons:
        verts = list(polygon.vertices)
        for index, a in enumerate(verts):
            b = verts[(index + 1) % len(verts)]
            adjacency[a].append(b)
            adjacency[b].append(a)
    for _ in range(8):
        smoothed = [delta.copy() for delta in deltas]
        for index, neighbors in enumerate(adjacency):
            if not neighbors:
                continue
            average = Vector()
            for neighbor in neighbors:
                average += deltas[neighbor]
            average /= len(neighbors)
            smoothed[index] = deltas[index].lerp(average, 0.6)
        deltas = smoothed

    for index, point in enumerate(key.data):
        point.co = bases[index] + deltas[index]
    return key


def key_shapes(body, frame, belly, fill):
    keys = body.data.shape_keys
    if keys is None:
        return
    for block in keys.key_blocks[1:]:
        if block.name == "Belly_Breath":
            block.value = belly
        elif block.name == PIT:
            block.value = fill
        else:
            block.value = 0.0
        block.keyframe_insert("value", frame=frame)


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


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, ignore_leaf_bones=False)
    return next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")


def pose_arms(rig, fingers, pulse):
    for side, sign in (("L", 1.0), ("R", -1.0)):
        shoulder = rig.pose.bones[f"Shoulder.{side}"]
        loc, rot, _ = shoulder.matrix.decompose()
        shrug = Quaternion((0, 0, 1), sign * 0.08) @ Quaternion((1, 0, 0), -0.26)
        shoulder.matrix = Matrix.LocRotScale(loc, shrug @ rot, Vector((1, 1, 1)))
        bpy.context.view_layer.update()

        upper = rig.pose.bones[f"UpperArm.{side}"]
        lower = rig.pose.bones[f"Arm.{side}"]
        hand = rig.pose.bones[f"Hand.{side}"]
        aim(upper, UPPER[side])
        aim(lower, FOREARM[side])
        aim(hand, HAND[side])
        twist(upper, sign * 0.12)
        twist(lower, -sign * 0.22)
        twist(hand, -sign * 0.95)
        twist(upper, pulse * sign * 0.03)
        twist(lower, pulse * sign * 0.02)

        elbow = (upper.tail - upper.head).angle(lower.tail - lower.head)
        if elbow < math.radians(12):
            raise RuntimeError(f"{side} arm too straight: {math.degrees(elbow):.1f} deg")

    for name, basis in fingers.items():
        bone = rig.pose.bones.get(name)
        if bone is not None:
            bone.matrix_basis = basis
    bpy.context.view_layer.update()


def sample_fingers(path):
    rig = load(path)
    first = int(rig.animation_data.action.frame_range[0])
    bpy.context.scene.frame_set(first)
    bpy.context.view_layer.update()
    held = {name: rig.pose.bones[name].matrix_basis.copy() for name in FINGERS if name in rig.pose.bones}
    # Carry_Idle only poses the right hand; mirror curl onto the left.
    for right, left in (
        ("Finger_M1.R", "Finger_M1.L"),
        ("Finger_M2.R", "Finger_M2.L"),
        ("Finger_T1.R", "Finger_T1.L"),
        ("Finger_T2.R", "Finger_T2.L"),
    ):
        if right in held and left in rig.pose.bones:
            held[left] = held[right].copy()
    return held


fingers = sample_fingers(CARRY)
rig = load(IDLE)
body = bpy.data.objects["Body"]
build_armpit_shape(body)
scene = bpy.context.scene
action = rig.animation_data.action
lo, hi = int(action.frame_range[0]), int(action.frame_range[1])
idle = {}
idle_belly = {}
keys = body.data.shape_keys
for frame in range(lo, hi + 1):
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    idle[frame] = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
    idle_belly[frame] = keys.key_blocks["Belly_Breath"].value if keys and "Belly_Breath" in keys.key_blocks else 0.0

clip = bpy.data.actions.new(CLIP)
clip.use_fake_user = True
rig.animation_data_create().action = clip
if clip.slots:
    rig.animation_data.action_slot = clip.slots[0]
keys.animation_data_clear()
keys.animation_data_create()
keys.animation_data.action = bpy.data.actions.new(f"{CLIP}_shapes")

ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
for offset in range(LAST + 1):
    source_frame = lo + (offset % (hi - lo + 1))
    source = idle[source_frame]
    for bone in ordered:
        bone.matrix_basis = source[bone.name]
    bpy.context.view_layer.update()
    pulse = sin(offset * 2 * pi / LAST)
    pose_arms(rig, fingers, pulse)
    key_pose(rig, offset)
    key_shapes(body, offset, idle_belly[source_frame], 1.0)

# Debug one frame
scene.frame_set(1)
bpy.context.view_layer.update()
for side in ("L", "R"):
    upper = rig.pose.bones[f"UpperArm.{side}"]
    lower = rig.pose.bones[f"Arm.{side}"]
    hand = rig.pose.bones[f"Hand.{side}"]
    elbow = math.degrees((upper.tail - upper.head).angle(lower.tail - lower.head))
    print(
        f"{side} elbow={elbow:.1f} upper_len={(upper.tail - upper.head).length:.3f} "
        f"hand={tuple(round(v, 3) for v in hand.head)}"
    )

export_fbx(rig, OUT_FBX, 0, LAST)

if PREVIEW_DIR is not None:
    PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.render.resolution_x = 800
    scene.render.resolution_y = 800
    cam_data = bpy.data.cameras.new("PreviewCamera")
    cam = bpy.data.objects.new("PreviewCamera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.65
    target = rig.matrix_world @ Vector((0, 1, 0))
    cam.location = rig.matrix_world @ Vector((-2, 1.6, 3))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.frame_set(1)
    bpy.context.view_layer.update()
    keys.key_blocks[PIT].value = 1.0
    scene.render.filepath = str(PREVIEW_DIR / "preview.png")
    bpy.ops.render.render(write_still=True)
    print("PREVIEW", PREVIEW_DIR / "preview.png")
