using UnityEngine;

namespace Vellocet.Sdk.Entities
{
    // Added only to effect lights. Ordinary lights have no ticking behaviour.
    [DisallowMultipleComponent]
    public sealed class LightEffectPlayer : MonoBehaviour
    {
        private static readonly float[] Flicker = Decode("mmnmmommommnonmmonqnmmo");
        private static readonly float[] Strobe = Decode("aaaaaaaazzzzzzzz");
        private static readonly float[] Candle = Decode("mmmaaaabcdefgmmmmaaaammmaamm");
        private Light _light;
        private LightMarker.LightEffect _effect;
        private float _intensity;
        private LightMarker _marker;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _marker = GetComponent<LightMarker>();
            _effect = _marker.Effect;
            _intensity = _light.intensity;
        }

        private void OnEnable() => InvokeRepeating(nameof(Sample), 0, .1f);
        private void OnDisable()
        {
            CancelInvoke();
            if (_light) _light.intensity = _intensity;
        }

        private void Sample()
        {
            if (_light && _light.enabled)
                _light.intensity = _intensity * (_effect == LightMarker.LightEffect.Custom && _marker.Style &&
                    float.IsFinite(_marker.CycleDuration) && _marker.CycleDuration > 0
                    ? Mathf.Max(0, _marker.Style.EvaluateIntensity((float)(Time.timeAsDouble % _marker.CycleDuration) / _marker.CycleDuration))
                    : Brightness(_effect, (long)(Time.timeAsDouble * 10)));
        }

        public static float Brightness(LightMarker.LightEffect effect, long tick)
        {
            var values = effect switch
            {
                LightMarker.LightEffect.Flicker => Flicker,
                LightMarker.LightEffect.Strobe => Strobe,
                LightMarker.LightEffect.Candle => Candle,
                _ => null
            };
            return values == null ? 1 : values[(int)((tick % values.Length + values.Length) % values.Length)];
        }

        private static float[] Decode(string pattern)
        {
            var values = new float[pattern.Length];
            for (var i = 0; i < values.Length; i++) values[i] = (pattern[i] - 'a') / 12f;
            return values;
        }
    }
}
