"""Rebuild the CharacterTest First FBXs from a hand-edited Blender character.

Run with Blender --background --python THIS -- CHARACTER.blend OUTPUT_DIRECTORY.
Stages outputs and backs up original FBXs; does not install them into Assets.
"""
import bpy
import hashlib
import json
import math
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CHARACTER, OUT = map(Path, sys.argv[sys.argv.index('--') + 1:])
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = ROOT / 'Assets/Scenes/CharacterTest/First'
BACKUP = OUT / 'originals'
BACKUP.mkdir(exist_ok=True)
STAGED = OUT / 'fbx'
STAGED.mkdir(exist_ok=True)
controller = (SOURCE / 'FirstCharacterPreview.controller').read_text(encoding='utf-8-sig')
states = []
for block in controller.split('--- !u!1102 '):
    name = re.search(r'^  m_Name: (.+)$', block, re.M)
    guid = re.search(r'm_Motion: \{fileID: [-\d]+, guid: ([a-f0-9]+)', block)
    if name and guid:
        states.append((name[1], guid[1]))
assets = []
for meta in sorted(SOURCE.glob('*.fbx.meta')):
    text = meta.read_text()
    guid = re.search(r'^guid: (\w+)', text, re.M)[1]
    mapped = [n for n, g in states if g == guid]
    if not mapped:
        continue
    clip = re.search(r'      name: (.+)', text)[1]
    end = int(float(re.search(r'      lastFrame: ([\d.]+)', text)[1]))
    loop = re.search(r'      loopTime: (\d)', text)[1] == '1'
    path = meta.with_suffix('')
    for p in (path, meta):
        dest = BACKUP / p.name
        if not dest.exists():
            shutil.copy2(p, dest)
    assets.append(dict(file=path.name, clip=clip, end=end, loop=loop, states=mapped))
assert set(g for _, g in states) == set(re.search(r'^guid: (\w+)', (BACKUP/(a['file']+'.meta')).read_text(), re.M)[1] for a in assets)
shutil.copy2(SOURCE/'FirstCharacterPreview.controller', BACKUP/'FirstCharacterPreview.controller')
(OUT/'manifest.json').write_text(json.dumps(assets, indent=2))
bpy.ops.wm.open_mainfile(filepath=str(CHARACTER), use_scripts=False)
for image in bpy.data.images:
    if image.source == 'FILE' and Path(image.filepath).name == 'Color_Texture.png':
        image.filepath = str(ROOT/'Assets/DGN_15_CapsuleAnimals _V2/Textures/Color_Texture.png')
        image.reload()
        image.pack()
scene = bpy.context.scene
rig = bpy.data.objects['DGN_Armature']
body = bpy.data.objects['Body']
meshes = [o for o in scene.objects if o.type == 'MESH']
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
rig.animation_data_clear()
for ob in meshes:
    if ob.data.shape_keys:
        ob.data.shape_keys.animation_data_clear()
        for key in ob.data.shape_keys.key_blocks:
            key.value = 0
names = list(rig.data.bones.keys())
shape_names = [k.name for k in body.data.shape_keys.key_blocks[1:]]
report = []
actions = {}
shape_actions = {}

def linear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points:
                        key.interpolation = 'LINEAR'

def angle(a, b):
    degrees = math.degrees(a.rotation_difference(b).angle)
    return min(degrees, 360-degrees)

