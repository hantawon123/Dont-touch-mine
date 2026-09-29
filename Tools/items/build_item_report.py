"""Create an offline, searchable visual catalog and validate generated sizes."""
import collections
import csv
import html
import json
from pathlib import Path

root = Path.cwd()
folder = root / 'docs/items'
data = json.loads((folder/'item-results.json').read_text(encoding='utf-8'))
manifest = json.loads((root/'Assets/_Game/Content/ItemCollection/ItemCollection.json').read_text(encoding='utf-8'))
items = data['items']
assert all(abs(data['maximumItemSize'][axis] - data['characterSize'][axis]) < .00001 for axis in 'xyz')
assert len(items) == len(manifest['items'])
assert len({e['id'] for e in items}) == len(items)
for e in items:
    assert 0 < e['scale'] <= 1, e['id']
    for axis in 'xyz':
        assert e['finalSize'][axis] <= data['maximumItemSize'][axis] + .001, e['id']
        assert abs(e['originalSize'][axis]*e['scale']-e['finalSize'][axis]) < .005, e['id']
    assert (root/e['prefab']).exists(), e['id']
    assert (folder/e['thumbnail']).stat().st_size > 100, e['id']

def dim(d): return ' × '.join(f'{d[k]:.3f}' for k in 'xyz')

counts = collections.Counter(e['categoryName'] for e in items)
with (folder/'아이템_카테고리_크기.csv').open('w',encoding='utf-8-sig',newline='') as f:
    writer = csv.writer(f)
    writer.writerow(['ID','카테고리','아이템','원본 크기 XYZ(m)','조정 크기 XYZ(m)','축소 배율','비고','프리팹','원본 에셋','패키지'])
    for e in items: writer.writerow([e['id'],e['categoryName'],e['displayName'],dim(e['originalSize']),dim(e['finalSize']),e['scale'],e['note'],e['prefab'],e['source'],e['package']])

lines=['# 아이템 분류 및 크기 검증','',f"캐릭터 실제 메시 크기(X/Y/Z): **{dim(data['characterSize'])} m**.",f"아이템 상한(X/Y/Z): **{dim(data['maximumItemSize'])} m**.",'',
       '각 축 상한을 모두 만족하는 하나의 배율을 적용했다. 원본의 종횡비·형태를 유지하며 작은 아이템은 키우지 않았다. 원본 에셋 대신 분류 폴더의 게임용 프리팹을 사용한다.','',
       f'전시 항목: {len(items)}개 (색상·형태·성장 변형 및 보류품 포함). 축소: {sum(e["scale"] < .99999 for e in items)}개.','',
       '**총기·폭발물은 현재 16개로 최소 20개 조건에 4개 부족하다.** 부품을 총기로 세지 않았다. 공구는 기존 창고 팩의 전동드릴·수평계·바이스·십자렌치·나사·사다리·가스통·작업용 콘으로 범위를 보충했다.','',
       '## 사용 방법','','- `Assets/_Game/Content/Scenes/ItemGallery.unity`를 연다. Play 없이 Scene 뷰에서도 카테고리별로 확인 가능하다.','- Play: 상단 카테고리 버튼 또는 숫자 1~6. 우클릭을 누른 채 WASD 이동, Q/E 상승·하강, Shift 가속, Home 시점 복귀. F 개별 물건 근접 보기, 좌우 화살표 이전·다음 물건. Game 뷰를 클릭해 포커스를 준 뒤 조작한다.','- 씬마다 현재 캐릭터 1배 크기 참조가 있으며 모든 전시 물건은 조정된 게임용 프리팹이다.','- `docs/items/index.html`: 카테고리 필터, 검색, 이미지, 크기, 배율, 프리팹 경로. CSV는 Excel에서 열 수 있다.','- 재생성: Unity 메뉴 `Tools > Game > Items > Build Categorized Item Gallery`. 이후 프로젝트 루트에서 `python Tools/items/build_item_report.py`.','',
       '## 범위','','게임 배정은 Resources/Items/ItemCatalog.asset의 활성 카테고리·아이템에 연결되어 있다. 새 프리팹에는 CarryableItem, Rigidbody, BoxCollider가 포함되어 있다. 추가·수정 방법은 [게임 카탈로그 관리](게임_카탈로그_관리.md)를 참고한다.','',
       '패키지의 중복 GUID 충돌을 피하기 위해 ItemSources 아래 패키지별로 GUID를 분리했다. 필요한 메시·프리팹·재질·텍스처 의존성만 가져왔고 데모 실행 스크립트는 가져오지 않았다. 캐릭터 크기는 PlayerCharacter.prefab의 실제 메시 경계로 측정했다.','',
       '## 카테고리별 항목 수','','| 카테고리 | 전시 항목 수 |','|---|---:|']
lines += [f'| {name} | {count} |' for name,count in counts.items()]
lines += ['','전시 항목 수는 고유 종류 수가 아니다. 색상·성장 변형은 아래 비고와 이미지로 구분한다.','']
for name in counts:
    lines += ['## '+name,'','| 아이템 | 조정 크기 XYZ(m) | 배율 | 비고 |','|---|---|---:|---|']
    for e in items:
        if e['categoryName']==name: lines.append(f"| {e['displayName']} | {dim(e['finalSize'])} | {e['scale']:.4f} | {e['note']} |")
