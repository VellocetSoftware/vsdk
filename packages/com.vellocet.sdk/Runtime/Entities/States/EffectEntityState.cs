using Vellocet.Sdk.Entities.Signals;
namespace Vellocet.Sdk.Entities.States
{
    public readonly struct EffectEntityState : IEntityStateSnapshot
    {
        public EffectEntityState(string kind, bool active, float amount, float radius, float force,
            float duration, float frequency, float elapsed)
        { Kind = kind; Active = active; Amount = amount; Radius = radius; Force = force;
          Duration = duration; Frequency = frequency; Elapsed = elapsed; }
        public string Kind { get; }
        public bool Active { get; }
        public float Amount { get; }
        public float Radius { get; }
        public float Force { get; }
        public float Duration { get; }
        public float Frequency { get; }
        public float Elapsed { get; }
        public string StateType => nameof(EffectEntityState);
    }
}
