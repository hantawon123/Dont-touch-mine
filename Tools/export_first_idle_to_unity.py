"""Export an approved First bake, retaining subdivided skin and shape keys.

Blender --background --python Tools/export_first_idle_to_unity.py -- INPUT.blend OUTPUT.fbx
"""
import sys
from pathlib import Path
import bpy

source, destination = sys.argv[sys.argv.index('--') + 1:]
bpy.ops.wm.open_mainfile(filepath=source, use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects['DGN_Armature']
scene.frame_set(scene.frame_start)
rig.data.pose_position = 'REST'
meshes = [o for o in scene.objects if o.type == 'MESH']
for obj in meshes:
    # FBX cannot apply subdivision on a mesh with shape keys. Evaluate each
    # shape independently with skinning disabled, preserving vertex weights.
    old_keys = obj.data.shape_keys
    shape_action = old_keys.animation_data.action if old_keys and old_keys.animation_data else None
    if old_keys:
        old_keys.animation_data_clear()
        for key in old_keys.key_blocks:
            key.value = 0
    for mod in obj.modifiers:
        if mod.type == 'ARMATURE':
            mod.show_viewport = False
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    result = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
    shapes = {}
    if old_keys:
        for key in list(old_keys.key_blocks)[1:]:
            key.value = 1
            bpy.context.view_layer.update()
            evaluated = obj.evaluated_get(depsgraph).to_mesh()
            shapes[key.name] = [v.co.copy() for v in evaluated.vertices]
            obj.evaluated_get(depsgraph).to_mesh_clear()
            key.value = 0
    obj.data = result
    for mod in list(obj.modifiers):
        if mod.type != 'ARMATURE':
            obj.modifiers.remove(mod)
        else:
            mod.show_viewport = True
    if shapes:
        obj.shape_key_add(name='Basis')
        for name, coords in shapes.items():
            key = obj.shape_key_add(name=name)
            assert len(coords) == len(key.data)
            for vertex, coord in zip(key.data, coords):
                vertex.co = coord
        if shape_action:
            animation = obj.data.shape_keys.animation_data_create()
            animation.action = shape_action
            if shape_action.slots:
                animation.action_slot = shape_action.slots[0]
    print('EXPORT_MESH', obj.name, len(obj.data.vertices), list(shapes))
rig.data.pose_position = 'POSE'
action = rig.animation_data.action
first_frame = int(action.frame_range[0])
last_key = int(action.frame_range[1])
scene.frame_start = first_frame
scene.frame_end = last_key
scene.render.fps = 30
# Unity samples the clip from time zero. Shift the source's first frame to zero.
actions = [action]
shape_keys = bpy.data.objects['Body'].data.shape_keys
if shape_keys and shape_keys.animation_data and shape_keys.animation_data.action:
    actions.append(shape_keys.animation_data.action)
for action in actions:
    for layer in action.layers:
        for strip in layer.strips:
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:
                            key.co.x -= first_frame
                            key.handle_left.x -= first_frame
                            key.handle_right.x -= first_frame
scene.frame_start = 0
scene.frame_end = last_key - first_frame
scene.frame_set(0)
bpy.ops.object.select_all(action='DESELECT')
for obj in [rig, *meshes]:
    obj.hide_set(False)
    obj.select_set(True)
bpy.context.view_layer.objects.active = rig
Path(destination).parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=destination, use_selection=True, object_types={'ARMATURE', 'MESH'},
    use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=True,
    bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
    bake_anim_step=1, bake_anim_simplify_factor=0,
    axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_ALL', armature_nodetype='NULL',
    primary_bone_axis='Y', secondary_bone_axis='X',
    use_armature_deform_only=False, mesh_smooth_type='FACE',
    path_mode='COPY', embed_textures=True,
)
print('EXPORTED', destination)
