// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;

namespace Vellocet.Sdk.Entities.Base
{
    public static class LayerMaskDefaults
    {
        public static bool TryGetPawnMask(out LayerMask mask)
        {
            return TryGetMask(SdkLayerSet.Pawn, out mask);
        }

        public static bool TryGetMask(SdkLayerSet layers, out LayerMask mask)
        {
            mask = SdkGame.Current.ResolveLayers(layers);
            return mask.value != 0;
        }

        public static LayerMask ResolvePawnOrCurrent(LayerMask current)
        {
            if (current.value != 0)
                return current;

            return TryGetPawnMask(out var pawnMask) ? pawnMask : current;
        }

    }
}
