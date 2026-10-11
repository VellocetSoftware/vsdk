// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("func_button")]
    [EntityAuthoring("World", EntityPlacementKind.Point, "Button")]
    public sealed class ButtonMarker : EntityMarker<ButtonMarker.Input, ButtonMarker.Output>
    {
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