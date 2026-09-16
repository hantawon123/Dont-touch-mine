import bpy
from mathutils import Vector
fbx=r"C:/Users/SSAFY/barleymilk/S15P21D205/Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Crawl_Forward.fbx"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
s=bpy.context.scene
b=bpy.data.objects["Body"]
waist=b.data.shape_keys.key_blocks["Crawl_Waist_Round"]
for f in [1,10,19,28]:
    s.frame_set(f)
    bpy.context.view_layer.update()
    ev=b.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me=ev.to_mesh()
    pts=[b.matrix_world@v.co for v in me.vertices]
    ev.to_mesh_clear()
    zs=[p.z for p in pts]
    mid=(min(zs)+max(zs))*0.5
    band=[p for p in pts if abs(p.z-mid)<0.10]
    xs=sorted(p.x for p in band)
    print("F", f, "width", round(xs[-1]-xs[0],3), "waist", round(waist.value,3), "n", len(band))
