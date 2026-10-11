using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("logic_wartime_factory")]
    [EntityAuthoring("Gameplay", EntityPlacementKind.Logic, "Wartime Factory Controller")]
    public sealed class WartimeFactoryMarker : PrefabEntityMarker
    {
        [SerializeField] private WarheadMarker[] stations = Array.Empty<WarheadMarker>();
        [SerializeField] private LabelMarker display;
        [Tooltip("Leave empty to use the game's localized message. {0} is the completed count and {1} is minutes remaining.")]
        [SerializeField] private string openText = string.Empty;
        [Tooltip("Leave empty to use the game's localized message.")]
        [SerializeField] private string closedText = string.Empty;
        [SerializeField] private AudioClip completionSound;
        [SerializeField] private AudioClip eventSound;
        public IReadOnlyList<WarheadMarker> Stations => stations;
        public LabelMarker Display => display;
        public string OpenText => openText;
        public string ClosedText => closedText;
        public AudioClip CompletionSound => completionSound;
        public AudioClip EventSound => eventSound;
        public override string ArchetypeId => "aniv.wartime-factory";
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
