using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class PoweredMachineMarker : PrefabEntityMarker
    {
        [SerializeField] private PowerSourceMarker powerSource;
        public PowerSourceMarker PowerSource => powerSource;
        public abstract float DrainScale { get; }
    }
}
