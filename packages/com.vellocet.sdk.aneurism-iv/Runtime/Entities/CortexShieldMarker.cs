using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Tooling;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_cortex_shield")]
    [EntityAuthoring("Security", EntityPlacementKind.Volume, "Cortex Shield",
        Description = "A centered security trigger. Draw or resize its bounds to fit the doorway. Input volume optionally overrides the contact region.")]
    public sealed class CortexShieldMarker : ContactOperationMarker, ICenteredBoundsEntityMarker, IToolVolumeVisualProvider
    {
        // Existing Unity scenes keep their local dimensions; Vertex uses the entity bounds.
        [SerializeField, HideInInspector] private Vector3 size = Vector3.one;
        [SerializeField] private bool eventBased;
        [SerializeField] private string[] exceptionFates = { "liquidator", "rogue" };
        [SerializeField] private bool wantedTrespassers;
        [SerializeField] private bool killTrespassers = true;
        public Vector3 Size => size;
        public Vector3 BoundsSize
        {
            get
            {
                var scaled = Vector3.Scale(transform.lossyScale, size);
                return new Vector3(Mathf.Abs(scaled.x), Mathf.Abs(scaled.y), Mathf.Abs(scaled.z));
            }
        }

        public bool TryGetToolVolumeVisual(out ToolVolumeVisualData data)
        {
            data = new ToolVolumeVisualData(Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one),
                Vector3.zero, BoundsSize, new Color(.2f, .5f, 1f, .12f), new Color(.2f, .5f, 1f, .9f));
            return isActiveAndEnabled && gameObject.activeInHierarchy;
        }
        public bool EventBased => eventBased;
        public System.Collections.Generic.IReadOnlyList<string> ExceptionFates => exceptionFates;
        public bool WantedTrespassers => wantedTrespassers;
        public bool KillTrespassers => killTrespassers;
        public override string ArchetypeId => "aniv.cortex-shield";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddOutput("passed", EntitySignalValueType.None);
            builder.AddOutput("trespass_detected", EntitySignalValueType.None);
            builder.AddOutput("enforcement_applied", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