lines += ['','## 전시에서 제외한 원본 프리팹','','렌더링 방식별 중복, 식물 구성 부품, 차량 스크립트 프리팹은 아래와 같이 처리했다. 차량은 동일 팩의 FBX 모델로 전시한다.','']
lines += [f"- `{e['source']}` — {e['reason']}" for e in manifest['excluded']]
(folder/'README.md').write_text('\n'.join(lines),encoding='utf-8')

cards=[]
for e in items:
    esc=html.escape
    cards.append(f'''<article data-category="{esc(e['categoryName'])}" data-search="{esc(e['displayName']+' '+e['source']+' '+e['package']).lower()}">
    <img loading="lazy" src="{esc(e['thumbnail'])}" alt="{esc(e['displayName'])}">
    <div class="body"><span class="tag">{esc(e['categoryName'])}</span><h2>{esc(e['displayName'])}</h2>
    <p class="size">{dim(e['finalSize'])} <small>m · XYZ</small></p><p class="scale">{'축소 × '+format(e['scale'],'.4f') if e['scale'] < .99999 else '원본 크기 유지'}</p>
    <p class="note">{esc(e['note']) or '—'}</p><details><summary>원본 / 프리팹 경로</summary><p>원본 크기 {dim(e['originalSize'])} m</p><code>{esc(e['prefab'])}</code><p>{esc(e['source'])}</p><p>{esc(e['package'])}</p></details></div></article>''')
options=''.join(f'<option>{html.escape(name)}</option>' for name in counts)
page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Keep It · 아이템 도감</title>
<style>*{box-sizing:border-box}body{margin:0;background:#101722;color:#edf2f7;font:15px/1.6 system-ui,sans-serif}header,main{max-width:1440px;margin:auto;padding:28px}header{padding-top:48px}.eyebrow{color:#72cfc1;letter-spacing:3px;font-size:12px}h1{font-size:38px;margin:12px 0}header p{color:#acbcd0;max-width:1050px}.notice{padding:14px 18px;border-left:3px solid #efb45a;background:#2b251d;color:#f5d8a4}.toolbar{position:sticky;top:0;z-index:1;background:#101722ee;display:flex;gap:12px;align-items:center;padding:16px 0;backdrop-filter:blur(12px)}input,select{padding:12px;background:#202c3e;border:1px solid #3a4a60;border-radius:8px;color:#fff;font:inherit}input{flex:1}#count{white-space:nowrap;color:#91a9c0}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(255px,1fr));gap:20px}article{background:#1b2636;border:1px solid #2b3a50;border-radius:12px;overflow:hidden}article img{width:100%;aspect-ratio:1.2;object-fit:contain;background:#101722}.body{padding:18px}h2{font-size:18px;margin:8px 0}.tag{color:#84d7cd;font-size:12px}.size{font-variant-numeric:tabular-nums;margin:5px 0}small,.note,details{color:#9bb0c8}.scale{color:#edc28b;font-size:13px;margin:5px 0}.note{font-size:12px;min-height:38px}details{font-size:11px;overflow-wrap:anywhere}summary{cursor:pointer}code{color:#cad8ec}.links a{color:#84d7cd;margin-right:20px}article[hidden]{display:none}@media(max-width:650px){header,main{padding:18px}.toolbar{flex-wrap:wrap}input{min-width:180px}h1{font-size:28px}}</style>
<header><div class="eyebrow">KEEP IT / ASSET COLLECTION</div><h1>아이템 도감</h1><p>카테고리별 실제 Unity 렌더링과 크기. 색상·형태 변형과 보류품을 포함한 전시 목록입니다. 작은 물건은 확대하지 않았으며, 이미지는 물건 확인을 위해 각각 화면에 맞춰 촬영했습니다. 실제 상대 크기는 전시 씬과 아래 수치를 확인하세요.</p>
<p>캐릭터 XYZ: CHARACTER m · 아이템 상한: LIMIT m<br>모든 축에 동일한 축소 배율을 적용했습니다.</p><div class="notice">총기·폭발물 16개 / 목표 20개 — 4개 추가 확보 필요. 보류품과 부속품은 총기 개수에 포함하지 않습니다.</div><p class="links"><a href="아이템_카테고리_크기.csv">CSV 목록</a><a href="README.md">분류·검증 보고서</a></p></header>
<main><div class="toolbar"><input id="search" placeholder="아이템 이름, 파일 이름, 패키지 검색" aria-label="아이템 검색"><select id="category" aria-label="카테고리"><option value="">전체 카테고리</option>OPTIONS</select><span id="count"></span></div><div class="grid">CARDS</div></main>
<script>const cards=[...document.querySelectorAll('article')],search=document.querySelector('#search'),category=document.querySelector('#category');function filter(){let n=0;for(const c of cards){c.hidden=!!((category.value&&c.dataset.category!==category.value)||!c.dataset.search.includes(search.value.toLowerCase().trim()));if(!c.hidden)n++}document.querySelector('#count').textContent=n+'개 표시'}search.addEventListener('input',filter);category.addEventListener('change',filter);filter();</script></html>'''
page=page.replace('CHARACTER',dim(data['characterSize'])).replace('LIMIT',dim(data['maximumItemSize'])).replace('OPTIONS',options).replace('CARDS','\n'.join(cards))
(folder/'index.html').write_text(page,encoding='utf-8')
print(f'PASS: {len(items)} unique IDs, all scale ratios, limits, prefabs and images; {sum(e["scale"]<.99999 for e in items)} scaled down.')
print(dict(counts))
