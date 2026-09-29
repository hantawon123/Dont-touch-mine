import bpy
for name in ('Punching','Big Hit To Head','Knocked Out'):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath='C:/Users/SSAFY/Downloads/'+name+'.fbx');r=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');a=r.animation_data.action
 print('SOURCE',name,'RANGE',tuple(a.frame_range),'BONES',list(r.pose.bones.keys())[:10])
 lo,hi=map(int,a.frame_range)
 for f in range(lo,hi+1,max(1,(hi-lo)//8)):
  bpy.context.scene.frame_set(f);bpy.context.view_layer.update();print(f,[(b.name,tuple(round(x,2) for x in b.head)) for b in r.pose.bones if b.name in ('Hips','Hand.R','Hand.L','Head')])
