"""Keep the approved shoulder deformation while isolating the torso from legs."""

def capture_weights(body):
    return [{body.vertex_groups[g.group].name:g.weight for g in v.groups}
            for v in body.data.vertices]

def restore_upper_body(body, source_weights):
    assert len(body.data.vertices)==len(source_weights)
    changed=0
    for vertex,source in zip(body.data.vertices,source_weights):
        t=max(0.0,min(1.0,(vertex.co.y-.65)/.20))
        t=t*t*(3-2*t)
        if t==0:continue
        current={body.vertex_groups[g.group].name:g.weight for g in vertex.groups}
        result={name:current.get(name,0)*(1-t)+source.get(name,0)*t
                for name in current.keys()|source.keys()}
        # A moving thigh must not pull on the upper torso. Preserve the source
        # shoulder/arm blend instead of assigning the entire axilla to Spine.
        for name in list(result):
            if name.startswith(('UpperLeg.','Leg.','Foot')):
                result['Spine']=result.get('Spine',0)+result.pop(name)
        total=sum(result.values())
        assert total>.999
        for group in body.vertex_groups:group.remove([vertex.index])
        for name,weight in result.items():
            if weight>1e-9:body.vertex_groups[name].add([vertex.index],weight/total,'REPLACE')
        changed+=1
    return changed
