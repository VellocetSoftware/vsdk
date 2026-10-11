// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities
{
    [EntityClass("game_capture_flag")]
    [EntityAuthoring("Objectives", EntityPlacementKind.Volume, "Capture Flag")]
    public sealed class CaptureFlagMarker : CapturePointMarker
    {
        [field: Header("Uplink graph")]
        [field: Tooltip("Exactly one flag is the middle point, initially open to both teams.")]
        [field: SerializeField]
        public bool IsMiddle { get; private set; }

        [field: Tooltip("On the middle flag: how many opposing terminal flags a team must hold simultaneously to win.")]
        [field: SerializeField, Min(1)]
        public int RequiredTerminals { get; private set; } = 1;

        protected override Vector3 InitialAuthoringSize => new(3f, 2.5f, 3f);

        protected override void Reset()
        {
            base.Reset();
            CaptureSeconds = 20f;
            DecaySeconds = 10f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.85f);
            foreach (var prerequisite in GetPrerequisiteMarkers())
                if (prerequisite)
                {
                    Gizmos.DrawLine(prerequisite.transform.position, transform.position);
                    var arrow = Vector3.Lerp(prerequisite.transform.position, transform.position, 0.7f);
                    Gizmos.DrawSphere(arrow, 0.2f);
                }
        }
#endif
    }
}
