using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Client.Character
{
    /// <summary>Pointer gestures belong to the portrait, leaving the colour grid's scroll alone.</summary>
    [DisallowMultipleComponent]
    public sealed class AvatarPreviewOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler
    {
        private Camera portraitCamera;
        private Transform stage;
        private RectTransform surface;
        private Vector2 previousPoint;
        private int draggingPointer=int.MinValue;
        private float yaw;
        private const float pitch=6f;
        private const float DefaultSize=1.32f;
        public float Yaw => yaw;
        public float Pitch => pitch;

        public void Initialize(Camera camera,Transform previewStage)
        {
            portraitCamera=camera;stage=previewStage;surface=(RectTransform)transform;
            ResetView();
        }
        public void ResetView()
        {
            yaw=0;draggingPointer=int.MinValue;
            if(portraitCamera==null)return;
            portraitCamera.orthographicSize=DefaultSize;UpdateCamera();
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if(data.button!=PointerEventData.InputButton.Left||portraitCamera==null||draggingPointer!=int.MinValue)return;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,data.pressPosition,data.pressEventCamera,out previousPoint))draggingPointer=data.pointerId;
        }
        public void OnDrag(PointerEventData data)
        {
            if(data.pointerId!=draggingPointer||portraitCamera==null)return;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,data.position,data.pressEventCamera,out var point))return;
            var delta=point-previousPoint;previousPoint=point;
            yaw=Mathf.Repeat(yaw+delta.x/Mathf.Max(1,surface.rect.width)*360f,360f);
            UpdateCamera();
        }
        public void OnEndDrag(PointerEventData data){if(data.pointerId==draggingPointer)draggingPointer=int.MinValue;}
        public void OnPointerClick(PointerEventData data)
        {
            if(data.button==PointerEventData.InputButton.Left&&data.clickCount==2)ResetView();
        }
        private void OnDisable(){draggingPointer=int.MinValue;}
        private void UpdateCamera()
        {
            if(stage==null)return;
            // Include the tallest rabbit ears and the soles at every horizontal angle.
            var target=stage.TransformPoint(new Vector3(0,1.20f,0));
            var offset=Quaternion.Euler(-pitch,yaw,0)*Vector3.forward*5f;
            portraitCamera.transform.position=target+stage.rotation*offset;
            portraitCamera.transform.LookAt(target,stage.up);
        }
    }
}
