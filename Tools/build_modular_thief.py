"""Build a non-destructive First-compatible modeling prototype in Blender 5.x.

blender --background --factory-startup --python Tools/build_modular_thief.py
Outputs stay outside Assets: this is an art review, not a live player replacement.
"""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'source/blender/characters/ModularThief_01'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx'))
rig = bpy.data.objects['DGN_Armature']
body = bpy.data.objects['Body']
scene = bpy.context.scene
rig.data.pose_position = 'REST'
bpy.context.view_layer.update()
rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
parents = {b.name: b.parent.name if b.parent else None for b in rig.data.bones}
idle = rig.animation_data.action
idle.name = 'First_Idle'
idle.use_fake_user = True
idle_shapes = body.data.shape_keys.animation_data.action
idle_shapes.name = 'Shapes_Idle'
idle_shapes.use_fake_user = True
for obj in list(bpy.data.objects):
    if obj.type == 'MESH' and obj != body:
        bpy.data.objects.remove(obj, do_unlink=True)

def material(name, color, roughness=.55, knit=False):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Roughness'].default_value = roughness
    if knit:
        n, links = mat.node_tree.nodes, mat.node_tree.links
        tex = n.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = 115
        tex.inputs['Detail'].default_value = 2
        bump = n.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = .20
        bump.inputs['Distance'].default_value = .013
        links.new(tex.outputs['Fac'], bump.inputs['Height'])
        links.new(bump.outputs['Normal'], p.inputs['Normal'])
    return mat

charcoal = material('Body_Charcoal', (.065,.058,.073))
purple = material('Hood_Lavender', (.40,.205,.59), .82, True)
backpack_mat = material('Backpack_Violet', (.29,.10,.52), .88, True)
inner = material('Hood_InnerEar', (.24,.105,.37), .8, True)
pink = material('Feet_Rose', (.70,.205,.34), .6)
sole = material('Feet_Sole', (.34,.075,.17), .65)
white = material('Eyes_Cream', (.94,.88,.70), .34)
pupil = material('Eyes_Purple', (.11,.025,.20), .26)
mouthmat = material('Mouth_Rose', (.80,.29,.40), .45)

# Change each key through the same smooth spatial map: preserve corrective deltas,
# bone weights and the key names consumed by existing Unity clips.
to_rig = rig.matrix_world.inverted() @ body.matrix_world
from_rig = to_rig.inverted()
for key in body.data.shape_keys.key_blocks:
    for vert in key.data:
        p = to_rig @ vert.co
        x,y,z = p
        torso = math.exp(-((y-.79)/.36)**4) * math.exp(-(abs(x)/.36)**8)
        # Turn the authored capsule into a plush, almost spherical pear:
        # fuller belly, rounded sides and a soft shoulder taper.
        p.x *= 1 + .34*torso
        p.z = z*(1 + .24*torso) + .085*torso
        shoulder = max(0, min(1, (y-.88)/.22))
        p.x *= 1 - .20*shoulder
        hip = max(0, min(1, (.60-y)/.20))
        p.x *= 1 - .10*hip
        # Short, plush limbs: keep their joint centers but give the arms and
        # lower legs the chunky silhouette of the reference character.
        limb = max(0, min(1, (abs(x)-.34)/.28)) * max(0, min(1, (y-.38)/.62))
        if limb > 0:
            side_center = .46 if x >= 0 else -.46
            p.x = side_center + (p.x-side_center)*1.12
            p.z *= 1.16
        leg = max(0, min(1, (.62-y)/.34)) * max(0, min(1, (.34-abs(x))/.20))
        p.x *= 1 + .14*leg
        p.z *= 1 + .12*leg
        # Match the concept sheet's compact pear body while keeping the First
        # joints untouched for animation compatibility.
        p.y = .46 + (p.y - .46) * .84
        head = max(0, min(1, (y-1.20)/.18))
        p.x *= 1 + .20*head
        # The hood owns the silhouette above the shoulders; tuck the authored
        # head volume inside it so no gray collar ring is visible.
        if p.y > .98:
            p.x *= .76
            p.z *= .76
        neck_blend = max(0, min(1, (y-1.02)/.30))
        p.y -= .16*neck_blend
        ankle=max(0,min(1,(.28-y)/.12))
        cx=.157 if x>0 else -.157
        p.x=cx+(p.x-cx)*(1-.18*ankle)
        p.z=.036+(p.z-.036)*(1-.12*ankle)
        vert.co = from_rig @ p
