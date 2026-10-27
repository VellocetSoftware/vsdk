using Vellocet.Stereoform;
using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_wartime_factory")]
    [EntityAuthoring("Industry", EntityPlacementKind.Point, "Wartime Factory")]
    public sealed class WartimeFactoryMarker : PrefabEntityMarker
    {
        [SerializeField] private WarheadMarker[] stations = Array.Empty<WarheadMarker>();
        [SerializeField] private CacheMarker[] caches = Array.Empty<CacheMarker>();
        [SerializeField] private bool useBuiltInParts = true;
        public WarheadMarker[] Stations => stations;
        public CacheMarker[] Caches => caches;
        public bool UseBuiltInParts => useBuiltInParts;
        [Tooltip("Leave empty to use the game's localized message. {0} is the completed count and {1} is minutes remaining.")]
        [SerializeField] private string openText = string.Empty;
        [Tooltip("Leave empty to use the game's localized message.")]
        [SerializeField] private string closedText = string.Empty;
        [SerializeField] private AudioEventReference completionSound;
        [SerializeField] private AudioEventReference eventSound;
        public string OpenText => openText;
        public string ClosedText => closedText;
        public AudioEventReference CompletionSound => completionSound;
        public AudioEventReference EventSound => eventSound;
        public override string ArchetypeId => "aniv.wartime-factory";
        public override string[] Inputs => new[] { "Open", "Close" };
        public override string[] Outputs => new[] { "OnOpen", "OnClose", "OnComplete" };

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("Open", EntitySignalValueType.None);
            builder.AddInput("Close", EntitySignalValueType.None);
            builder.AddOutput("OnOpen", EntitySignalValueType.None);
            builder.AddOutput("OnClose", EntitySignalValueType.None);
            builder.AddOutput("OnComplete", EntitySignalValueType.None);
            builder.AddProperty("is_open", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("completed", EntitySignalValueType.Int, true, false);
        }
    }
}
