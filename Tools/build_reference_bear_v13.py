import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'source/blender/characters/ReferenceBear_V13'
SOURCE=Path('C:/Users/SSAFY/Downloads/V12_session_before_v13.blend')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'source/blender/characters/ModularThief_01/ModularThief_01.blend'),use_scripts=False)
s=bpy.context.scene
rig=bpy.data.objects['DGN_Armature']; old=bpy.data.objects['Body']
s.frame_set(1); bpy.context.view_layer.update()
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
for o in list(bpy.data.objects):
    if o.type=='MESH' and o!=old or o.type in {'LIGHT','CAMERA'}:
        bpy.data.objects.remove(o,do_unlink=True)
# Reuse the supplied sculpt, keeping its authored joint weights.
with bpy.data.libraries.load(str(SOURCE),link=False) as (a,b):
    b.objects=['Bear_Body']
body=b.objects[0]; s.collection.objects.link(body)
body.parent=rig; body.matrix_parent_inverse=Matrix.Identity(4); body.matrix_basis=Matrix.Identity(4)
for m in list(body.modifiers): body.modifiers.remove(m)
# Fit the source sculpt to the existing First Idle pose without changing the skeleton.
skinm={b.name:rig.pose.bones[b.name].matrix @ b.matrix_local.inverted() for b in rig.data.bones}
for v in body.data.vertices:
    p=v.co.copy(); p.x*=1.13; p.y*=1.24
    z=p.z; p.z=.15+(z-.15)*.62 if z<.65 else .46+(z-.65)*.96
    belly=math.exp(-((p.z-.73)/.29)**4)*math.exp(-(abs(p.x)/.32)**8); p.x*=1+.23*belly; p.y*=1+.16*belly
    if p.z<.43: p.x+=.04*(1 if p.x>0 else -1)
    target=rig.matrix_world.inverted() @ p
    weights=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups if body.vertex_groups[g.group].name in skinm]
    total=sum(w for n,w in weights)
    if total:
        mat=sum((skinm[n]*(w/total) for n,w in weights),Matrix(((0,0,0,0),)*4))
        v.co=mat.inverted_safe()@target
    else: v.co=target
body.shape_key_add(name='Basis')
# Transfer the existing authored corrective deltas to the supplied body surface.
T=rig.matrix_world.inverted()@old.matrix_world
kd=KDTree(len(old.data.vertices))
for i,v in enumerate(old.data.shape_keys.key_blocks[0].data): kd.insert(T@v.co,i)
kd.balance()
near=[kd.find(v.co)[1] for v in body.data.vertices]
for src in list(old.data.shape_keys.key_blocks)[1:]:
    k=body.shape_key_add(name=src.name)
    for i,v in enumerate(k.data): v.co+=T.to_3x3()@(src.data[near[i]].co-old.data.shape_keys.key_blocks[0].data[near[i]].co)
if old.data.shape_keys.animation_data and old.data.shape_keys.animation_data.action:
    a=old.data.shape_keys.animation_data.action; body.data.shape_keys.animation_data_create().action=a
    if a.slots: body.data.shape_keys.animation_data.action_slot=a.slots[0]
bpy.data.objects.remove(old,do_unlink=True); body.name='Body'
mod=body.modifiers.new('First skeleton','ARMATURE'); mod.object=rig
sub=body.modifiers.new('Soft body surface','SUBSURF'); sub.levels=1
body['customization_slot']='Body'
# Shared modeling utilities from the existing First builder, no scene initialization.
code=(ROOT/'Tools/build_modular_thief.py').read_text(encoding='utf-8')
scene=s
exec(code[code.index('def material('):code.index('charcoal = material')])
exec(code[code.index('def mesh('):code.index('# Hood cross sections:')])
charcoal=material('V13 | Charcoal fleece',(.037,.030,.028),.86)
purple=material('V13 | Lavender plush',(.39,.15,.49),.92,True)
inner=material('V13 | Ear shadow',(.205,.083,.275),.94,True)
white=material('V13 | Warm cream eyes',(.91,.825,.65),.48)
black=material('V13 | Black pupils',(.008,.006,.006),.64)
pink=material('V13 | Rose shoes',(.65,.155,.225),.48)
sole=material('V13 | Dark soles',(.048,.026,.031),.82)
mouthmat=material('V13 | Rose smile',(.64,.20,.265),.62)
body.data.materials.clear(); body.data.materials.append(charcoal)
for p in body.data.polygons:p.use_smooth=True;p.material_index=0
# Round hood with a shaped forehead notch and softly squared face opening.
verts=[];faces=[];N=96;cy=1.46
rings=[(.305,.258,.335),(.313,.266,.351),(.335,.289,.351),(.372,.335,.306),(.409,.390,.205),(.430,.425,.035),(.405,.403,-.155),(.320,.335,-.292),(.175,.210,-.365),(.001,.001,-.39)]
for j,(rx,ry,z) in enumerate(rings):
    for i in range(N):
        a=2*math.pi*i/N;co=math.cos(a);si=math.sin(a)
        e=.77 if j<4 else 1
        x=rx*math.copysign(abs(co)**e,co); y=cy+ry*math.copysign(abs(si)**e,si)
        if si>0: y-=.045*math.exp(-(x/.077)**2)*max(0,1-j/5)
        verts.append((x,y,z))
