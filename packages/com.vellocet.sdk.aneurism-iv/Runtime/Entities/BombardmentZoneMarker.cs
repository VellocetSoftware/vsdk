using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("env_bombardment")]
    [EntityAuthoring("Wartime", EntityPlacementKind.Point, "Bombardment Zone")]
    public sealed class BombardmentZoneMarker : PrefabEntityMarker
    {
        [SerializeField, Min(.01f)] private float radius = 32;
        [SerializeField, Tooltip("Start during wartime and stop when wartime ends.")] private bool followWartime = true;
        [SerializeField] private bool automaticBombardment = true;
        public bool AutomaticBombardment => automaticBombardment;
        public float Radius => radius;
        public bool FollowWartime => followWartime;
        public override string ArchetypeId => "aniv.bombardment-zone";
        public override string[] Inputs => new[] { "Start", "Stop", "DropBomb" };
        public override string[] Outputs => new[] { "OnStarted", "OnStopped", "OnBombDropped" };
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            foreach (var input in Inputs) builder.AddInput(input, EntitySignalValueType.None);
            foreach (var output in Outputs) builder.AddOutput(output, EntitySignalValueType.None);
            builder.AddProperty("is_running", EntitySignalValueType.Bool, true, false);
        }
    }
}
