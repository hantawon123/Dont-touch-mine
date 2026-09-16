using System.Linq;
using Game.Client.Character;
using Game.Core.Players;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Architecture.Tests
{
    public class AvatarWearableTests
    {
        GameObject avatar,other;
        AvatarPartCatalog catalog;
        AvatarAppearanceApplier applier;
        [SetUp] public void SetUp()
        {
            catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
            var prefab=Resources.Load<GameObject>("AvatarPreview");
            avatar=Object.Instantiate(prefab);other=Object.Instantiate(prefab);
            applier=avatar.GetComponent<AvatarAppearanceApplier>();
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(avatar);Object.DestroyImmediate(other);}
        [Test] public void NewPlayerAndOlderSavedAppearanceWearDefaultShoes()
        {
            var fresh = applier.ResolvePlayerAppearance(AvatarAppearance.Default);
            Assert.That(fresh, Is.EqualTo(catalog.Default));
            Assert.That(fresh.ShoesId, Is.EqualTo("shoes_blue"));
            applier.Apply(fresh);
            Assert.That(avatar.GetComponentsInChildren<Renderer>()
                .Count(r => r.name.StartsWith("Wearable_CompactShoes")), Is.EqualTo(2));

            var saved = catalog.Default.With(AvatarPartCategory.BodyColor, "body_orange_vivid")
                .With(AvatarPartCategory.Shoes, AvatarAppearance.NoPart);
            var completed = applier.ResolvePlayerAppearance(saved);
            Assert.That(completed.ShoesId, Is.EqualTo("shoes_blue"));
            Assert.That(completed.BodyColorId, Is.EqualTo(saved.BodyColorId));
            Assert.That(completed.HoodId, Is.EqualTo(saved.HoodId));
            Assert.That(completed.FaceId, Is.EqualTo(saved.FaceId));
        }
        [Test] public void NetworkedPlayerSupportsEveryClosetSelection()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Content/Prefabs/NetworkedPlayer.prefab");
            var player = Object.Instantiate(prefab);
            try
            {
                var dresser = player.GetComponentInChildren<AvatarAppearanceApplier>(true);
                Assert.That(dresser, Is.Not.Null);
                foreach (var category in new[] { AvatarPartCategory.BodyColor, AvatarPartCategory.Hood,
                    AvatarPartCategory.HoodColor, AvatarPartCategory.Shoes, AvatarPartCategory.Face })
                foreach (var part in catalog.Find(category).Parts)
                {
                    var selected = catalog.Default.With(category, part.Id);
                    Assert.That(new[] { selected.BodyColorId, selected.HoodId, selected.ShoesId, selected.FaceId }
                        .All(id => id.Length <= 64), Is.True, "Part id must fit replicated state");
                    dresser.Apply(selected);
                    Assert.That(dresser.Current, Is.EqualTo(selected));
                    Assert.That(player.GetComponentsInChildren<Renderer>(true)
                        .Any(r => r.name.StartsWith("Wearable_CompactShoes") && r.enabled), Is.True);
                    if (category == AvatarPartCategory.Hood)
                        Assert.That(player.GetComponentsInChildren<MeshFilter>(true).Single(m => m.name == "Hood").sharedMesh,
                            Is.EqualTo(part.Mesh));
                    if (category == AvatarPartCategory.BodyColor)
                    {
                        var body = player.GetComponentsInChildren<Renderer>(true).Single(r => r.name == "Body");
                        var properties = new MaterialPropertyBlock();
                        body.GetPropertyBlock(properties, 0);
                        Assert.That(Vector4.Distance(properties.GetColor("_BaseColor"), part.Swatch), Is.LessThan(.00001f));
                    }
                }
            }
            finally { Object.DestroyImmediate(player); }
        }
        [Test] public void FaceSwitch_RestoresEyes_AndDoesNotAffectAnotherAvatar()
        {
            foreach(var face in new[]{"face_sparkle","face_sleepy","face_fierce","face_brown","face_default"})
            {
                applier.Apply(catalog.Default.With(AvatarPartCategory.Face,face));
                Assert.That(avatar.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Eye_")).All(r=>r.enabled==(face=="face_default")),Is.True);
                Assert.That(other.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Eye_")).All(r=>r.enabled),Is.True);
            }
        }
        [Test] public void BrownEyesKeepPrintedIrisesAndTintAllFourLidsWithTheBody()
        {
            Assert.That(catalog.TryFind(AvatarPartCategory.Face,"face_brown",out var face),Is.True);
            Assert.That(face.Thumbnail,Is.Not.Null);
            Assert.That(face.Wearable.attachments.Length,Is.EqualTo(6));
            foreach(var body in catalog.Find(AvatarPartCategory.BodyColor).Parts)
            {
                var appearance=catalog.Default.With(AvatarPartCategory.Face,face.Id).With(AvatarPartCategory.BodyColor,body.Id);
                Assert.That(catalog.Normalise(appearance),Is.EqualTo(appearance));
                applier.Apply(appearance);
                var lids=avatar.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Wearable_Brown")&&r.name.Contains("Lid")).ToArray();
                Assert.That(lids,Has.Length.EqualTo(4));
                foreach(var lid in lids)
                {
                    Assert.That(lid.transform.parent.name,Is.EqualTo("Head"));
                    var block=new MaterialPropertyBlock();lid.GetPropertyBlock(block,0);
                    // MaterialPropertyBlock may round during the colour-space roundtrip.
                    Assert.That(Vector4.Distance(block.GetColor("_BaseColor"),body.Swatch),Is.LessThan(.00001f));
                }
            }
            var whites=face.Wearable.attachments.Where(a=>a.name.Contains("Eye_White")).ToArray();
            Assert.That(whites,Has.Length.EqualTo(2));
            Assert.That(whites.All(a=>a.materials.Any(m=>m.GetTexture("_BaseMap")!=null)),Is.True);
        }
        [Test] public void ShoesFollowFeet_AndUseThePreviewLayer()
        {
            foreach(var t in avatar.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            applier.Apply(catalog.Default);
            var shoes=avatar.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Wearable_CompactShoes")).ToArray();
            Assert.That(shoes,Has.Length.EqualTo(2));
            foreach(var shoe in shoes)
            {
                Assert.That(shoe.gameObject.layer,Is.EqualTo(31));
                Assert.That(shoe.transform.parent.name,Does.StartWith("Foot."));
                var before=shoe.bounds.center;shoe.transform.parent.localPosition+=Vector3.up*.2f;
                Assert.That(Vector3.Distance(before,shoe.bounds.center),Is.GreaterThan(.1f));
                Assert.That(shoe.transform.localPosition,Is.EqualTo(Vector3.zero));
            }
        }
        [Test] public void RepeatedSelectionsReuseMeshesAndMaterials()
        {
            foreach(var face in catalog.Find(AvatarPartCategory.Face).Parts)applier.Apply(catalog.Default.With(AvatarPartCategory.Face,face.Id));
            var count=avatar.GetComponentsInChildren<Renderer>(true).Length;
            for(int n=0;n<10;n++)foreach(var face in catalog.Find(AvatarPartCategory.Face).Parts)applier.Apply(catalog.Default.With(AvatarPartCategory.Face,face.Id));
            Assert.That(avatar.GetComponentsInChildren<Renderer>(true),Has.Length.EqualTo(count));
            foreach(var renderer in avatar.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Wearable_")))
                Assert.That(renderer.sharedMaterials.All(AssetDatabase.Contains),Is.True);
        }
        [Test] public void MissingBoneKeepsOriginalEyesVisible()
        {
            var head=avatar.GetComponentsInChildren<Transform>().Single(t=>t.name=="Head");head.name="UnboundHead";
            applier.Apply(catalog.Default.With(AvatarPartCategory.Face,"face_sparkle"));
            Assert.That(avatar.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Eye_")).All(r=>r.enabled),Is.True);
        }
        [Test] public void ExpandedPaletteKeepsApprovedColoursAndAddsNineBrightOptions()
        {
            var expected=new[]{"FF8A24","FFCB77","FFE033","D9ED92","CCD5AE","95D5B2","27D85F","43AA8B","20CDF5","BDE0FE","BBD0FF","3478FF","0466C8","002855","8E9AAF","E4CCFF","CDB4DB","9381FF","8B4DFF","FFCAD4","FF4FA3","DA627D","FF3B3B","E63946","F8F9FA","CED4DA","495057","212529"};
            foreach(var category in new[]{AvatarPartCategory.BodyColor,AvatarPartCategory.HoodColor,AvatarPartCategory.Shoes})
            {
                var parts=catalog.Find(category).Parts;
                Assert.That(parts.Select(p=>ColorUtility.ToHtmlStringRGB(p.Swatch)),Is.EqualTo(expected));
                Assert.That(parts.Select(p=>p.Id).Distinct().Count(),Is.EqualTo(28));
                if(category==AvatarPartCategory.Shoes)Assert.That(parts.All(p=>p.Wearable!=null&&p.Thumbnail!=null),Is.True);
            }
        }
        [Test] public void PaletteReorderingKeepsDefaultOutfitAndRetiredColoursResolveToIt()
        {
            Assert.That(catalog.Default.BodyColorId,Is.EqualTo("body_lemon"));
            Assert.That(catalog.Default.HoodColorId,Is.EqualTo("hood_ice"));
            Assert.That(catalog.Default.ShoesId,Is.EqualTo("shoes_blue"));
            var retired=catalog.Default.With(AvatarPartCategory.BodyColor,"body_cocoa").With(AvatarPartCategory.HoodColor,"hood_cocoa").With(AvatarPartCategory.Shoes,"shoes_orange");
            Assert.That(catalog.Normalise(retired),Is.EqualTo(catalog.Default));
        }
    }
}