# bmesh uses Mesh.vertices for the Basis layer; keep it in sync before cutting.
for v,k in zip(body.data.vertices,body.data.shape_keys.key_blocks['Basis'].data):
    v.co=k.co
# Remove original feet under the independent slippers. bmesh preserves shape layers.
bm = bmesh.new()
bm.from_mesh(body.data)
cut = [v for v in bm.verts if (to_rig @ v.co).y < .225]
bmesh.ops.delete(bm, geom=cut, context='VERTS')
bm.to_mesh(body.data)
bm.free()
# Light relaxation removes imported surface ripples, identically for every key.
neighbors=[set() for v in body.data.vertices]
for edge in body.data.edges:
    a,b=edge.vertices; neighbors[a].add(b); neighbors[b].add(a)
for key in body.data.shape_keys.key_blocks:
    for iteration in range(3):
        positions=[v.co.copy() for v in key.data]
        for i,v in enumerate(key.data):
            if neighbors[i]:
                average=sum((positions[j] for j in neighbors[i]),Vector())/len(neighbors[i])
                v.co=positions[i].lerp(average,.28)
for v,k in zip(body.data.vertices,body.data.shape_keys.key_blocks['Basis'].data):
    v.co=k.co
body.data.materials.clear()
body.data.materials.append(charcoal)
for poly in body.data.polygons:
    poly.material_index = 0
    poly.use_smooth = True
body['customization_slot'] = 'Body'
body['notes'] = 'First topology above ankles and all 6 corrective keys retained; widened torso/head.'

