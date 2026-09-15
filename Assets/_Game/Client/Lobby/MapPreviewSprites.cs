using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Lobby
{
    /// <summary>
    /// 맵 미리보기 사진. <c>Resources/UI/Maps/MapPreview_{mapId}</c>에서 읽고, 없으면 null을 돌려
    /// 기존 단색 상자를 그대로 보이게 한다(랜덤 선택·아직 사진이 없는 맵).
    /// </summary>
    /// <remarks>
    /// 사진은 에디터 메뉴 <c>Game/Match Map/Preview/Capture Map Preview From Main Camera</c>가
    /// 해당 맵 씬의 Main Camera 시점을 4:3으로 찍어 저장한다. UI 쪽 미리보기 크기(200×150, 100×75)와
    /// 같은 비율이라 늘어남 없이 채워진다.
    /// </remarks>
    public static class MapPreviewSprites
    {
        public const string ResourceFolder = "UI/Maps/";
        public const string ResourcePrefix = "MapPreview_";
        public const string PhotoChildName = "Photo";

        /// <summary>미리보기 사진의 가로:세로. 캡처 도구와 UI 상자 크기가 이 값을 공유한다.</summary>
        public const float Aspect = 4f / 3f;

        /// <summary>선택되지 않은 슬롯의 사진 톤. 선택 슬롯(흰색)과 구분되도록 살짝 어둡게.</summary>
        public static readonly Color DimmedTint = new(0.62f, 0.62f, 0.66f, 1f);

        private static readonly Dictionary<string, Sprite> Cache = new();

        /// <summary>맵 id의 리소스 경로. 랜덤(빈 id)은 null.</summary>
        public static string ResourcePath(string mapId)
        {
            var normalized = mapId?.Trim();
            return string.IsNullOrEmpty(normalized) ? null : ResourceFolder + ResourcePrefix + normalized;
        }

        /// <summary>맵 사진. 없으면 null. 결과는 있든 없든 한 번만 찾고 기억한다.</summary>
        public static Sprite For(string mapId)
        {
            var path = ResourcePath(mapId);
            if (path == null)
            {
                return null;
            }

            if (Cache.TryGetValue(path, out var cached) && cached != null)
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);
            Cache[path] = sprite;
            return sprite;
        }

        /// <summary>
        /// 둥근 상자(<paramref name="frame"/>) 안에 사진용 자식 Image를 만든다. 상자를 마스크로 써서
        /// 사진 모서리도 둥글게 잘리고, 사진이 없을 때는 자식을 꺼서 상자 색만 보인다.
        /// </summary>
        /// <param name="frame">사진을 담을 상자. 이 Image의 스프라이트 모양대로 잘린다.</param>
        /// <param name="size">사진 크기. 비우면 상자를 가득 채운다(4:3 상자용). 정사각 슬롯은 4:3 크기를 넘긴다.</param>
        public static Image AttachPhoto(Image frame, Vector2? size = null)
        {
            var mask = frame.GetComponent<Mask>() ?? frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var photoGo = new GameObject(PhotoChildName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            photoGo.transform.SetParent(frame.transform, false);
            var rect = (RectTransform)photoGo.transform;
            if (size.HasValue)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = size.Value;
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var photo = photoGo.GetComponent<Image>();
            photo.type = Image.Type.Simple;
            photo.preserveAspect = false;
            photo.raycastTarget = false;
            photo.color = Color.white;
            photoGo.SetActive(false);
            return photo;
        }

        /// <summary>사진을 넣거나(없으면) 숨긴다.</summary>
        public static void Apply(Image photo, Sprite sprite, Color? tint = null)
        {
            if (photo == null)
            {
                return;
            }

            photo.sprite = sprite;
            photo.color = tint ?? Color.white;
            photo.gameObject.SetActive(sprite != null);
        }

        /// <summary>이미 만들어진 상자에서 사진 자식을 찾는다(레이아웃 재사용 시).</summary>
        public static Image FindPhoto(Component frame)
        {
            var child = frame != null ? frame.transform.Find(PhotoChildName) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }
    }
}
