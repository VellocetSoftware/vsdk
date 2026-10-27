using System;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class PrefabEntityMarker : EntityMarker, IMapModelMarker, IEntityInteractionMarker, IEntityImportHierarchy
    {
        [UnityEngine.SerializeField, UnityEngine.Header("Persistence"), UnityEngine.Tooltip("Optional stable storage key. Leave empty to derive it from this placement; retain an existing key when relocating storage or changing its prefab.")]
        private string persistenceKey = "";
        public string PersistenceKey => persistenceKey;
        public abstract string ArchetypeId { get; }
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
        public virtual bool SupportsInteraction => false;
    }
}
