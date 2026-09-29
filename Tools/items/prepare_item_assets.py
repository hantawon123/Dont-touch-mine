"""Import model/material dependencies from the user's already-downloaded packages.

Never overwrite an existing asset. Namespace vendor GUIDs and remap references.
Run from the Unity project root with the inventory JSON path as the argument.
"""
import collections
import hashlib
import json
from pathlib import Path
import re
import sys
import tarfile

ROOT = Path.cwd().resolve()
OUT = ROOT / 'Assets/_Game/Content/ItemCollection'
CATEGORIES = {'food': '음식·음료', 'household': '생활용품', 'tools': '공구·작업용품',
              'modern': '총기·폭발물', 'fantasy': '판타지 소품', 'reserve': '보류·부품·모형'}
WORDS = dict(x.split('=', 1) for x in '''Mushroom=버섯 Cookie=쿠키 Sandwich=샌드위치 Tea=차 Croissant=크루아상 Can=캔 Ice_cream=아이스크림 Egg=달걀 Shrimp=새우 Bar=바 Plate=접시 Cup=컵 Glass=유리잔 Watermelon=수박 Sasuage=소시지 Sausage=소시지 Bowl=그릇 Onion=양파 Chili=고추 Bottle=병 Burger=햄버거 Donut=도넛 Soda=탄산음료 Cheesecake=치즈케이크 Pepper=피망 Fish=생선 Coffee=커피 Chips=감자칩 Fork=포크 Pastry=페이스트리 Yogurt=요구르트 Avocado=아보카도 Apple=사과 Orange=오렌지 Pumpkin=호박 Tomato=토마토 Bread=빵 Meat=고기 Cheese=치즈 Briefcase=서류가방 KitchenKnife=주방칼 Hatchet=손도끼 Chair=의자 GasCan=연료통 Book=책 Bat=야구방망이 FastFoodCup=음료컵 Spatula=뒤집개 Toothbrush=칫솔 SmartPhone=스마트폰 Pan=프라이팬 Table=탁자 Dumbell=아령 Desk=책상 SledgeHammer=대형망치 Wrench=렌치 TapeMeasurer=줄자 Hammer=망치 PillBottle=약통 Spoon=숟가락 FastCoffeeCup=커피컵 MilkCarton=우유팩 Toolbox=공구함 Camera=카메라 Goblet=고블릿 BeerMug=맥주잔 Pencil=연필 ButcherKnife=식칼 Pot=화분·냄비 Plunger=뚫어뻥 MedBook=의학서적 TV=텔레비전 Mug=머그컵 HealthKit=구급용품 TVStand=TV장 Money=돈 Saw=톱 Bandages=붕대 ScrewDriver=드라이버 AmmoCrate=탄약상자 Shovel=삽 Crowbar=쇠지렛대 Syringe=주사기 Flashlight=손전등 AmmoCase=탄약케이스 Medkit=구급상자 Bookshelf=책장 Binoculars=쌍안경 Tweezers=핀셋 BandageRoll=붕대 AdhesiveTape=테이프 Airfreshener=방향제 Razor=면도기 BobbyPin=실핀 NailClippers=손톱깎이 HairCurler=헤어롤 CottonSwab=면봉 SprayBottle=분무기 HairGrease=헤어제품 Soapbox=비누통 HandMirror=손거울 RubberDuck=고무오리 Soap=비누 HairDryer=드라이어 LatexGloves=고무장갑 AdhesiveBandage=반창고 Clothespin=빨래집게 TissueBox=티슈 Basket=바구니 MedicalScissors=의료용가위 knife=칼 pistol=권총 baseball_bat=야구방망이 sight=조준경 axe=도끼 shotgun_handguard=산탄총손잡이 pistol_drum=드럼탄창 hockey_stick=하키스틱 sniper_rifle=저격소총 rifle=소총 rifle_magazine=소총탄창 shotgun=산탄총 RPG7=RPG7발사기 ANPEQ15=레이저표적지시기 M2_50cal=M2중기관총 M249=M249기관총 TAN_LR_Scope_01=장거리조준경 RGD-5=수류탄 Uzi=우지 M4_8=M4소총 M1911=M1911권총 M107=M107저격소총 ELCAN=조준경 Bennelli_M4=베넬리산탄총 Smoke=연막탄 Flash=섬광탄 SR_Scope_00=조준경 AK74=AK74소총'''.split())


