// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_button")]
    [EntityAuthoring("World", EntityPlacementKind.Point, "Button")]
    public class ButtonMarker : EntityMarker<ButtonMarker.Input, ButtonMarker.Output>, IMapModelMarker
    {
        [SerializeField, Tooltip("Optional game-supplied model preset. Empty uses authored brush geometry.")]
        private string model;
        [SerializeField, Tooltip("Optional player-facing name, shared by brush and model buttons. Empty keeps the model's default name.")]
        private string overrideName = "";
        [SerializeField, Tooltip("Interaction action text, independent of the button's name.")] private string label = "Press";
        [SerializeField, Min(0)] private float cooldown = .25f;
        [SerializeField] private bool startsDisabled;
        protected virtual string DefaultModel => string.Empty;
        public string Model => string.IsNullOrEmpty(model) ? DefaultModel : model;
        public string ArchetypeId => Model;
        public string Label => label;
        public string OverrideName => overrideName;
        public float Cooldown => cooldown;
        public bool StartsDisabled => startsDisabled;
        public override EntitySignalCapabilities SignalCapabilities => base.SignalCapabilities | EntitySignalCapabilities.Operational;

        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("press", EntitySignalValueType.None);
            builder.AddOutput("pressed", EntitySignalValueType.None);
            builder.AddState("state", nameof(EntityOperationalState));
        }

        public enum Input
        {
            Press
        }

        public enum Output
        {
            OnPressed
        }
    }
}
