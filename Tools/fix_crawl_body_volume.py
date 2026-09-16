"""Strengthen the crawl torso corrective and export only affected Unity clips."""
import bpy
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = Path(r'C:\Users\SSAFY\.codex\visualizations\2026\09\14\01a09e00-d780-7f01-a5d2-f47dc854742f\current-bear-all-actions')
DEST = OUT/'crawl-volume-fixed'
DEST.mkdir(exist_ok=True)

bpy.ops.wm.open_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_AllActions.blend'), use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects['DGN_Armature']
body = bpy.data.objects['Body']
meshes = [obj for obj in scene.objects if obj.type == 'MESH']
manifest = json.loads((OUT/'manifest.json').read_text())
affected = [entry for entry in manifest if entry['clip'] == 'Idle' or 'Crawl' in entry['clip']]

keys = body.data.shape_keys
basis = keys.key_blocks['Basis']
waist = keys.key_blocks['Crawl_Waist_Round']
for point, base in zip(waist.data, basis.data):
    point.co = base.co + (point.co - base.co) * 1.0
waist.slider_min = 0
waist.slider_max = 1
waist.value = 0

for entry in affected:
    clip = entry['clip']
    shape_action = bpy.data.actions[clip + '_CurrentBear_Shapes']
    if clip != 'Idle':
        body.data.shape_keys.animation_data_create().action = shape_action
        for frame in range(entry['end'] + 1):
            waist.value = 1.0
            waist.keyframe_insert('value', frame=frame, group='Crawl_Waist_Round')
    for layer in shape_action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for keyframe in curve.keyframe_points:
                        keyframe.interpolation = 'LINEAR'

scene.frame_start = 0
scene.frame_end = 36
scene.frame_set(0)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_CrawlVolume.blend'))

# Bake the render surface once, preserving each corrective target.
rig.data.pose_position = 'REST'
for obj in meshes:
    shape_keys = obj.data.shape_keys
    if shape_keys:
        shape_keys.animation_data_clear()
        for key in shape_keys.key_blocks:
            key.value = 0
    for modifier in obj.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.show_viewport = False
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    result = bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph), preserve_all_data_layers=True, depsgraph=depsgraph)
    shapes = {}
    if shape_keys:
        for key in list(shape_keys.key_blocks)[1:]:
            key.value = 1
            bpy.context.view_layer.update()
            evaluated = obj.evaluated_get(depsgraph)
            mesh = evaluated.to_mesh()
            shapes[key.name] = [vertex.co.copy() for vertex in mesh.vertices]
            evaluated.to_mesh_clear()
            key.value = 0
    obj.data = result
    for modifier in list(obj.modifiers):
        if modifier.type == 'ARMATURE':
            modifier.show_viewport = True
        else:
            obj.modifiers.remove(modifier)
    if shapes:
        obj.shape_key_add(name='Basis')
        for name, coordinates in shapes.items():
            key = obj.shape_key_add(name=name)
            for point, coordinate in zip(key.data, coordinates):
                point.co = coordinate
rig.data.pose_position = 'POSE'

for index, entry in enumerate(affected):
    clip = entry['clip']
    rig.animation_data.action = bpy.data.actions[clip + '_CurrentBear']
    body.data.shape_keys.animation_data_create().action = bpy.data.actions[clip + '_CurrentBear_Shapes']
    body.data.shape_keys.animation_data.action_slot = body.data.shape_keys.animation_data.action.slots[0]
    scene.frame_start, scene.frame_end = 0, entry['end']
    scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    output = DEST/entry['file']
    bpy.ops.export_scene.fbx(filepath=str(output), use_selection=True,
        object_types={'ARMATURE','MESH'}, use_mesh_modifiers=False, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', armature_nodetype='NULL',
        primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
        mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
    print('EXPORTED', index + 1, '/', len(affected), clip, flush=True)

(DEST/'manifest.json').write_text(json.dumps(affected, indent=2))