for index, asset in enumerate(assets):
    old_objects, old_actions = set(bpy.data.objects), set(bpy.data.actions)
    rig.animation_data_clear()
    body.data.shape_keys.animation_data_clear()
    bpy.ops.import_scene.fbx(filepath=str(BACKUP/asset['file']), use_anim=True)
    imported = set(bpy.data.objects)-old_objects
    src = next(o for o in imported if o.type == 'ARMATURE')
    src_body = next((o for o in imported if o.type == 'MESH' and o.name.startswith('Body')), None)
    assert set(names).issubset(src.data.bones.keys()), asset['clip']
    first, last = map(float, src.animation_data.action.frame_range)
    assert last > first, (asset['clip'], first, last)
    for ob in imported:
        if ob.type == 'MESH':
            for mod in ob.modifiers:
                mod.show_viewport = False
    correction = {}
    for b in rig.data.bones:
        sb = src.data.bones[b.name]
        align = (b.tail_local-b.head_local).normalized().rotation_difference((sb.tail_local-sb.head_local).normalized())
        correction[b.name] = (sb.matrix_local.to_quaternion().inverted() @ align @ b.matrix_local.to_quaternion()).to_matrix().to_4x4()
    samples = {n: [] for n in names}
    shapes = {n: [] for n in shape_names}
    for step in range(asset['end']+1):
        frame = first + (last-first)*step/asset['end']
        scene.frame_set(math.floor(frame), subframe=frame-math.floor(frame))
        bpy.context.view_layer.update()
        desired = {n: src.pose.bones[n].matrix @ correction[n] for n in names}
        for b in rig.data.bones:
            if b.parent:
                localrest = b.parent.matrix_local.inverted() @ b.matrix_local
                basis = localrest.inverted() @ desired[b.parent.name].inverted() @ desired[b.name]
            else:
                basis = b.matrix_local.inverted() @ desired[b.name]
            loc, quat, scale = basis.decompose()
            quat.normalize()
            samples[b.name].append([loc, quat, scale])
        sk = src_body.data.shape_keys if src_body else None
        for n in shape_names:
            shapes[n].append(sk.key_blocks[n].value if sk and n in sk.key_blocks else 0.)
    for ob in imported:
        bpy.data.objects.remove(ob, do_unlink=True)
    for a in set(bpy.data.actions)-old_actions:
        bpy.data.actions.remove(a)
    for datablocks in (bpy.data.meshes, bpy.data.armatures, bpy.data.materials):
        for data in list(datablocks):
            if data.users == 0:
                datablocks.remove(data)
    # Only locomotion cycles get mild distal filtering. Preserve grasping,
    # gestures, impacts and the endpoints of one-shot actions.
    locomotion = asset['loop'] and any(t in asset['clip'] for t in ('Walk', 'Run', 'Crawl'))
    raw_closure = max(angle(row[0][1], row[-1][1]) for row in samples.values())
    for n, rows in samples.items():
        if locomotion and n.startswith(('Hand.', 'Finger_', 'FootToe1.')):
            count = len(rows)-1
            qs = [r[1].copy() for r in rows[:count]]
            filtered = [qs[i].slerp(qs[(i-1)%count].slerp(qs[(i+1)%count], .5), .35) for i in range(count)]
            for row, q in zip(rows, filtered):
                row[1] = q
        if asset['loop']:
            # Distribute any end mismatch over the final 20% of the cycle.
            width = max(3, min(8, asset['end']//5))
            end_loc, end_q, end_s = (v.copy() for v in rows[-1])
            delta_q = end_q.inverted() @ rows[0][1]
            from mathutils import Quaternion
            for j in range(width+1):
                t = j/width
                t = t*t*(3-2*t)
                row = rows[len(rows)-1-width+j]
                row[0] += (rows[0][0]-end_loc)*t
                row[1] = row[1] @ Quaternion().slerp(delta_q, t)
                row[2] += (rows[0][2]-end_s)*t
            rows[-1] = [v.copy() for v in rows[0]]
    if asset['loop']:
        for rows in shapes.values():
            delta = rows[0]-rows[-1]
            width = max(3, min(8, asset['end']//5))
            for j in range(width+1):
                t = j/width
                rows[len(rows)-1-width+j] += delta*t*t*(3-2*t)
            rows[-1] = rows[0]
    action = bpy.data.actions.new(asset['clip']+'_CurrentBear')
    action.use_fake_user = True
    rig.animation_data_create().action = action
    sa = bpy.data.actions.new(asset['clip']+'_CurrentBear_Shapes')
    sa.use_fake_user = True
    body.data.shape_keys.animation_data_create().action = sa
    previous = {}
    for frame in range(asset['end']+1):
        for pb in rig.pose.bones:
            loc, q, scale = samples[pb.name][frame]
            q = q.copy()
            if pb.name in previous and q.dot(previous[pb.name]) < 0:
                q.negate()
            previous[pb.name] = q.copy()
            pb.rotation_mode = 'QUATERNION'
            pb.location, pb.rotation_quaternion, pb.scale = loc, q, scale
            for prop in ('location', 'rotation_quaternion', 'scale'):
                pb.keyframe_insert(prop, frame=frame, group=pb.name)
        for n in shape_names:
            key = body.data.shape_keys.key_blocks[n]
            key.value = shapes[n][frame]
            key.keyframe_insert('value', frame=frame)
    linear(action)
    linear(sa)
    actions[asset['clip']], shape_actions[asset['clip']] = action, sa
    # Validate every baked bone matrix and the complete low-resolution skin.
    modifier_flags = [(m, m.show_viewport) for m in body.modifiers if m.type != 'ARMATURE']
    for m, _ in modifier_flags:
        m.show_viewport = False
    max_coord = 0.
    for frame in range(asset['end']+1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        assert all(math.isfinite(c) for pb in rig.pose.bones for row in pb.matrix for c in row), (asset['clip'], frame)
        ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = ev.to_mesh()
        value = max(abs(c) for v in mesh.vertices for c in v.co)
        assert math.isfinite(value) and value < 20, (asset['clip'], frame, value)
        max_coord = max(max_coord, value)
        ev.to_mesh_clear()
    for m, flag in modifier_flags:
        m.show_viewport = flag
    steps = {n: max(angle(r[i][1], r[i+1][1]) for i in range(len(r)-1)) for n,r in samples.items()}
    report.append(dict(**asset, source_frame_range=[first,last], frames_checked=asset['end']+1, source_closure_degrees=raw_closure,
                       max_bone_step_degrees=max(steps.values()), distal_step_degrees={n:v for n,v in steps.items() if n.startswith(('Hand.','Finger_','FootToe1.'))},
                       max_coordinate=max_coord, shape_ranges={n:[min(v),max(v)] for n,v in shapes.items()}))
    print('BAKED', index+1, '/', len(assets), asset['clip'], flush=True)
    (OUT/'bake-report.json').write_text(json.dumps(report, indent=2))

rig.animation_data.action = actions['Walk_Forward']
body.data.shape_keys.animation_data.action = shape_actions['Walk_Forward']
scene.frame_start = 0
scene.frame_end = 23
scene.render.fps = 30
scene.frame_set(0)
helper = bpy.data.texts.get('preview_walk.py')
if helper:
    helper.use_module = False
    helper.clear()
    helper.write('# Actions are in the Action Editor. Select a matching *_Shapes action on Body shape keys for crouch/crawl.\n')
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'BasicPlayerCapsule_Bear_AllActions.blend'))

# Bake subdivision/surface smoothing into the export mesh, including each
# corrective shape separately, with skinning disabled in rest pose.
rig.data.pose_position = 'REST'
for obj in meshes:
    keys = obj.data.shape_keys
    if keys:
        keys.animation_data_clear()
        for key in keys.key_blocks:
            key.value = 0
    for mod in obj.modifiers:
        if mod.type == 'ARMATURE':
            mod.show_viewport = False
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    result = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    coords = {}
    if keys:
        for key in list(keys.key_blocks)[1:]:
            key.value = 1
            bpy.context.view_layer.update()
            ev = obj.evaluated_get(dg)
            mesh = ev.to_mesh()
            coords[key.name] = [v.co.copy() for v in mesh.vertices]
            ev.to_mesh_clear()
            key.value = 0
    obj.data = result
    for mod in list(obj.modifiers):
        if mod.type == 'ARMATURE':
            mod.show_viewport = True
        else:
            obj.modifiers.remove(mod)
    if coords:
        obj.shape_key_add(name='Basis')
        for name, values in coords.items():
            key = obj.shape_key_add(name=name)
            for p, value in zip(key.data, values):
                p.co = value
rig.data.pose_position = 'POSE'
scene.render.fps = 30
for index, asset in enumerate(assets):
    rig.animation_data.action = actions[asset['clip']]
    # Action slot names are stable across the newly evaluated Key datablock.
    body.data.shape_keys.animation_data_create().action = shape_actions[asset['clip']]
    body.data.shape_keys.animation_data.action_slot = shape_actions[asset['clip']].slots[0]
    scene.frame_start, scene.frame_end = 0, asset['end']
    scene.frame_set(0)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(STAGED/asset['file']), use_selection=True,
        object_types={'ARMATURE','MESH'}, use_mesh_modifiers=False, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1, bake_anim_simplify_factor=0, axis_forward='-Z', axis_up='Y',
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', armature_nodetype='NULL',
        primary_bone_axis='Y', secondary_bone_axis='X', use_armature_deform_only=False,
        mesh_smooth_type='FACE', path_mode='COPY', embed_textures=True)
    print('EXPORTED', index+1, '/', len(assets), asset['file'], flush=True)
(OUT/'complete.json').write_text(json.dumps({'assets':len(assets),'states':len(states),'frames':sum(a['frames_checked'] for a in report)},indent=2))