def mesh(name, verts, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    data.materials.append(mat)
    for p in data.polygons:
        p.use_smooth = True
    return obj

def skin(obj, bone):
    g = obj.vertex_groups.new(name=bone)
    g.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    mod = obj.modifiers.new('First_Rig', 'ARMATURE')
    mod.object = rig
    return obj

def uv_shape(name, center, radius, mat, bone, flatten=None):
    verts, faces = [], []
    nu,nv = 40,24
    for j in range(nv+1):
        t = math.pi*j/nv
        for i in range(nu):
            a = 2*math.pi*i/nu
            p = Vector((center[0]+radius[0]*math.sin(t)*math.cos(a),
                        center[1]+radius[1]*math.cos(t),
                        center[2]+radius[2]*math.sin(t)*math.sin(a)))
            if flatten is not None:
                p.y = max(p.y, flatten)
            verts.append(p)
    for j in range(nv):
        for i in range(nu):
            a=j*nu+i; b=j*nu+(i+1)%nu
            faces.append((a,b,b+nu,a+nu))
    obj = mesh(name,verts,faces,mat)
    # Recalculate consistent normals including the UV sphere poles.
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    return skin(obj,bone)

def join(parts,name):
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    parts[0].name=name
    return parts[0]

# Hood cross sections: face opening -> thick soft edge -> crown -> closed back.
rings=[(.340,.275,.335),(.351,.289,.350),(.380,.316,.341),
       (.421,.357,.272),(.463,.396,.135),(.473,.407,-.025),
       (.438,.383,-.190),(.345,.307,-.310),(.198,.184,-.372),(.001,.001,-.395)]
verts=[]; faces=[]; n=80; cy=1.635
for rx,ry,z in rings:
    for i in range(n):
        a=2*math.pi*i/n
        verts.append((rx*math.cos(a),cy+ry*math.sin(a),z))
for j in range(len(rings)-1):
    for i in range(n):
        a=j*n+i; b=j*n+(i+1)%n
        faces.append((a,b,b+n,a+n))
hood=mesh('Hood',verts,faces,purple)
# The concept uses a large, nearly spherical hood. Enlarge the shell vertically
# so it completely covers the authored head instead of leaving a gray cap above.
for v in hood.data.vertices:
    v.co.x *= 1.06
    v.co.y = 1.635 + (v.co.y - 1.635) * 1.24
bm=bmesh.new(); bm.from_mesh(hood.data)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(hood.data); bm.free()
bpy.context.view_layer.objects.active=hood
hood.select_set(True)
sub=hood.modifiers.new('Soft_Silhouette','SUBSURF'); sub.levels=2
bpy.ops.object.modifier_apply(modifier=sub.name)
solid=hood.modifiers.new('Fabric_Thickness','SOLIDIFY'); solid.thickness=.012
bpy.ops.object.modifier_apply(modifier=solid.name)
skin(hood,'Head')
hood_shell_vertex_count=len(hood.data.vertices)
ears=[]
for sign in [-1,1]:
    ears.append(uv_shape('Ear', (sign*.32,1.996,-.025),(.145,.154,.09),purple,'Head'))
    ears.append(uv_shape('EarInset',(sign*.32,2.000,.050),(.096,.100,.023),inner,'Head'))
hood=join([hood,*ears],'Hood')
hood['customization_slot']='Hood'

for side,sign in [('L',-1),('R',1)]:
    eye=uv_shape('Eye_White_'+side,(sign*.143,1.635,.336),(.130,.153,.073),white,'Head')
    eye['customization_slot']='Eyes'
    eye=uv_shape('Eye_Pupil_'+side,(sign*.132,1.629,.403),(.043,.055,.021),pupil,'Head')
    eye['customization_slot']='Eyes'
    glint=uv_shape('Eye_Glint_'+side,(sign*.132-.011,1.650,.423),(.011,.014,.006),white,'Head')
    glint['customization_slot']='Eyes'

def mouth_curve(name,points):
    curve=bpy.data.curves.new(name,'CURVE'); curve.dimensions='3D'
    curve.bevel_depth=.010; curve.bevel_resolution=4
    s=curve.splines.new('BEZIER'); s.bezier_points.add(len(points)-1)
    for p,co in zip(s.bezier_points,points):
        p.co=co; p.handle_left_type='AUTO'; p.handle_right_type='AUTO'
    obj=bpy.data.objects.new(name,curve); scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.context.view_layer.objects.active=obj; bpy.ops.object.convert(target='MESH')
    obj.parent=rig; obj.matrix_parent_inverse=Matrix.Identity(4)
    obj.data.materials.append(mouthmat)
    skin(obj,'Head'); obj['customization_slot']='Mouth'
    return obj

mouth_curve('Mouth_Smile',[(-.075,1.453,.357),(-.038,1.430,.372),(0,1.423,.377),(.038,1.430,.372),(.075,1.453,.357)])
for side,sign in [('L',1),('R',-1)]:
    shoe=uv_shape('Feet_'+side,(sign*.157,.099,.073),(.148,.119,.221),pink,'Foot.'+side, .014)
    shoe['customization_slot']='Feet'
    bottom=uv_shape('Sole_'+side,(sign*.157,.030,.073),(.147,.036,.219),sole,'Foot.'+side,.010)
    shoe=join([shoe,bottom],'Feet_'+side)

# Lower the complete head costume over the neck, with bones untouched.
for obj in [o for o in scene.objects if o.type=='MESH' and o!=body]:
    if obj.get('customization_slot') in {'Hood','Eyes','Mouth'}:
        for v in obj.data.vertices: v.co.y-=.16

# Rounded ankle cuff conceals the body / replaceable slipper seam during flexion.
for side,sign in [('L',1),('R',-1)]:
    verts=[]; faces=[]; nu,nv=48,12
    for i in range(nu):
        a=2*math.pi*i/nu
        for j in range(nv):
            b=2*math.pi*j/nv
            verts.append((sign*.157+(.119+.022*math.cos(b))*math.cos(a),
                          .176+.028*math.sin(b), .036+(.130+.022*math.cos(b))*math.sin(a)))
    for i in range(nu):
        for j in range(nv):
            faces.append((i*nv+j,((i+1)%nu)*nv+j,((i+1)%nu)*nv+(j+1)%nv,i*nv+(j+1)%nv))
    cuff=skin(mesh('Cuff_'+side,verts,faces,pink),'Foot.'+side)
    join([bpy.data.objects['Feet_'+side],cuff],'Feet_'+side)

# Large soft backpack from the reference image. It is an independent accessory
# skinned to Spine, so it follows crouch/crawl/carry actions without changing
# the First armature or the Body corrective keys.
backpack = uv_shape('Backpack', (.18, .98, -.36), (.43, .46, .30), backpack_mat, 'Spine')
backpack['customization_slot'] = 'Body'
backpack['part_role'] = 'Accessory_Backpack'
# Slightly squash the bottom so it sits against the round torso.
for v in backpack.data.vertices:
    if v.co.y < .70:
        v.co.y = .70 + (v.co.y-.70)*.72

# Independent modeling controls, all neutral at export. Existing Body animation
# controls keep their original names and can run alongside these custom shapes.
custom_key=body.shape_key_add(name='Custom_Belly')
for v in custom_key.data:
    p=to_rig@v.co
    amount=math.exp(-((p.y-.75)/.26)**4)*math.exp(-(abs(p.x)/.42)**8)
    p.x*=1+.10*amount; p.z*=1+.08*amount
    v.co=from_rig@p
for obj in [o for o in scene.objects if o.type=='MESH' and o!=body]:
    obj.shape_key_add(name='Basis')
    slot=obj.get('customization_slot')
    if slot=='Hood':
        key=obj.shape_key_add(name='Custom_SmallEars')
        for i,v in enumerate(key.data):
            if i>=hood_shell_vertex_count:
                center=Vector((.32 if v.co.x>0 else -.32,1.996-.16,-.025))
                v.co=center+(v.co-center)*.68
    elif slot=='Eyes':
        key=obj.shape_key_add(name='Custom_EyeSize')
        center=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
        for v in key.data:
            delta=v.co-center; delta.x*=1.12; delta.y*=1.12
            v.co=center+delta
    elif slot=='Mouth':
        key=obj.shape_key_add(name='Custom_WideSmile')
        for v in key.data: v.co.x*=1.25
    elif slot=='Feet':
        key=obj.shape_key_add(name='Custom_RoundFeet')
        cx=.157 if obj.name.endswith('_L') else -.157
        for v in key.data:
            v.co.x=cx+(v.co.x-cx)*1.10
            v.co.z=.036+(v.co.z-.036)*.85
    elif slot=='Body' and obj != body:
        key=obj.shape_key_add(name='Custom_BackpackScale')
        center=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
        for v in key.data:
            v.co=center+(v.co-center)*1.08
if backpack.data.shape_keys is None or len(backpack.data.shape_keys.key_blocks) < 2:
    key=backpack.shape_key_add(name='Custom_BackpackScale')
    center=sum((v.co for v in backpack.data.vertices),Vector())/len(backpack.data.vertices)
    for v in key.data:
        v.co=center+(v.co-center)*1.08

parts=[o for o in scene.objects if o.type=='MESH']
base_objects=set(bpy.data.objects)
clips={'Idle':(idle,idle_shapes)}
report={'rig':'DGN_Armature','bones':len(rest),'checks':[],'limitations':[
    'Blender prototype; Unity runtime and wardrobe integration not executed.',
    'Procedural Blender bump materials need baking for Unity.',
    'Corrective keys retained and deformed with the surface; final pose sculpting may still be needed.',
    'Body proportions remain constrained by First joint positions; this is not the exact short-limbed concept silhouette.'
]}
for name in ['Walk_Forward','Run_Forward','Jump','Land','Crouch_Idle','Crawl_Forward',
             'Carry_TwoHands','Carry_TwoHands_Crouch_Idle','Throw_TwoHands','Punch','Stun_Idle']:
    bpy.ops.import_scene.fbx(filepath=str(ROOT / f'Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_{name}.fbx'))
    imported=set(bpy.data.objects)-base_objects
    src=next(o for o in imported if o.type=='ARMATURE')
    assert set(rest)==set(b.name for b in src.data.bones), name
    error=max(abs(rest[b.name][i][j]-b.matrix_local[i][j]) for b in src.data.bones for i in range(4) for j in range(4))
    assert error<.0001, (name,error)
    assert all(parents[b.name]==(b.parent.name if b.parent else None) for b in src.data.bones)
    action=src.animation_data.action; action.name='First_'+name; action.use_fake_user=True
    shapes=None
    for obj in imported:
        if obj.type=='MESH' and obj.data.shape_keys and obj.data.shape_keys.animation_data:
            shapes=obj.data.shape_keys.animation_data.action
            if shapes: shapes.name='Shapes_'+name; shapes.use_fake_user=True
    clips[name]=(action,shapes)
    report['checks'].append({'clip':name,'rest_matrix_max_error':error})
    for obj in imported: bpy.data.objects.remove(obj,do_unlink=True)

def activate(name,frame=None):
    a,k=clips[name]
    rig.animation_data.action=a
    if a.slots: rig.animation_data.action_slot=a.slots[0]
    keys=body.data.shape_keys
    keys.animation_data_clear()
    for key in keys.key_blocks: key.value=0
    if k:
        keys.animation_data_create().action=k
        if k.slots: keys.animation_data.action_slot=k.slots[0]
    scene.frame_start=int(a.frame_range[0]); scene.frame_end=int(a.frame_range[1])
    scene.frame_set(frame if frame is not None else scene.frame_start)
    bpy.context.view_layer.update()

rig.data.pose_position='POSE'
for name,(a,k) in clips.items():
    activate(name)
    for f in range(int(a.frame_range[0]),int(a.frame_range[1])+1):
        scene.frame_set(f); bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get()
        for obj in parts:
            evaluated=obj.evaluated_get(deps)
            assert all(math.isfinite(v) for row in evaluated.matrix_world for v in row)
    print('CHECKED',name, list(a.frame_range))
report['sampled_clips']=list(clips)
report['parts']={o.name:o.get('customization_slot','') for o in parts}
report['shape_keys']=[k.name for k in body.data.shape_keys.key_blocks]
activate('Idle')

# Self-contained action picker, saved inside the blend for Blender's Text Editor.
picker=bpy.data.texts.new('SELECT_ACTION.py')
picker.write('''# Change CLIP, then Run Script. The matching corrective animation is selected too.
import bpy
CLIP = "Idle"
r = bpy.data.objects["DGN_Armature"]
k = bpy.data.objects["Body"].data.shape_keys
a = bpy.data.actions["First_" + CLIP]
r.animation_data_create().action = a
if a.slots: r.animation_data.action_slot = a.slots[0]
k.animation_data_clear()
for key in k.key_blocks: key.value = 0
s = bpy.data.actions.get("Shapes_" + CLIP)
if s:
    k.animation_data_create().action = s
    if s.slots: k.animation_data.action_slot = s.slots[0]
bpy.context.scene.frame_start = int(a.frame_range[0])
bpy.context.scene.frame_end = int(a.frame_range[1])
bpy.context.scene.frame_set(int(a.frame_range[0]))
''')

# Export only the editable character and Idle, never the studio props.
bpy.ops.object.select_all(action='DESELECT')
for obj in [rig,*parts]: obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'ModularThief_01_Idle.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},use_mesh_modifiers=False,add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,
    bake_anim_step=1,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',armature_nodetype='NULL',
    primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,
    mesh_smooth_type='FACE',path_mode='AUTO')