def display(name):
    if name == 'Pot': return '냄비'
    if name.startswith('CS_Plant_Pot_'):
        pieces=name.split('_')
        return '화분 '+pieces[-2]+' / 성장 '+pieces[-1].removeprefix('L')
    if name.startswith('Pot') and ('Cactus' in name or name[3:].isdigit()):
        return '선인장 화분' if 'Cactus' in name else '인테리어 화분 '+name[3:]
    if name == 'Plant & Pot': return '식물 화분 조합'
    compact = name.replace(' ', '')
    for key in sorted(WORDS, key=len, reverse=True):
        if compact.lower().startswith(key.lower()):
            suffix = compact[len(key):].strip('_')
            return WORDS[key] + (' ' + suffix if suffix else '')
    if 'one_handed_axe' in name: return '한손도끼 ' + name.rsplit('_', 1)[-1]
    if 'two_handed_axe' in name: return '양손도끼 ' + name.rsplit('_', 1)[-1]
    if 'one_handed_sword' in name: return '한손검 ' + name.rsplit('_', 1)[-1]
    if 'two_handed_sword' in name: return '양손검 ' + name.rsplit('_', 1)[-1]
    if name.startswith('PP_Theme_'):
        terms = {'Spellbook':'마법책','Axe':'도끼','Horn':'뿔피리','Bomb':'폭탄','Torch':'횃불','Shield':'방패','Potion':'물약','Spear':'창','Dynamite':'다이너마이트','Hammer':'망치','Dagger':'단검','Chakram':'차크람','Arrow':'화살','Bow':'활','Quiver':'화살통','Wand':'마법봉','Sword':'검','Scythe':'낫','Fist':'주먹무기','Scepter':'홀','Throwing':'투척칼','Key':'열쇠'}
        return terms.get(name.split('_')[3],name) + ' ' + name.split('_')[2] + '-' + name.split('_')[-1]
    if name.startswith('Basement_'):
        terms={'CordlessDrill':'전동드릴','SpiritLevel':'수평계','Vise':'바이스','WheelWrench':'십자렌치','Wrench1':'렌치 A','Wrench2':'렌치 B','MeasuringTape':'줄자','Hammer':'망치','Flashlight':'손전등','Screw':'나사','Ladder':'사다리','GasCylinder':'가스통','RoadCone':'작업용콘'}
        return '창고 ' + terms.get(name.split('_')[-1],name)
    return name


def classify(package, path):
    n = Path(path).stem
    if 'Swords Pack' in package or 'Axes Pack' in package or 'Fantasy RPG' in package: return 'fantasy', ''
    if 'Road Vehicles' in package: return 'reserve', '차량 10종: 독립 카테고리 최소 20개 미달, 모형 후보'
    if 'Cosmic Retro' in package or 'Flower Pots' in package: return 'household', '화분 조합/성장 단계: 서로 다른 식물 종류로 중복 계산하지 않음'
    if 'Bathroom' in package: return 'household', ''
    if 'RPG Food' in package:
        if n.startswith(('Trader', 'Wooden')): return 'reserve', '환경 구조물/내용물이 담긴 세트'
        return ('household' if n.startswith(('Bottle','Plate')) else 'food'), ''
    if package.startswith('Food FREE'):
        if n == 'Frame': return 'reserve', '전시용 프레임'
        return ('household' if n.startswith(('Plate','Cup','Glass','Bowl','Fork','Bottle')) else 'food'), ''
    if 'Weapons VOL1' in package:
        return ('reserve', '무기 부속품: 총기 개수에서 제외') if n in ['ANPEQ15','TAN_LR_Scope_01','ELCAN','SR_Scope_00'] else ('modern','')
    if package.startswith('Weapons FREE'):
        if n.startswith(('pistol_001','sniper_rifle_','rifle_001','shotgun_001')): return 'modern',''
        if n == 'axe_001': return 'tools','손도끼 형태 변형'
        if n in ['baseball_bat_001','hockey_stick_001']: return 'household','스포츠용품'
        return 'reserve','근접무기 또는 무기 부속품: 총기·폭발물에서 제외'
    if 'Household Items' in package:
        if n.startswith(('Hammer','Hatchet','SledgeHammer','Wrench','ScrewDriver','Saw','Shovel','Crowbar','TapeMeasurer','Toolbox','Flashlight','GasCan')): return 'tools',''
        if n.startswith('Ammo'): return 'reserve','탄약 보관함: 총기·폭발물에서 제외'
        if n.startswith(('MilkCarton','FastCoffeeCup','FastFoodCup')): return 'food',''
        return 'household',''
    raise ValueError(package)


