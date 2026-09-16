import bpy, sys, json
from pathlib import Path

paths = [Path(arg) for arg in sys.argv[sys.argv.index('--') + 1:]]
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True)
    data = {'path': str(path), 'objects': []}
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH':
            continue
        slots = []
        for material in obj.data.materials:
            slots.append({
                'name': material.name if material else None,
                'nodes': [(node.name, node.type, getattr(getattr(node, 'image', None), 'filepath', None)) for node in material.node_tree.nodes] if material and material.use_nodes else [],
            })
        data['objects'].append({'name': obj.name, 'vertices': len(obj.data.vertices), 'materials': slots})
    print(json.dumps(data, ensure_ascii=False))
