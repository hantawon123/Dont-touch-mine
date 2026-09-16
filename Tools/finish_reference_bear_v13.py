import bpy,random,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'source/blender/characters/ReferenceBear_V13'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'),use_scripts=False)
s=bpy.context.scene;r=bpy.data.objects['DGN_Armature'];hood=bpy.data.objects['Hood_Bear'];ctrl=bpy.data.objects['CUSTOMIZE']
random.seed(1309);verts=[];faces=[];indices=[]
# Short, tapered geometric fibers: render and viewport work without simulation.
hood.data.update()
for _ in range(22000):
    vi=random.randrange(len(hood.data.vertices));v=hood.data.vertices[vi];p=v.co.copy();n=v.normal.normalized()
    if n.length<.5:continue
    axis=n.cross(Vector((0,1,0)))
    if axis.length<.1:axis=n.cross(Vector((1,0,0)))
    axis.normalize();other=n.cross(axis);a=random.random()*math.tau
    axis=axis*math.cos(a)+other*math.sin(a)
    p+=axis*random.uniform(-.002,.002);length=random.uniform(.002,.006);w=random.uniform(.00035,.00075)
    start=len(verts)
    verts.extend([p-axis*w,p+axis*w,p+n*length+axis*.001])
    faces.append((start,start+1,start+2));indices.extend([vi]*3)
me=bpy.data.meshes.new('Plush fibers');me.from_pydata(verts,[],faces);me.update()
o=bpy.data.objects.new('Hood_Fibers',me);bpy.data.collections['SLOT | Hood'].objects.link(o);o.parent=r;me.materials.append(hood.data.materials[0]);o['customization_slot']='Hood'
g=o.vertex_groups.new(name='Head');g.add(list(range(len(verts))),1,'REPLACE');m=o.modifiers.new('First skeleton','ARMATURE');m.object=r
o.shape_key_add(name='Basis')
for pn in ['Ears_Small','Ears_Rabbit']:
    k=o.shape_key_add(name=pn,from_mix=False)
    src=hood.data.shape_keys.key_blocks[pn];base=hood.data.shape_keys.key_blocks[0]
    for j,v in enumerate(k.data):v.co+=src.data[indices[j]].co-base.data[indices[j]].co
    f=k.driver_add('value');f.driver.type='AVERAGE';var=f.driver.variables.new();var.name='control';var.type='SINGLE_PROP';var.targets[0].id=ctrl;var.targets[0].data_path='["'+pn+'"]'
