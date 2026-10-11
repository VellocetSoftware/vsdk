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

        private void Awake()
        {
            _light = GetComponent<Light>();
            _effect = GetComponent<LightMarker>().Effect;
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
                _light.intensity = _intensity * Brightness(_effect, (long)(Time.timeAsDouble * 10));
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
