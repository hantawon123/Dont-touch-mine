import bpy, json, hashlib
path=r'C:\Users\SSAFY\barleymilk\S15P21D205\Assets\Scenes\CharacterTest\First\FirstPlayerCapsule_Idle.fbx'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path, use_anim=False)
rows=[]
for o in bpy.data.objects:
    if o.type=='MESH' and 'hood' in o.name.lower():
        vals=[]
        for v in o.data.vertices: vals.extend(round(float(c),8) for c in v.co)
        rows.append({'name':o.name,'verts':len(o.data.vertices),'faces':len(o.data.polygons),'dims':tuple(round(x,8) for x in o.dimensions),'hash':hashlib.sha256(','.join(f'{x:.8f}' for x in vals).encode()).hexdigest(),'mats':[m.name for m in o.data.materials]})
open(r'C:\Users\SSAFY\barleymilk\S15P21D205\unity_hood_inspect.json','w',encoding='utf-8').write(json.dumps(rows,ensure_ascii=False,indent=2))
print(json.dumps(rows,ensure_ascii=False,indent=2),flush=True)
