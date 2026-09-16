import bpy,sys,json
bpy.ops.wm.open_mainfile(filepath=sys.argv[sys.argv.index('--')+1],use_scripts=False)
data={}
for obj in bpy.data.objects:
 if obj.type=='MESH':
  data[obj.name]=[]
  for mat in obj.data.materials:
   info={'name':mat.name if mat else None,'diffuse':tuple(mat.diffuse_color) if mat else None,'nodes':[]}
   if mat and mat.use_nodes:
    for node in mat.node_tree.nodes:
     info['nodes'].append([node.name,node.type,getattr(getattr(node,'image',None),'filepath',None)])
   data[obj.name].append(info)
print(json.dumps(data,ensure_ascii=False))
