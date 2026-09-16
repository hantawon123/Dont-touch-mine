"""Stage the project integration edits until the asset package is validated."""
from pathlib import Path
import re
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts/smooth-bear-unity/scripts'
OUT.mkdir(parents=True,exist_ok=True)

def replace_method(text,signature,replacement):
    start=text.index(signature)
    opening=text.index('{',start)
    depth=1;end=opening+1
    while depth:
        if text[end]=='{':depth+=1
        elif text[end]=='}':depth-=1
        end+=1
    return text[:start]+replacement+text[end:]

p=ROOT/'Assets/_Game/Editor/CharacterTestPreviewSetup.cs'
text=p.read_text(encoding='utf-8-sig')
states=re.findall(r'new\("Assets/Scenes/CharacterTest/First/[^"\n]+", "([^"]+)"',text)
start=text.index('        private static readonly MotionDefinition[] MotionDefinitions')
end=text.index('        [InitializeOnLoadMethod]',start)
text=text[:start]+'        private static readonly string[] MotionNames =\n        {\n'+''.join('            "'+s+'",\n' for s in states)+'        };\n\n'+text[end:]
text=text.replace('"Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx"','SmoothBearAssets.ModelPath')
text=text.replace('"Assets/Scenes/CharacterTest/First/FirstCharacterPreview.controller"','SmoothBearAssets.PreviewControllerPath')
text=text.replace('private const string PreviewName = "FirstPlayerCapsule"','private const string PreviewName = "SmoothBear"')
text=text.replace('        private const string Tag = "first-motion-preview-v2";\n','')
text=replace_method(text,'        public static bool ApplyBlenderPreview()', '''        public static bool ApplyBlenderPreview()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) == null ||
                MotionNames.Any(name => SmoothBearAssets.LoadClip(name) == null) ||
                SmoothBearAssets.LoadClip(IdleState) == null) return false;
            ConfigureController();
            AssignToScene();
            return true;
        }''')
text=replace_method(text,'        private static void ConfigurePreview(', '')
text=replace_method(text,'        private static void ConfigureMotion(', '')
text=text.replace('var clip = AssetDatabase.LoadAllAssetsAtPath(PreviewPath)\n                .OfType<AnimationClip>().First(c => c.name == IdleState);','var clip = SmoothBearAssets.LoadClip(IdleState);')
text=text.replace('foreach (var motion in MotionDefinitions)','foreach (var name in MotionNames)')
text=text.replace('var clipMotion = AssetDatabase.LoadAllAssetsAtPath(motion.Path)\n                    .OfType<AnimationClip>().FirstOrDefault(clip => clip.name == motion.State);','var clipMotion = SmoothBearAssets.LoadClip(name);')
text=text.replace('{motion.State} at {motion.Path}','{name}').replace('motion.State','name')
text=text.replace('MotionDefinitions.Select(motion => name)','MotionNames')
text=text.replace('            DestroyNamed("PlayerCapsule");','            DestroyNamed("FirstPlayerCapsule");\n            DestroyNamed("PlayerCapsule");')
text=replace_method(text,'        private static void ApplyProjectMaterials(','''        private static void ApplyProjectMaterials(GameObject preview) => SmoothBearAssets.ApplyMaterials(preview);''')
text=replace_method(text,'        private sealed class MotionDefinition','')
text=text.replace('First 캐릭터 FBX가 없습니다.','SmoothBear 모델 또는 애니메이션이 없습니다.').replace('First 캐릭터의 전체 동작','SmoothBear 캐릭터의 전체 동작').replace('First idle FBX','SmoothBear idle clip')
(OUT/'CharacterTestPreviewSetup.cs').write_text(text,encoding='utf-8')

p=ROOT/'Assets/_Game/Editor/FirstInGameSetup.cs'
text=p.read_text(encoding='utf-8-sig')
start=text.index('        private static readonly MotionDefinition[] Motions')
end=text.index('        private static bool appliedThisDomain',start)
text=text[:start]+text[end:]
text=text.replace('"Assets/Scenes/CharacterTest/First/FirstPlayerCapsule_Idle.fbx"','SmoothBearAssets.ModelPath')
text=re.sub(r'LoadClip\(\s*"Assets/Scenes/CharacterTest/First/[^"\n]+",\s*"([^"\n]+)"\)',r'SmoothBearAssets.LoadClip("\1")',text)
text=text.replace('LoadClip(IdlePath, IdleState)','SmoothBearAssets.LoadClip(IdleState)')
start=text.index('            foreach (var motion in Motions)')
end=text.index('            BindState(machine, "Punch"',start)
text=text[:start]+'''            foreach (var clip in SmoothBearAssets.LoadClips())
                BindState(machine, clip.name, clip);

'''+text[end:]
text=re.sub(r'        private static AnimationClip LoadClip\(string path, string state\) =>[\s\S]+?;\n','',text,count=1)
text=replace_method(text,'        private static void ApplyProjectMaterials(','''        private static void ApplyProjectMaterials(GameObject visual) => SmoothBearAssets.ApplyMaterials(visual);''')
text=replace_method(text,'        private readonly struct MotionDefinition','')
text=text.replace('            appliedThisDomain = true;','            SmoothBearAssets.SaveCharacterPrefab();\n            appliedThisDomain = true;')
text=text.replace('First Generic','SmoothBear Generic').replace('First 메시','SmoothBear 메시').replace('First 클립','SmoothBear 클립').replace('First 모습','SmoothBear 모습').replace('First In-Game','SmoothBear In-Game').replace('Apply First In-Game Character','Apply SmoothBear In-Game Character')
(OUT/'FirstInGameSetup.cs').write_text(text,encoding='utf-8')
print('STAGED integration scripts; preview motions',len(states))