# A real Blender studio render rather than a generated concept image.
scene.render.engine='CYCLES'
scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1000; scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.world.color=(.22,.22,.22)
scene.view_settings.view_transform='AgX'
floor_mat=material('Studio_Ivory',(.72,.68,.62),.85)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.024))
floor=bpy.context.object; floor.name='Studio_Floor'; floor.data.materials.append(floor_mat)
def aim(obj,target): obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
for name,location,energy,size in [('Key',(-3,-4,6),550,4),('Fill',(3,-2,3),260,3),('Rim',(1,3,5),650,3)]:
    data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.shape='DISK'; data.size=size
    o=bpy.data.objects.new(name,data); scene.collection.objects.link(o); o.location=location; aim(o,(0,0,1))
cam_data=bpy.data.cameras.new('Review_Camera'); cam=bpy.data.objects.new('Review_Camera',cam_data)
scene.collection.objects.link(cam); scene.camera=cam; cam_data.type='ORTHO'; cam_data.ortho_scale=2.60
cam.location=(3,-7,3.0); aim(cam,(0,0,1.08))
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(OUT/'preview-hero.png')
scene.render.film_transparent=False
scene.render.fps=30
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
rig.show_in_front=True
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ModularThief_01.blend'))
(OUT/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.render.render(write_still=True)
for name,frame in [('Crouch_Idle',1),('Crawl_Forward',10),('Carry_TwoHands',1)]:
    activate(name,frame)
    cam.location=(3,-7,3.0); aim(cam,(0,0,.95)); cam_data.ortho_scale=2.65
    scene.render.filepath=str(OUT/f'preview-{name}.png')
    bpy.ops.render.render(write_still=True)
print('DONE',OUT)
