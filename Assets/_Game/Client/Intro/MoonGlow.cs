using System.Reflection;
using UnityEngine;

namespace Game.Client.Intro
{
    /// <summary>
    /// 가운데 고정된 달빛이 아주 미세하게 "숨쉬게" 만든다.
    /// 위치는 절대 움직이지 않는다 (요구사항: 조명 위치 가운데 고정).
    ///
    /// Light2D 를 직접 참조하지 않고 리플렉션으로 intensity 만 건드린다.
    /// 덕분에 이 스크립트는 URP 가 없거나 Light2D 를 안 쓰는 프로젝트에서도 그대로 컴파일된다.
    /// lightObject 가 비어 있으면 달무리 크기만 호흡시킨다.
    /// </summary>
    public class MoonGlow : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("가운데 Point Light 2D. 비워두면 조명은 건드리지 않는다.")]
        public Component lightObject;

        public SpriteRenderer glowSprite;   // moon_glow
        public SpriteRenderer discSprite;   // moon_disc

        [Header("호흡")]
        [Tooltip("한 번 밝아졌다 어두워지는 데 걸리는 시간(초). 5초 루프면 5의 약수로.")]
        public float period = 5f;

        [Tooltip("빛 세기 변화폭 (0.05 = ±5%)")]
        [Range(0f, 0.3f)] public float intensitySwing = 0.05f;

        [Tooltip("달무리 크기 변화폭")]
        [Range(0f, 0.2f)] public float scaleSwing = 0.02f;

        PropertyInfo _intensity;
        float _baseIntensity;
        Vector3 _baseGlowScale;

        void Awake()
        {
            if (lightObject != null)
            {
                _intensity = lightObject.GetType().GetProperty("intensity");
                if (_intensity != null)
                    _baseIntensity = (float)_intensity.GetValue(lightObject);
            }
            if (glowSprite != null) _baseGlowScale = glowSprite.transform.localScale;
        }

        void LateUpdate()
        {
            float s = Mathf.Sin(Mathf.PI * 2f * IntroLoopController.Now() / Mathf.Max(0.01f, period));

            if (_intensity != null)
                _intensity.SetValue(lightObject, _baseIntensity * (1f + s * intensitySwing));

            if (glowSprite != null)
                glowSprite.transform.localScale = _baseGlowScale * (1f + s * scaleSwing);
        }
    }
}