def main(inventory):
    data=json.loads(Path(inventory).read_text(encoding='utf-8'))
    archives=[]; entries={}; records=[]; excluded=[]; remaps={}; sourcepaths={}
    for pack in data:
        archive=tarfile.open(pack['file']); archives.append(archive)
        members={m.name:m for m in archive}
        remaps[archive]={m.name.split('/')[0].encode():hashlib.md5((pack['package']+'/'+m.name.split('/')[0]).encode()).hexdigest().encode() for m in members.values() if m.name.endswith('/pathname')}
        folder=re.sub(r'[^A-Za-z0-9]+','_',pack['package']).strip('_')
        for key,m in members.items():
            if not key.endswith('/pathname'): continue
            path=archive.extractfile(m).read().decode('utf-8-sig').split('\n')[0].rstrip('\x00')
            prefix=key.split('/')[0]
            if prefix+'/asset' not in members: continue
            targetpath='Assets/ItemSources/'+folder+'/'+path.removeprefix('Assets/')
            sourcepaths[(pack['package'],path)]=targetpath
            guid=remaps[archive][prefix.encode()].decode()
            entries[guid]=(targetpath,archive,members,prefix)
        for path in sorted(pack['paths']):
            if not path.endswith('.prefab'): continue
            why=''
            if '/HDRP/' in path or '/Built-In/' in path or '/HD RENDER PIPELINE/' in path: why='렌더 파이프라인 중복'
            if 'Cosmic Retro' in pack['package'] and '/Plant + Pot/' not in path: why='식물/화분 구성 부품 또는 데모; 완성 조합만 전시'
            if 'Flower Pots' in pack['package'] and '/Plant & pots/' not in path: why='식물/화분 구성 부품; 완성 조합만 전시'
            if 'Road Vehicles' in pack['package']: why='프리팹 스크립트 대신 동일 차량 FBX로 전시'
            if why:
                excluded.append({'source':path,'reason':why}); continue
            category,note=classify(pack['package'],path)
            records.append(dict(source=path,package=pack['package'],category=category,categoryName=CATEGORIES[category],displayName=display(Path(path).stem),note=note))
        if 'Road Vehicles' in pack['package']:
            for path in sorted(pack['paths']):
                if path.lower().endswith('.fbx'):
                    records.append(dict(source=path,package=pack['package'],category='reserve',categoryName=CATEGORIES['reserve'],displayName=Path(path).stem,note='차량 10종: 모형 후보, 최소 20개 미달'))
    # Reuse the workshop props already owned by this project to broaden tools.
    for p in sorted((ROOT/'Assets/PolyWorkshop_BasementWorkshop/Props/Prefabs').glob('*.prefab')):
        if p.stem.startswith('Basement_Tools_') or p.stem in ['Basement_Ladder','Basement_GasCylinder','Basement_RoadCone']:
            records.append(dict(source=p.relative_to(ROOT).as_posix(),package='Existing Basement Workshop',category='tools',categoryName=CATEGORIES['tools'],displayName=display(p.stem),note='기존 프로젝트 에셋으로 보충'))
    for r in records:
        r['originalSource']=r['source']
        r['source']=sourcepaths.get((r['package'],r['source']),r['source'])
    bypath={v[0]:k for k,v in entries.items()}
    queue=[bypath[r['source']] for r in records if r['source'] in bypath]
    # Some VOL1 prefabs reference GUIDs absent from the vendor archive. Include
    # the actual supplied FBX models so the Unity builder can repair by mesh name.
    queue += [g for g,v in entries.items() if '/Low_Poly_Weapons_VOL1/' in v[0] and v[0].lower().endswith(('.fbx','.png'))]
    visited=set(); imported=[]
    while queue:
        guid=queue.pop()
        if guid in visited: continue
        visited.add(guid)
        path,archive,members,prefix=entries[guid]
        target=(ROOT/path).resolve()
        if not target.is_relative_to(ROOT/'Assets'): raise ValueError('Unsafe asset path: '+path)
        if prefix+'/asset' not in members: continue
        payload=archive.extractfile(members[prefix+'/asset']).read()
        meta=archive.extractfile(members[prefix+'/asset.meta']).read()
        # Vendor packs reuse GUIDs for different materials; namespace each pack.
        remap=lambda b: re.sub(rb'[0-9a-f]{32}',lambda m:remaps[archive].get(m[0],m[0]),b)
        payload=remap(payload); meta=remap(meta)
        if Path(path).suffix in ['.cs','.dll']: raise ValueError('Unexpected script dependency: '+path)
        for content in [payload,meta]:
            for ref in re.findall(rb'[0-9a-f]{32}',content):
                ref=ref.decode()
                if ref in entries: queue.append(ref)
        if target.exists():
            pass  # Keep any Unity importer migrations or user changes.
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            target.write_bytes(payload)
            target.with_name(target.name+'.meta').write_bytes(meta)
            imported.append(path)
    for r in records:
        r['id']=r['category']+'_'+hashlib.sha1(r['source'].encode()).hexdigest()[:10]
        n=Path(r['source']).stem
        r['family']=re.sub(r'(Blue|Red|Green|White|Black|Brown|Grey|Orange|Purple)$','',n)
        if 'Household Items' not in r['package']: r['family']=n
        if r['family']!=n: r['note']=(r['note']+'; 색상 변형 (종류 수 중복 제외)').lstrip('; ')
        if r['package']=='Low Poly Weapons VOL1': r['note']=(r['note']+'; 원본 팩의 메시·텍스처 참조를 제공된 FBX·팔레트로 복구').lstrip('; ')
    assert len({r['id'] for r in records})==len(records)
    assert sum(r['category']=='modern' for r in records)==16
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'ItemCollection.json').write_text(json.dumps({'items':records,'excluded':excluded},ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'Tools/items/imported-assets.json').write_text(json.dumps(imported,ensure_ascii=False,indent=2),encoding='utf-8')
    print('Imported dependency files:',len(imported))
    print('Display entries:',dict(collections.Counter(r['category'] for r in records)))
    print('Excluded duplicate/component prefabs:',len(excluded))
    for a in archives:a.close()


if __name__=='__main__': main(sys.argv[1])
