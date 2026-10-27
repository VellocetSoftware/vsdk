using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_rot_node")]
    [EntityAuthoring("Environment / Rot", EntityPlacementKind.Point, "Rot Node", Description = "Registers automatically with the map rot service. Choose Rot or Zakazen and whether nearby players can be infected.")]
    public sealed class RotNodeMarker : PrefabEntityMarker
    {
        public enum Kind { Rot, Zakazen }
        [SerializeField] private Kind kind;
        [SerializeField] private bool startsDisabled;
        [SerializeField] private bool infectionEnabled = true;
        public Kind Variant => kind;
        public bool StartsDisabled => startsDisabled;
        public bool InfectionEnabled => infectionEnabled;
        public override string ArchetypeId => kind == Kind.Rot ? "aniv.rot-node" : "aniv.zakazen-node";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddInput("enable_infection", EntitySignalValueType.None);
            builder.AddInput("disable_infection", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddOutput("infection_applied", EntitySignalValueType.Float);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("infection_enabled", EntitySignalValueType.Bool, true, false);
            builder.AddOutput("spawned", EntitySignalValueType.None);
            builder.AddOutput("died", EntitySignalValueType.None);
            builder.AddOutput("drop_rejected", EntitySignalValueType.None);
            builder.AddProperty("is_spawned", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("health", EntitySignalValueType.Float, true, false);
            builder.AddProperty("growth", EntitySignalValueType.Float, true, false);
        }
    }
}
