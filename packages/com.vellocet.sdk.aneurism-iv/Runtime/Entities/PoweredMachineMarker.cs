using UnityEngine;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class PoweredMachineMarker : OperationEntityMarker
    {
        [SerializeField] private PowerSourceMarker powerSource;
        public PowerSourceMarker PowerSource => powerSource;
        public abstract float DrainScale { get; }
        public override string[] Inputs => new[] { "Start", "Stop", "Toggle" };
        public override string[] Outputs => new[] { "OnStarted", "OnStopped" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("Start", EntitySignalValueType.None);
            builder.AddInput("Stop", EntitySignalValueType.None);
            builder.AddInput("Toggle", EntitySignalValueType.None);
            builder.AddOutput("OnStarted", EntitySignalValueType.None);
            builder.AddOutput("OnStopped", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_running", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("has_power", EntitySignalValueType.Bool, true, false);
        }
    }
}
