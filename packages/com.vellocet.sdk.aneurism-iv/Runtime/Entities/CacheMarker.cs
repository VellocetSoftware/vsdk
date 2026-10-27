using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_supply_cache")]
    [EntityAuthoring("Shipping", EntityPlacementKind.Point, "Supply Cache")]
    public sealed class CacheMarker : OperationEntityMarker
    {
        public enum Kind { Cache, Supplier }
        public enum WorldEvent { None, Wartime, LimitationOrder, Rotting, Lost, Darkness }
        public enum SupplyType { Instance, Inventory }

        [Serializable]
        public sealed class Interaction
        {
            public string eventName = "Take supplies.";
            public Sprite eventIcon;
            public string[] allowedDoerFates = Array.Empty<string>();
            public int allowedDoerAttributes;
            public bool doerIsWhitelist = true;
            public string[] allowedReceiverFates = Array.Empty<string>();
            public int allowedReceiverAttributes;
            public bool receiverIsWhitelist = true;
            [Range(-100, 100)] public int priority;
            public bool requiresOwnership;
        }

        [Serializable]
        public sealed class Supply
        {
            public string item = GameItem.Screwdriver;
            public SupplyType supplyType = SupplyType.Inventory;
            public bool checkIfHasItem = true;
            public bool ignoreWeight;
            [Min(0)] public float takeRate = 300;
            [Range(0, 1)] public float minKarma;
            public Interaction interaction = new();
        }

        [SerializeField] private Kind kind = Kind.Supplier;
        [SerializeField] private WorldEvent eventRestriction;
        [SerializeField] private Supply[] supplies = { new() };
        public Kind Variant => kind;
        public WorldEvent EventRestriction => eventRestriction;
        public IReadOnlyList<Supply> Supplies => supplies;
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.cache." + kind.ToString().ToLowerInvariant();
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("take", EntitySignalValueType.String);
            builder.AddOutput("supplied", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("supply_count", EntitySignalValueType.Int, true, false);
            builder.AddProperty("is_available", EntitySignalValueType.Bool, true, false);
        }
    }
}
