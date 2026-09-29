using System.IO;
using System.Linq;
using Game.Client.Character;
using Game.Client.Home;
using Game.Core.Flow;
using Game.Core.Players;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>Local preview using the production closet view and presenter.</summary>
    [InitializeOnLoad]
    public static class SmoothBearClosetPreview
    {
        const string Request="artifacts/smooth-bear-unity/customization/preview.request";
        const string Pending="SmoothBearClosetPreview.Pending";
        static CharacterClosetPresenter presenter;
        static GameObject root;
        static SmoothBearClosetPreview()
        {
            EditorApplication.update+=Check;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.ExitingPlayMode){presenter?.Dispose();presenter=null;}
            };
        }
        [MenuItem("Game/Preview/Animal hood customization")]
        public static void Open()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Request));File.WriteAllText(Request,"open");
        }
        static void Check()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(File.Exists(Request))
            {
                var command=File.ReadAllText(Request);File.Delete(Request);
                if(command=="open-isolated"&&!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    if(!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/CharacterTest.unity");
                }
                if(command=="capture"&&root!=null){Capture();return;}
                if(command=="orbit-check"&&root!=null){CheckOrbit();return;}
                if(command=="framing-check"&&root!=null){CheckFraming();return;}
                if(command=="colour-review"&&Application.isPlaying){ReviewColours();return;}
                SessionState.SetBool(Pending,true);
                if(!EditorApplication.isPlayingOrWillChangePlaymode)EditorApplication.EnterPlaymode();
            }
            if(!SessionState.GetBool(Pending,false)||!Application.isPlaying)return;
            SessionState.SetBool(Pending,false);
            if(root!=null)return;
            foreach(var driver in Object.FindObjectsByType<Game.Client.CharacterTestPreviewDriver>(FindObjectsSortMode.None))driver.enabled=false;
            root=new GameObject("LocalCustomizationPreview");
            var view=root.AddComponent<CharacterClosetView>();
            var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
            var flow=new AppFlowSystem();flow.TryTransitionTo(AppFlowState.CharacterCloset);
            presenter=new CharacterClosetPresenter(view,catalog,new AvatarAppearanceState(),new LocalHost(),flow,()=>EditorApplication.ExitPlaymode());
            presenter.Start();
            Debug.Log("CLOSET_PREVIEW_READY");
        }
        static void Capture()
        {
            var portrait=root.GetComponentsInChildren<RawImage>().Single(r=>r.name=="CharacterPortrait");
            var rt=(RenderTexture)portrait.texture;var previous=RenderTexture.active;
            RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            File.WriteAllBytes("artifacts/smooth-bear-unity/customization/closet-portrait.png",image.EncodeToPNG());
            Object.Destroy(image);RenderTexture.active=previous;
            File.WriteAllText("artifacts/smooth-bear-unity/customization/preview-selection.txt",presenter.Draft.BodyColorId+" / "+presenter.Draft.HoodId);
        }
        static void CheckOrbit()
        {
            const string output="artifacts/smooth-bear-unity/wearables";
            try
            {
                var orbit=root.GetComponentInChildren<AvatarPreviewOrbit>();
                var rect=(RectTransform)orbit.transform;var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=1,position=center,pressPosition=center,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
                var target=hits.First().gameObject;
                if(UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IDragHandler>(target)!=orbit.gameObject)throw new System.Exception("Portrait does not receive drag input");
                var portrait=orbit.GetComponent<RawImage>();var rt=(RenderTexture)portrait.texture;
                var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c=>c.targetTexture==rt);
                orbit.ResetView();
                UnityEngine.EventSystems.ExecuteEvents.Execute(orbit.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.beginDragHandler);
                data.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.center.x+rect.rect.width*.5f,rect.rect.center.y+rect.rect.height*.3f,0)));
                UnityEngine.EventSystems.ExecuteEvents.Execute(orbit.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.dragHandler);
                UnityEngine.EventSystems.ExecuteEvents.Execute(orbit.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.endDragHandler);
                if(Mathf.Abs(orbit.Yaw-180)>1||Mathf.Abs(orbit.Pitch-6)>.01f)throw new System.Exception("Horizontal-only orbit failed");
                var fixedSize=camera.orthographicSize;
                data.scrollDelta=new Vector2(0,120);
                UnityEngine.EventSystems.ExecuteEvents.Execute(orbit.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.scrollHandler);
                if(Mathf.Abs(camera.orthographicSize-fixedSize)>.001f)throw new System.Exception("Wheel changed fixed framing");
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                Capture();File.Copy("artifacts/smooth-bear-unity/customization/closet-portrait.png",output+"/orbit-back.png",true);
                data.clickCount=2;UnityEngine.EventSystems.ExecuteEvents.Execute(orbit.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                if(Mathf.Abs(orbit.Yaw)>.01f||Mathf.Abs(orbit.Pitch-6)>.01f)throw new System.Exception("Front reset failed");
                File.WriteAllText(output+"/orbit-ui-result.json","{\"success\":true,\"portraitRaycast\":true,\"horizontalOnly\":true,\"wheelDisabled\":true,\"doubleClickReset\":true}");
            }
            catch(System.Exception e){File.WriteAllText(output+"/orbit-ui-result.json",e.ToString());Debug.LogException(e);}
        }
        static void CheckFraming()
        {
            const string output="artifacts/smooth-bear-unity/wearables/framing-result.txt";
            var orbit=root.GetComponentInChildren<AvatarPreviewOrbit>();
            var rt=orbit.GetComponent<RawImage>().texture;
            var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c=>c.targetTexture==rt);
            var applier=camera.transform.parent.GetComponentInChildren<AvatarAppearanceApplier>();
            var original=applier.Current;
            var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
            var report=new System.Text.StringBuilder();
            try
            {
                foreach(var hood in catalog.Find(AvatarPartCategory.Hood).Parts)
                {
                    applier.Apply(catalog.Default.With(AvatarPartCategory.Hood,hood.Id));
                    orbit.ResetView();
                    var rect=(RectTransform)orbit.transform;
                    var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                    var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=1,position=center,pressPosition=center,button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
                    orbit.OnBeginDrag(data);
                    for(int step=0;step<8;step++)
                    {
                        data.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(new Vector3(rect.rect.center.x+rect.rect.width*step/8f,rect.rect.center.y,0)));
                        orbit.OnDrag(data);
                        var min=Vector2.one;var max=Vector2.zero;
                        foreach(var renderer in applier.GetComponentsInChildren<Renderer>().Where(r=>r.enabled))
                        {
                            Mesh mesh=null;bool baked=false;
                            if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh);baked=true;}
                            else mesh=renderer.GetComponent<MeshFilter>()?.sharedMesh;
                            if(mesh==null)continue;
                            foreach(var vertex in mesh.vertices)
                            {
                                var p=(Vector2)camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex));
                                min=Vector2.Min(min,p);max=Vector2.Max(max,p);
                            }
                            if(baked)Object.DestroyImmediate(mesh);
                        }
                        report.AppendLine($"{hood.Id} yaw={step*45}: min={min:F3} max={max:F3}");
                        if(min.x<.02f||min.y<.02f||max.x>.98f||max.y>.98f)throw new System.Exception("Character too close to portrait edge");
                    }
                    orbit.OnEndDrag(data);
                }
                report.AppendLine("PASS: all 4 hoods at 8 angles fit with at least 2% margin.");
            }
            catch(System.Exception e){report.AppendLine(e.ToString());Debug.LogException(e);}
            finally{applier.Apply(original);orbit.ResetView();File.WriteAllText(output,report.ToString());}
        }
        static void ReviewColours()
        {
            const string output="artifacts/smooth-bear-unity/wearables";
            var view=Object.FindObjectsByType<CharacterClosetView>(FindObjectsSortMode.None).First(v=>v.isActiveAndEnabled);
            var portrait=view.GetComponentsInChildren<RawImage>().Single(r=>r.name=="CharacterPortrait");
            var rt=(RenderTexture)portrait.texture;
            var camera=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c=>c.targetTexture==rt);
            var applier=camera.transform.parent.GetComponentInChildren<AvatarAppearanceApplier>();
            var original=applier.Current;var oldRt=RenderTexture.active;
            var key=camera.transform.Find("PortraitLight").GetComponent<Light>();
            var fill=camera.transform.Find("PortraitFillLight").GetComponent<Light>();
            var bounce=camera.transform.Find("PortraitBounceLight").GetComponent<Light>();
            var catalog=AssetDatabase.LoadAssetAtPath<AvatarPartCatalog>("Assets/_Game/Content/Config/AvatarPartCatalog.asset");
            try
            {
                var hint=view.GetComponentsInChildren<TMPro.TMP_Text>().Single(t=>t.name=="PortraitControlsHint");
                if(hint.color!=Color.black)throw new System.Exception("Hint must be black");
                foreach(var name in new[]{"red_vivid","lilac_light","mint","blue_vivid"})
                {
                    applier.Apply(catalog.Default.With(AvatarPartCategory.BodyColor,"body_"+name).With(AvatarPartCategory.Shoes,"shoes_"+name));
                    for(int pass=0;pass<2;pass++)
                    {
                        key.intensity=pass==0?12:10;fill.intensity=pass==0?5:9;
                        fill.transform.localPosition=pass==0?new Vector3(2,0,1):new Vector3(2,1,1);
                        bounce.enabled=pass==1;
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                        RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
                        image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                        File.WriteAllBytes(output+"/colour-"+name+(pass==0?"-before.png":"-after.png"),image.EncodeToPNG());Object.Destroy(image);
                    }
                }
                File.WriteAllText(output+"/colour-review-result.json","{\"success\":true,\"blackHint\":true,\"colourComparisons\":4}");
            }
            finally
            {
                applier.Apply(original);RenderTexture.active=oldRt;key.intensity=10;fill.intensity=9;
                fill.transform.localPosition=new Vector3(2,1,1);bounce.enabled=true;
            }
        }
        sealed class LocalHost:IHomeApplicationHost
        {
            public void Quit(){} public void OpenHome(){} public void OpenRoomBrowser(){}
            public void OpenCharacterCloset(){} public void OpenSettings(){} public void OpenTutorial(){}
            public void CreateRoom(string title,bool isPublic,int maxPlayers){}
            public void JoinRoom(string code){} public void OpenLobby(){}
        }
    }
}
