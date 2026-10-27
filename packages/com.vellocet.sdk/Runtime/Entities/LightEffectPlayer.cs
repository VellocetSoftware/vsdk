using UnityEngine;

namespace Vellocet.Sdk.Entities
{
    // Added only to effect lights. Ordinary lights have no ticking behaviour.
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    public sealed class LightEffectPlayer : MonoBehaviour
    {
        private static readonly float[] Flicker = Decode("mmnmmommommnonmmonqnmmo");
        private static readonly float[] Strobe = Decode("aaaaaaaazzzzzzzz");
        [SerializeField] private LightMarker.LightEffect effect = LightMarker.LightEffect.Candle;
        private Light _light;
        private LightMarker.LightEffect _effect;
        private float _intensity;
        private LightMarker _marker;
        private float _phase;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _marker = GetComponent<LightMarker>();
            _effect = _marker ? _marker.Effect : effect;
            _intensity = _light.intensity;
            _phase = (uint)GetInstanceID() % 997 * .173f;
        }

        private void OnEnable()
        {
            if (_effect != LightMarker.LightEffect.Candle) InvokeRepeating(nameof(Sample), 0, .1f);
        }
        private void Update()
        {
            if (_effect == LightMarker.LightEffect.Candle && _light && _light.enabled)
                _light.intensity = _intensity * CandleBrightness(Time.timeAsDouble, _phase);
        }
        private void OnDisable()
        {
            CancelInvoke();
            if (_light) _light.intensity = _intensity;
        }

        private void Sample()
        {
            if (_light && _light.enabled)
                _light.intensity = _intensity * (_effect == LightMarker.LightEffect.Custom && _marker && _marker.Style &&
                    float.IsFinite(_marker.CycleDuration) && _marker.CycleDuration > 0
                    ? Mathf.Max(0, _marker.Style.EvaluateIntensity((float)(Time.timeAsDouble % _marker.CycleDuration) / _marker.CycleDuration))
                    : Brightness(_effect, (long)(Time.timeAsDouble * 10)));
        }

        public static float Brightness(LightMarker.LightEffect effect, long tick)
        {
            if (effect == LightMarker.LightEffect.Candle) return CandleBrightness(tick * .1);
            var values = effect switch
            {
                LightMarker.LightEffect.Flicker => Flicker,
                LightMarker.LightEffect.Strobe => Strobe,
                _ => null
            };
            return values == null ? 1 : values[(int)((tick % values.Length + values.Length) % values.Length)];
        }

        public static float CandleBrightness(double seconds, float phase = 0)
        {
            var t = (float)seconds;
            var body = Mathf.PerlinNoise(31.17f + phase, t * 1.7f + 8.3f);
            var flutter = Mathf.PerlinNoise(73.91f + phase, t * 7.3f + 29.1f);
            var drift = Mathf.PerlinNoise(19.63f + phase, t * .43f + 53.7f);
            return .62f + .62f * (body * .65f + flutter * .2f + drift * .15f);
        }

        private static float[] Decode(string pattern)
        {
            var values = new float[pattern.Length];
            for (var i = 0; i < values.Length; i++) values[i] = (pattern[i] - 'a') / 12f;
            return values;
        }
    }
}