for j in range(len(rings)-1):
    for i in range(N): a=j*N+i;b=j*N+(i+1)%N;faces.append((a,b,b+N,a+N))
hood=mesh('Hood_Bear',verts,faces,purple)
bpy.ops.object.select_all(action='DESELECT');hood.select_set(True);bpy.context.view_layer.objects.active=hood
sub=hood.modifiers.new('Rounded plush shell','SUBSURF');sub.levels=2;bpy.ops.object.modifier_apply(modifier=sub.name)
sol=hood.modifiers.new('Fabric thickness','SOLIDIFY');sol.thickness=.018;bpy.ops.object.modifier_apply(modifier=sol.name)
skin(hood,'Head');hood['customization_slot']='Hood'
ears=[]
for sign in [-1,1]:
    ears.append(uv_shape('Ear',(sign*.302,1.876,.004),(.136,.151,.080),purple,'Head'))
    ears.append(uv_shape('EarInset',(sign*.302,1.883,.071),(.084,.097,.019),inner,'Head'))
hood=join([hood,*ears],'Hood_Bear')
face=uv_shape('Face_Base',(0,1.455,.242),(.329,.285,.103),charcoal,'Head');face['customization_slot']='FaceBase'
for sign,side in [(-1,'R'),(1,'L')]:
    o=uv_shape('Eye_White_'+side,(sign*.139,1.515,.351),(.113,.119,.054),white,'Head');o['customization_slot']='Eyes'
    o=uv_shape('Eye_Pupil_'+side,(sign*.129,1.478,.400),(.041,.045,.017),black,'Head');o['customization_slot']='Eyes'
# A continuous smile with an O expression using matching tube topology.
verts=[];faces=[];nu=48;nv=10
for i in range(nu):
    t=i/(nu-1);x=(t-.5)*.242;y=1.298+.045*(2*t-1)**2
    for j in range(nv):
        a=2*math.pi*j/nv;verts.append((x,y+.006*math.cos(a),.357+.006*math.sin(a)))
for i in range(nu-1):
    for j in range(nv): a=i*nv+j;b=i*nv+(j+1)%nv;faces.append((a,b,b+nv,a+nv))
mouth=skin(mesh('Mouth',verts,faces,mouthmat),'Head');mouth['customization_slot']='Mouth'
for side,sign in [('L',1),('R',-1)]:
    shoe=uv_shape('Shoes_'+side,(sign*.212,.128,.073),(.165,.130,.218),pink,'Foot.'+side,.024)
    bot=uv_shape('Sole_'+side,(sign*.212,.030,.073),(.165,.026,.219),sole,'Foot.'+side,.015)
    shoe=join([shoe,bot],'Shoes_'+side);shoe['customization_slot']='Shoes'
# A persistent, script-free customization controller: ID properties drive shape keys.
ctrl=bpy.data.objects.new('CUSTOMIZE',None);s.collection.objects.link(ctrl);ctrl.empty_display_type='CUBE';ctrl.empty_display_size=.12;ctrl.location=(1.15,0,1)
def prop(name,value=0.,lo=0.,hi=1.):
    ctrl[name]=value;ctrl.id_properties_ui(name).update(min=lo,max=hi,description=name.replace('_',' '))
def drive(key,propname):
    f=key.driver_add('value');d=f.driver;d.type='AVERAGE';v=d.variables.new();v.name='control';v.type='SINGLE_PROP';v.targets[0].id=ctrl;v.targets[0].data_path='["'+propname+'"]'
