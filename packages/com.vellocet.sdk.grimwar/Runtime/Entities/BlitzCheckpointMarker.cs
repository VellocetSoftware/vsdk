// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vellocet.Sdk.Entities
{
    [EntityClass("game_blitz_checkpoint")]
    [EntityAuthoring("Objectives", EntityPlacementKind.Point, "Blitz Checkpoint")]
    public sealed class BlitzCheckpointMarker : EntityMarker<BlitzCheckpointMarker.Input, BlitzCheckpointMarker.Output>, IEntityImportOwner
    {
        public EntityMarker ImportOwner => Tank;

        public enum Input
        {
            Reset,
            Reach
        }

        public enum Output
        {
            OnReached,
            OnReset
        }

        [field: Header("Display")]
        [field: Tooltip("Display name shown to players and HUD when this checkpoint is reached.")]
        [field: SerializeField]
        public string CheckpointName { get; private set; } = "Checkpoint";

        [field: Header("Routing")]
        [field: Tooltip("The tank this checkpoint belongs to.")]
        [field: SerializeField]
        public BlitzTankMarker Tank { get; private set; }

        [field: Header("Round")]
        [field: Tooltip("Bonus seconds added to the live round timer when this checkpoint is reached.")]
        [field: SerializeField]
        [field: Min(0f)]
        public float TimeBonusSeconds { get; private set; }

#if UNITY_EDITOR
        [NonSerialized] private bool _hasLastEditorPosition;
        [NonSerialized] private Vector3 _lastEditorPosition;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (Application.isPlaying)
                return;

            if (Tank)
                Tank.RequestAuthoringSyncFromCheckpoint();
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
                return;

            var position = transform.position;
            if (!_hasLastEditorPosition)
            {
                _lastEditorPosition = position;
                _hasLastEditorPosition = true;
                return;
            }

            if ((position - _lastEditorPosition).sqrMagnitude <= 0.000001f)
                return;

            _lastEditorPosition = position;
            if (Tank)
                Tank.RequestAuthoringSyncFromCheckpoint();
        }

        internal bool EnsureTankReferenceInEditor(BlitzTankMarker tank)
        {
            if (Tank == tank)
                return false;

            Tank = tank;
            EditorUtility.SetDirty(this);
            if (!PrefabUtility.IsPartOfPrefabAsset(this))
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            return true;
        }
#endif
    }
}