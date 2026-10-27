// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("logic_script")]
    [EntityAuthoring("Logic", EntityPlacementKind.Logic, "Logic Script", Description = "Attach a module library to this hierarchy. Use its typed settings and public inputs and outputs.")]
    public sealed class LogicScriptMarker : EntityMarker<LogicScriptMarker.Input, LogicScriptMarker.Output>
    {
        public override Vellocet.Sdk.Entities.Signals.EntitySignalCapabilities SignalCapabilities =>
            Vellocet.Sdk.Entities.Signals.EntitySignalCapabilities.None;

        public enum Input
        {
        }

        public enum Output
        {
        }

        [field: Header("Module")]
        [field: Tooltip("Optional .vsiglib TextAsset. Empty uses the standard Logic modules.")]
        [field: SerializeField]
        public TextAsset Library { get; private set; }

        [field: SerializeField] public string ModuleName { get; private set; } = string.Empty;

        [field: Tooltip("Optional VSig alias. Empty uses the GameObject name; {id} expands to this entity's stable ID for reusable instances.")]
        [field: SerializeField]
        public string Alias { get; private set; } = string.Empty;

        [field: Tooltip("Scope module refs to this GameObject hierarchy. Disable for pure logic modules.")]
        [field: SerializeField]
        public bool BindToHierarchy { get; private set; }

        [field: SerializeField] public Argument[] Arguments { get; private set; } = Array.Empty<Argument>();

        [Serializable]
        public sealed class Argument
        {
            [SerializeField] private string name = string.Empty;
            [SerializeField] private string expression = string.Empty;

            public string Name => name?.Trim() ?? string.Empty;
            public string Expression => expression?.Trim() ?? string.Empty;
        }
    }
}
