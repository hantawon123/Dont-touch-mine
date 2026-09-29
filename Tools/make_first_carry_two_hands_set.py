"""Overlay Carry_TwoHands arms onto the same bases as Carry locomotion.

blender --background --python Tools/make_first_carry_two_hands_set.py -- FIRST_DIR
"""
import uuid
from pathlib import Path
import sys

import bpy
from mathutils import Vector

FIRST = Path(sys.argv[sys.argv.index("--") + 1])
HOLD = FIRST / "FirstPlayerCapsule_Carry_TwoHands.fbx"
PIT = "Armpit_Fill"

CLIPS = (
    ("Walk_Forward", "Carry_Walk_Forward", "Carry_TwoHands_Walk_Forward"),
    ("Walk_Back", "Carry_Walk_Back", "Carry_TwoHands_Walk_Back"),
    ("Walk_Left", "Carry_Walk_Left", "Carry_TwoHands_Walk_Left"),
    ("Walk_Right", "Carry_Walk_Right", "Carry_TwoHands_Walk_Right"),
    ("Run_Forward", "Carry_Run_Forward", "Carry_TwoHands_Run_Forward"),
    ("Run_Back", "Carry_Run_Back", "Carry_TwoHands_Run_Back"),
    ("Run_Left", "Carry_Run_Left", "Carry_TwoHands_Run_Left"),
    ("Run_Right", "Carry_Run_Right", "Carry_TwoHands_Run_Right"),
    ("Crouch_Idle", "Carry_Crouch_Idle", "Carry_TwoHands_Crouch_Idle"),
    ("Crouch_Walk_Forward", "Carry_Crouch_Walk_Forward", "Carry_TwoHands_Crouch_Walk_Forward"),
    ("Crouch_Walk_Back", "Carry_Crouch_Walk_Back", "Carry_TwoHands_Crouch_Walk_Back"),
    ("Crouch_Walk_Left", "Carry_Crouch_Walk_Left", "Carry_TwoHands_Crouch_Walk_Left"),
    ("Crouch_Walk_Right", "Carry_Crouch_Walk_Right", "Carry_TwoHands_Crouch_Walk_Right"),
    ("Jump", "Carry_Jump", "Carry_TwoHands_Jump"),
    ("Land", "Carry_Land", "Carry_TwoHands_Land"),
)


def descendants(bone):
    names = [bone.name]
    for child in bone.children:
        names.extend(descendants(child))
    return names


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
    if not source.exists():
        print("MISSING_META", source)
        return
    text = source.read_text(encoding="utf-8")
    old_guid = next(line.split(": ", 1)[1].strip() for line in text.splitlines() if line.startswith("guid:"))
    new_guid = uuid.uuid5(uuid.NAMESPACE_URL, f"first/{clip_name}").hex
    text = text.replace(f"guid: {old_guid}", f"guid: {new_guid}", 1)
    text = text.replace(f"name: {carry_name}", f"name: {clip_name}")
    dest.write_text(text, encoding="utf-8")


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(HOLD), use_anim=True, ignore_leaf_bones=False)
hold_rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
hold_bones = descendants(hold_rig.pose.bones["Shoulder.L"]) + descendants(hold_rig.pose.bones["Shoulder.R"])
hold_bones = list(dict.fromkeys(hold_bones))
frame = int(hold_rig.animation_data.action.frame_range[0])
bpy.context.scene.frame_set(frame)
bpy.context.view_layer.update()
held = {name: hold_rig.pose.bones[name].matrix_basis.copy() for name in hold_bones}
hold_body = bpy.data.objects["Body"]
hold_keys = hold_body.data.shape_keys
basis = hold_keys.key_blocks[0]
pit = hold_keys.key_blocks[PIT]
pit_deltas = [pit.data[i].co - basis.data[i].co for i in range(len(basis.data))]
print("HOLD_BONES", hold_bones)

for source_name, carry_name, clip_name in CLIPS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(
        filepath=str(FIRST / f"FirstPlayerCapsule_{source_name}.fbx"),
        use_anim=True,
        ignore_leaf_bones=False,
    )
    scene = bpy.context.scene
    rig = next(obj for obj in scene.objects if obj.type == "ARMATURE")
    body = bpy.data.objects["Body"]
    if body.data.shape_keys is None:
        body.shape_key_add(name="Basis")
    keys = body.data.shape_keys
    pit_key = keys.key_blocks.get(PIT) or body.shape_key_add(name=PIT)
    if len(pit_key.data) == len(pit_deltas):
        for index, delta in enumerate(pit_deltas):
            pit_key.data[index].co = keys.key_blocks[0].data[index].co + Vector(delta)
    action = rig.animation_data.action
    first, last = int(action.frame_range[0]), int(action.frame_range[1])
    ordered = sorted(rig.pose.bones, key=lambda bone: len(bone.parent_recursive))
    samples = {}
    for frame in range(first, last + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        shapes = {block.name: block.value for block in keys.key_blocks[1:] if block.name != PIT}
        samples[frame] = (
            {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones},
            shapes,
        )

    clip = bpy.data.actions.new(clip_name)
    clip.use_fake_user = True
    rig.animation_data_create().action = clip
    if clip.slots:
        rig.animation_data.action_slot = clip.slots[0]
    keys.animation_data_clear()
    keys.animation_data_create()
    keys.animation_data.action = bpy.data.actions.new(f"{clip_name}_shapes")

    export_first = 0
    for offset, frame in enumerate(range(first, last + 1)):
        pose, shapes = samples[frame]
        for bone in ordered:
            bone.matrix_basis = pose[bone.name]
        bpy.context.view_layer.update()
        for name in hold_bones:
            if name in rig.pose.bones:
                rig.pose.bones[name].matrix_basis = held[name]
        bpy.context.view_layer.update()
        key_pose(rig, offset)
        for block in keys.key_blocks[1:]:
            if block.name == PIT:
                block.value = 1.0
            else:
                block.value = shapes.get(block.name, 0.0)
            block.keyframe_insert("value", frame=offset)
    export_fbx(rig, FIRST / f"FirstPlayerCapsule_{clip_name}.fbx", 0, last - first)
    write_meta(carry_name, clip_name)

print("CARRY_TWO_HANDS_SET", FIRST)
