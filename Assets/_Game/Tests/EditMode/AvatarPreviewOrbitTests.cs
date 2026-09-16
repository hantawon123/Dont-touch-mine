using Game.Client.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Architecture.Tests
{
    public class AvatarPreviewOrbitTests
    {
        GameObject surface,stage,cameraObject,eventObject;
        AvatarPreviewOrbit orbit;Camera camera;EventSystem events;
        [SetUp] public void SetUp()
        {
            stage=new GameObject("stage");stage.transform.position=new Vector3(10000,10000,10000);
            cameraObject=new GameObject("camera");camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;
            surface=new GameObject("portrait",typeof(RectTransform));surface.GetComponent<RectTransform>().sizeDelta=new Vector2(560,700);
            eventObject=new GameObject("events");events=eventObject.AddComponent<EventSystem>();
            orbit=surface.AddComponent<AvatarPreviewOrbit>();orbit.Initialize(camera,stage.transform);
        }
        [TearDown] public void TearDown(){Object.DestroyImmediate(surface);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(stage);Object.DestroyImmediate(eventObject);}
        PointerEventData Pointer(Vector2 position,int id=1)=>new(events){position=position,pointerId=id,button=PointerEventData.InputButton.Left};
        [Test] public void HorizontalDragReachesBackWithoutMovingCharacter()
        {
            var original=stage.transform.position;var data=Pointer(Vector2.zero);orbit.OnBeginDrag(data);
            data.position=new Vector2(280,0);orbit.OnDrag(data);
            Assert.That(camera.transform.position.z,Is.LessThan(stage.transform.position.z));
            Assert.That(stage.transform.position,Is.EqualTo(original));
            Assert.That(Vector3.Distance(camera.transform.position,stage.transform.position+Vector3.up*1.20f),Is.EqualTo(5).Within(.01f));
        }
        [Test] public void VerticalDragDoesNotChangeView_AndDoubleClickRestoresFraming()
        {
            var original=camera.transform.position;var data=Pointer(Vector2.zero);orbit.OnBeginDrag(data);
            data.position=new Vector2(0,10000);orbit.OnDrag(data);Assert.That(orbit.Pitch,Is.EqualTo(6));
            data.position=new Vector2(0,-10000);orbit.OnDrag(data);Assert.That(orbit.Pitch,Is.EqualTo(6));
            Assert.That(Vector3.Distance(original,camera.transform.position),Is.LessThan(.001f));
            data.position=new Vector2(280,100);orbit.OnDrag(data);
            Assert.That(orbit.Yaw,Is.EqualTo(180).Within(.01f));
            orbit.OnEndDrag(data);data.clickCount=2;orbit.OnPointerClick(data);
            Assert.That(Vector3.Distance(original,camera.transform.position),Is.LessThan(.001f));
        }
        [Test] public void ScrollDoesNotZoom_AndFixedFramingIsLarger()
        {
            var data=Pointer(Vector2.zero);data.scrollDelta=new Vector2(0,1000);
            Assert.That(ExecuteEvents.Execute(surface,data,ExecuteEvents.scrollHandler),Is.False);
            data.scrollDelta=new Vector2(0,-1000);ExecuteEvents.Execute(surface,data,ExecuteEvents.scrollHandler);
            Assert.That(camera.orthographicSize,Is.EqualTo(1.32f));
            Assert.That(orbit.Yaw,Is.Zero);Assert.That(orbit.Pitch,Is.EqualTo(6));
        }
        [Test] public void OtherPointersAndEndedDragsCannotRotatePortrait()
        {
            var data=Pointer(Vector2.zero);orbit.OnBeginDrag(data);
            orbit.OnDrag(Pointer(new Vector2(100,100),2));Assert.That(orbit.Yaw,Is.Zero);
            orbit.OnEndDrag(data);data.position=new Vector2(100,100);orbit.OnDrag(data);Assert.That(orbit.Yaw,Is.Zero);
        }
        [Test] public void FirstDragFrameIncludesMovementSincePointerPress()
        {
            var data=Pointer(new Vector2(140,0));data.pressPosition=Vector2.zero;
            orbit.OnBeginDrag(data);orbit.OnDrag(data);
            Assert.That(orbit.Yaw,Is.EqualTo(90).Within(.01f));
        }
    }
}
