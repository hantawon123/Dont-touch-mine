import bpy
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
