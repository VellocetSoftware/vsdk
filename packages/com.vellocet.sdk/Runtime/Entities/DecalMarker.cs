using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("env_decal")]
    [EntityAuthoring("Environment", EntityPlacementKind.Volume, "Decal")]
    public sealed class DecalMarker : EntityMarker, IBoundedEntityMarker
    {
        [SerializeField, Tooltip("Native decal material, for example graffiti, signage or a game board.")]
        private Material material;
        [SerializeField, Range(0, 1)] private float opacity = 1;
        [SerializeField, Range(1, 1000)] private float drawDistance = 100;
        public Material Material => material;
        public float Opacity => opacity;
        public float DrawDistance => drawDistance;
        public override string[] Inputs => Array.Empty<string>();
        public override string[] Outputs => Array.Empty<string>();
        public override EntitySignalCapabilities SignalCapabilities => EntitySignalCapabilities.None;
    }
}
