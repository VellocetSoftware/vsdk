using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_warhead")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Warhead Assembly Station")]
    public sealed class WarheadMarker : ContactOperationMarker
    {
        [SerializeField] private string[] requiredParts = {
            GameItem.WarheadCasing, GameItem.WarheadDetonator, GameItem.WarheadReceiver, GameItem.WarheadCore
        };
        [SerializeField] private bool startEnabled = true;
        [Range(.001f, 1)] [SerializeField] private float progressPerHit = .05f;
        [Range(0, 3600)] [SerializeField] private float resetDelay = 5f;
        [Range(0, 1)] [SerializeField] private float failureChance = .05f;
        [Range(0, 1)] [SerializeField] private float rotReduction = .01f;
        public IReadOnlyList<string> RequiredParts => requiredParts;
        public bool StartEnabled => startEnabled;
        public float ProgressPerHit => progressPerHit;
        public float ResetDelay => resetDelay;
        public float FailureChance => failureChance;
        public float RotReduction => rotReduction;
        public override string ArchetypeId => "aniv.warhead";
        public override string[] Inputs => new[] { "Enable", "Disable", "Toggle" };
        public override string[] Outputs => new[] { "OnEnabled", "OnDisabled", "OnComplete" };

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("part_accepted", EntitySignalValueType.Int);
            builder.AddOutput("failed", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("is_building", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("progress", EntitySignalValueType.Float, true, false);
            builder.AddProperty("part_index", EntitySignalValueType.Int, true, false);
            builder.AddProperty("required_item", EntitySignalValueType.String, true, false);
            builder.AddInput("Enable", EntitySignalValueType.None);
            builder.AddInput("Disable", EntitySignalValueType.None);
            builder.AddInput("Toggle", EntitySignalValueType.None);
            builder.AddOutput("OnEnabled", EntitySignalValueType.None);
            builder.AddOutput("OnDisabled", EntitySignalValueType.None);
            builder.AddOutput("OnComplete", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }
    }
}
