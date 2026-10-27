using System;
using UnityEngine;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("info_rat_track")]
    [EntityAuthoring("Recreation / Racing", EntityPlacementKind.Logic, "Rat Track")]
    public sealed class RatTrackMarker : EntityMarker, ISplineEntityMarker
    {
        [field: SerializeField] public SplineContainer SplineContainer { get; private set; }
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
