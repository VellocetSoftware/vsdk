using Vellocet.Sdk.Entities.Signals;
namespace Vellocet.Sdk.Entities.States
{
    public readonly struct LightEntityState : IEntityStateSnapshot
    {
        public LightEntityState(bool isOn, string color) { IsOn = isOn; Color = color; }
        public bool IsOn { get; }
        public string Color { get; }
        public string StateType => nameof(LightEntityState);
    }
}