for name in ['Blink_L','Blink_R','Angry','Mouth_Frown','Mouth_Open','Ears_Small','Ears_Rabbit','Shoes_Wide']:prop(name)
for name in ['Look_X','Look_Y']:prop(name,0.,-1.,1.)
for o in [o for o in s.objects if o.type=='MESH' and o.get('customization_slot')]:
    if o==body:continue
    o.shape_key_add(name='Basis')
    if o.get('customization_slot')=='Eyes':
        side=o.name[-1];k=o.shape_key_add(name='Blink_'+side)
        for v in k.data:v.co.y=1.515+(v.co.y-1.515)*.045
        drive(k,'Blink_'+side)
        k=o.shape_key_add(name='Angry')
        sign=1 if side=='L' else -1
        for v in k.data:v.co.y+=sign*(v.co.x-sign*.139)*.50
        drive(k,'Angry')
        if 'Pupil' in o.name:
            for pn,ax,amt in [('Look_X',0,.037),('Look_Y',1,.036)]:
                k=o.shape_key_add(name=pn);k.slider_min=-1
                for v in k.data:v.co[ax]+=amt
                drive(k,pn)
    elif o==mouth:
        k=o.shape_key_add(name='Mouth_Frown')
        for v in k.data:v.co.y=2*1.326-v.co.y
        drive(k,'Mouth_Frown')
        k=o.shape_key_add(name='Mouth_Open')
        for i,v in enumerate(k.data):
            t=(i//nv)/(nu-1);a=2*math.pi*t;b=2*math.pi*(i%nv)/nv
            v.co=(.043*math.sin(a),1.31+.052*math.cos(a)+.006*math.cos(b),.357+.006*math.sin(b))
        drive(k,'Mouth_Open')
    elif o==hood:
        for pn in ['Ears_Small','Ears_Rabbit']:
            k=o.shape_key_add(name=pn)
            for v in k.data:
                if v.co.y>1.82 and abs(v.co.x)>.18:
                    factor=max(0,min(1,(v.co.y-1.82)/.1))
                    if pn=='Ears_Rabbit':v.co.y+=.24*factor
                    else:v.co.y-=.09*factor
            drive(k,pn)
    elif o.get('customization_slot')=='Shoes':
        k=o.shape_key_add(name='Shoes_Wide');cx=.212 if o.name.endswith('L') else -.212
        for v in k.data:v.co.x=cx+(v.co.x-cx)*1.16
        drive(k,'Shoes_Wide')
for k in body.data.shape_keys.key_blocks: k.value=0.0
# Organized interchange slots and an embedded image reference.
for label in ['Body','Hood','FaceBase','Eyes','Mouth','Shoes']:
    col=bpy.data.collections.new('SLOT | '+label);s.collection.children.link(col)
    for o in list(s.objects):
        if o.get('customization_slot')==label:
            for c in list(o.users_collection):c.objects.unlink(o)
            col.objects.link(o)
ref=bpy.data.images.load('C:/Users/SSAFY/Downloads/Codex 이미지 2026년 9월 11일 오후 03_18_11.png');ref.pack()
# Studio and front / three-quarter cameras.
def aim(o,t):o.rotation_euler=(Vector(t)-o.location).to_track_quat('-Z','Y').to_euler()
studio=bpy.data.collections.new('STUDIO');s.collection.children.link(studio)
for name,loc,power,size in [('Key',(-3,-4,5),430,3),('Fill',(3,-3,2.5),170,3),('Rim',(0,2,4),500,2.5)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,(0,0,1))
world=bpy.data.worlds.new('V13 charcoal studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.17,.17,.17,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35;s.world=world
for name,loc in [('Front',(0,-7,1.25)),('ThreeQuarter',(3,-7,2.7)),('Back',(3,7,2.7))]:
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;aim(o,(0,0,1.02));d.type='ORTHO';d.ortho_scale=2.23
s.camera=bpy.data.objects['Front'];s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True
s.render.resolution_x=780;s.render.resolution_y=920;s.render.resolution_percentage=100;s.render.film_transparent=True;s.render.image_settings.file_format='PNG'
s.view_settings.view_transform='AgX'
rig.show_in_front=True
bpy.ops.object.select_all(action='DESELECT');ctrl.select_set(True);bpy.context.view_layer.objects.active=ctrl
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.overlay.show_overlays=False
s.render.filepath=str(OUT/'preview-front.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'))
bpy.ops.render.render(write_still=True)
print('V13 BUILD COMPLETE')