# Optional convenience panel; ordinary ID-property controls need no script execution.
ui='''import bpy
class V13_OT_Action(bpy.types.Operator):
    bl_idname='v13.action'
    bl_label='Select First Action'
    clip: bpy.props.StringProperty()
    def execute(self,context):
        r=bpy.data.objects['DGN_Armature'];k=bpy.data.objects['Body'].data.shape_keys
        a=bpy.data.actions['First_'+self.clip];r.animation_data_create().action=a
        if a.slots:r.animation_data.action_slot=a.slots[0]
        k.animation_data_clear()
        for key in k.key_blocks:key.value=0
        sa=bpy.data.actions.get('Shapes_'+self.clip)
        if sa:
            k.animation_data_create().action=sa
            if sa.slots:k.animation_data.action_slot=sa.slots[0]
        context.scene.frame_start=int(a.frame_range[0]);context.scene.frame_end=int(a.frame_range[1]);context.scene.frame_set(int(a.frame_range[0]))
        return {'FINISHED'}
class V13_PT_Customize(bpy.types.Panel):
    bl_label='Reference Bear | Customize'
    bl_idname='V13_PT_Customize'
    bl_space_type='VIEW_3D'
    bl_region_type='UI'
    bl_category='Character'
    def draw(self,context):
        c=bpy.data.objects['CUSTOMIZE'];l=self.layout
        for title,props in [('Eyes',['Blink_L','Blink_R','Look_X','Look_Y','Angry']),('Mouth',['Mouth_Frown','Mouth_Open']),('Hood and shoes',['Ears_Small','Ears_Rabbit','Shoes_Wide'])]:
            box=l.box();box.label(text=title)
            for p in props:box.prop(c,'["'+p+'"]',text=p.replace('_',' '),slider=True)
        box=l.box();box.label(text='Material colors')
        for name in ['Lavender plush','Rose shoes','Charcoal fleece','Warm cream eyes']:
            mat=bpy.data.materials['V13 | '+name];box.prop(mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'],'default_value',text=name)
        box=l.box();box.label(text='First actions')
        for a in bpy.data.actions:
            if a.name.startswith('First_'):
                op=box.operator('v13.action',text=a.name[6:]);op.clip=a.name[6:]
for c in [V13_OT_Action,V13_PT_Customize]:
    old=getattr(bpy.types,c.__name__,None)
    if old:bpy.utils.unregister_class(old)
    bpy.utils.register_class(c)
'''
t=bpy.data.texts.get('OPEN_CUSTOMIZER.py') or bpy.data.texts.new('OPEN_CUSTOMIZER.py');t.clear();t.write(ui)
(OUT/'OPEN_CUSTOMIZER.py').write_text(ui,encoding='utf8')
readme='''Reference Bear V13

SOURCE: V12_session_before_v13.blend / Bear_Body mesh and authored weights.
ANIMATION: Project First rig, 28 unchanged bones, 12 existing First actions.
Reference PNG is packed in this file.

CUSTOMIZE: Select CUSTOMIZE in Outliner > Object Properties > Custom Properties.
Blink_L/R, Look_X/Y, Angry; Mouth_Frown/Open; Ears_Small/Rabbit; Shoes_Wide.
Defaults are zero. Use one mouth/ear mode at a time for clean presets.
Change colors in V13 material Principled BSDF Base Color.

OPTIONAL PANEL: Text Editor > OPEN_CUSTOMIZER.py > Run Script.
Return to 3D View > N > Character. It includes colors and action buttons.
This panel must be run again after reopening Blender. Sliders themselves use
built-in drivers and work without running a script.

SLOTS: Body / Hood (shell and fibers) / FaceBase / Eyes / Mouth / Shoes.
Swap or hide all objects in a slot together. New parts need matching First
rest coordinates, Armature modifier and bone vertex groups.
Hood/face/eyes/mouth follow Head; shoes follow Foot.L/R.

ANIMATION: SELECT_ACTION.py (CLIP name) selects skeleton and body correction
actions together. The optional Character panel does the same.
Body sculpt was calibrated to First Idle; transferred correction deltas
need pose-specific art review in extreme poses. Numeric checks do not prove
no intersections. Runtime Unity wardrobe and material baking are not included.

BUILD: Tools/build_reference_bear_v13.py then Tools/finish_reference_bear_v13.py.
VERIFY: Tools/verify_reference_bear_v13.py.
'''
t=bpy.data.texts.get('START_HERE.txt') or bpy.data.texts.new('START_HERE.txt');t.clear();t.write(readme)
for area in bpy.context.screen.areas:
    if area.type=='PROPERTIES':area.spaces.active.context='OBJECT'
s.camera=bpy.data.objects['Front'];s.cycles.samples=32;s.render.resolution_percentage=100
s.render.filepath=str(OUT/'preview-front.png')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'ReferenceBear_V13.blend'))
bpy.ops.render.render(write_still=True)
# Alternate preset makes the expression and costume controls reviewable.
ctrl['Blink_L']=1.;ctrl['Mouth_Open']=1.;ctrl['Ears_Rabbit']=1.;ctrl.update_tag();s.frame_set(2)
s.render.resolution_percentage=65;s.render.filepath=str(OUT/'preview-customization.png');bpy.ops.render.render(write_still=True)
print('FINISH COMPLETE',flush=True)
