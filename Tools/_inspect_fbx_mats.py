import bpy,sys
p=sys.argv[-1];bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=p,use_anim=False)
for o in bpy.context.scene.objects:
 if o.type=='MESH': print(o.name,[(m.name if m else None) for m in o.data.materials],len(o.material_slots))
