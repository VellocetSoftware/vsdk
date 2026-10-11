using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("prop_warhead")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Point, "Warhead Assembly Station")]
    public sealed class WarheadMarker : PrefabEntityMarker
    {
        [SerializeField] private GameItem[] requiredParts = {
            GameItem.WarheadCasing, GameItem.WarheadDetonator, GameItem.WarheadReceiver, GameItem.WarheadCore
        };
        [SerializeField] private bool startEnabled = true;
        [Range(.001f, 1)] [SerializeField] private float progressPerHit = .05f;
        [Range(0, 3600)] [SerializeField] private float resetDelay = 5f;
        [Range(0, 1)] [SerializeField] private float failureChance = .05f;
        [Range(0, 1)] [SerializeField] private float rotReduction = .01f;
        public IReadOnlyList<GameItem> RequiredParts => requiredParts;
        public bool StartEnabled => startEnabled;
        public float ProgressPerHit => progressPerHit;
        public float ResetDelay => resetDelay;
        public float FailureChance => failureChance;
        public float RotReduction => rotReduction;
        public override string ArchetypeId => "aniv.warhead";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
