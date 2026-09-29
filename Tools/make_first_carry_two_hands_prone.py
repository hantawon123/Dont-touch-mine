"""Prone two-hand carry: both arms raised toward the sky.

blender --background --python Tools/make_first_carry_two_hands_prone.py -- FIRST_DIR [PREVIEW_DIR]
"""
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

args = sys.argv[sys.argv.index("--") + 1 :]
FIRST = Path(args[0])
PREVIEW_DIR = Path(args[1]) if len(args) > 1 else None
HOLD = FIRST / "FirstPlayerCapsule_Carry_Idle.fbx"

CLIPS = (
    ("Prone_Idle", "Carry_Prone_Idle", "Carry_TwoHands_Prone_Idle"),
    ("Crawl_Forward", "Carry_Crawl_Forward", "Carry_TwoHands_Crawl_Forward"),
    ("Crawl_Back", "Carry_Crawl_Back", "Carry_TwoHands_Crawl_Back"),
    ("Crawl_Left", "Carry_Crawl_Left", "Carry_TwoHands_Crawl_Left"),
    ("Crawl_Right", "Carry_Crawl_Right", "Carry_TwoHands_Crawl_Right"),
)

FINGERS = (
    "Finger_M1.L", "Finger_M2.L", "Finger_T1.L", "Finger_T2.L",
    "Finger_M1.R", "Finger_M2.R", "Finger_T1.R", "Finger_T2.R",
)

# Prone: Y up, +Z toward the head.
# Upper arm parallel to the floor toward the head; forearm nearly vertical;
# hand follows the forearm more, palm facing the ground.
UPPER = {
    "L": Vector((0.50, 0.18, 0.82)),
    "R": Vector((-0.50, 0.18, 0.82)),
}
FOREARM = {
    "L": Vector((0.16, 0.96, 0.10)),
    "R": Vector((-0.16, 0.96, 0.10)),
}
HAND = {
    "L": Vector((0.16, 0.96, 0.10)),
    "R": Vector((-0.16, 0.96, 0.10)),
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


def twist_axis_toward(bone, local_axis, world_target):
    bone_dir = (bone.tail - bone.head).normalized()
    world_axis = (bone.matrix.to_3x3() @ local_axis).normalized()
    target = world_target.normalized()
    a = world_axis - bone_dir * world_axis.dot(bone_dir)
    b = target - bone_dir * target.dot(bone_dir)
    if a.length < 1e-4 or b.length < 1e-4:
        return
    a.normalize()
    b.normalize()
    angle = a.angle(b)
    if a.cross(b).dot(bone_dir) < 0.0:
        angle = -angle
    angle = max(-0.45, min(0.45, angle))
    twist(bone, angle)


def palm_down(hand):
    best_axis = Vector((1.0, 0.0, 0.0))
    best = 2.0
    for axis in (
        Vector((1.0, 0.0, 0.0)),
        Vector((-1.0, 0.0, 0.0)),
        Vector((0.0, 0.0, 1.0)),
        Vector((0.0, 0.0, -1.0)),
    ):
        world = (hand.matrix.to_3x3() @ axis).normalized()
        if world.y < best:
            best = world.y
            best_axis = axis
    twist_axis_toward(hand, best_axis, Vector((0.0, -1.0, 0.0)))


def descendants(bone):
    names = [bone.name]
    for child in bone.children:
        names.extend(descendants(child))
    return names


def pose_raise(rig, _fingers=None):
    for side, sign in (("L", 1.0), ("R", -1.0)):
        aim(rig.pose.bones[f"UpperArm.{side}"], UPPER[side])
        aim(rig.pose.bones[f"Arm.{side}"], FOREARM[side])
        aim(rig.pose.bones[f"Hand.{side}"], HAND[side])
        palm_down(rig.pose.bones[f"Hand.{side}"])
    for name in FINGERS:
        bone = rig.pose.bones.get(name)
        if bone is not None:
            bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


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


def write_meta(carry_name, clip_name):
    source = FIRST / f"FirstPlayerCapsule_{carry_name}.fbx.meta"
    dest = FIRST / f"FirstPlayerCapsule_{clip_name}.fbx.meta"
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{clip_name}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace(f"name: {carry_name}", f"name: {clip_name}")
    dest.write_text(text, encoding="utf-8")


bpy.ops.wm.read_factory_settings(use_empty=True)
held_arms = None

for source_name, carry_name, clip_name in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FIRST / f"FirstPlayerCapsule_{source_name}.fbx"), use_anim=True)
    scene = bpy.context.scene
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    body = bpy.data.objects.get("Body")
    keys = body.data.shape_keys if body is not None else None
    action = rig.animation_data.action
    first, last = int(action.frame_range[0]), int(action.frame_range[1])
    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    samples = {}
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        shapes = {block.name: block.value for block in keys.key_blocks[1:]} if keys else {}
        samples[frame] = ({bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}, shapes)

    clip = bpy.data.actions.new(clip_name)
    clip.use_fake_user = True
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    if keys is not None:
        keys.animation_data_clear()
        keys.animation_data_create()
        keys.animation_data.action = bpy.data.actions.new(f"{clip_name}_shapes")

    for offset, frame in enumerate(range(first, last + 1)):
        pose, shapes = samples[frame]
        for bone in ordered:
            bone.matrix_basis = pose[bone.name]
        bpy.context.view_layer.update()
        if held_arms is None:
            pose_raise(rig)
            held_arms = {
                name: rig.pose.bones[name].matrix_basis.copy()
                for name in descendants(rig.pose.bones["Shoulder.L"]) + descendants(rig.pose.bones["Shoulder.R"])
                if name in rig.pose.bones
            }
        else:
            for name, basis in held_arms.items():
                if name in rig.pose.bones:
                    rig.pose.bones[name].matrix_basis = basis
            bpy.context.view_layer.update()
        key_pose(rig, offset)
        if keys is not None:
            for block in keys.key_blocks[1:]:
                block.value = 0.0 if block.name == "Armpit_Fill" else shapes.get(block.name, 0.0)
                block.keyframe_insert("value", frame=offset)
    export_fbx(rig, FIRST / f"FirstPlayerCapsule_{clip_name}.fbx", 0, last - first)
    write_meta(carry_name, clip_name)
    if source_name == "Prone_Idle" and PREVIEW_DIR is not None:
        PREVIEW_DIR.mkdir(parents=True, exist_ok=True)
        scene.frame_set(1)
        bpy.context.view_layer.update()
        scene.render.engine = "BLENDER_WORKBENCH"
        scene.display.shading.light = "STUDIO"
        scene.display.shading.color_type = "MATERIAL"
        scene.render.resolution_x = 1000
        scene.render.resolution_y = 560
        cam_data = bpy.data.cameras.new("Side")
        cam = bpy.data.objects.new("Side", cam_data)
        scene.collection.objects.link(cam)
        scene.camera = cam
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = 3.2
        hips = rig.pose.bones["Hips"].head
        cam.location = rig.matrix_world @ Vector((hips.x - 3.2, hips.y + 0.35, hips.z + 0.35))
        target = rig.matrix_world @ Vector((hips.x, hips.y + 0.25, hips.z + 0.55))
        cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(PREVIEW_DIR / "prone_raise.png")
        bpy.ops.render.render(write_still=True)
        print("PREVIEW", PREVIEW_DIR / "prone_raise.png")
