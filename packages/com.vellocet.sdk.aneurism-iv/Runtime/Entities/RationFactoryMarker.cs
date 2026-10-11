using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_ration_factory")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Ration Factory")]
    public sealed class RationFactoryMarker : PrefabEntityMarker
    {
        [SerializeField] private GameItem tier1Resource = GameItem.Fish;
        [SerializeField] private GameItem tier2Resource = GameItem.SuspiciousMeat;
        public GameItem Tier1Resource => tier1Resource;
        public GameItem Tier2Resource => tier2Resource;
        public override string ArchetypeId => "aniv.ration-factory";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
