// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Vellocet.Sdk.Entities;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;
using Vellocet.Sdk.Entities.States;
using Object = UnityEngine.Object;

namespace Vellocet.Sdk.Tests
{
    public sealed class EntityMarkerContractTests
    {
        [Test]
        public void EveryAuthorableMarker_UsesCanonicalIdAndMatchingIcon()
        {
            var markers = TypeCache.GetTypesDerivedFrom<EntityMarker>()
                .Where(type => !type.IsAbstract)
                .Select(type => new
                {
                    type,
                    id = EntityMarker.GetEntityClassName(type),
                    path = EntityMarker.GetIconPath(type)
                })
                .ToArray();
            var invalidIds = markers
                .Where(item => !EntityIdConvention.IsCanonical(item.id))
                .Select(item => $"{item.type.Name}: {item.id ?? "<missing>"}")
                .OrderBy(value => value)
                .ToArray();
            var duplicateIds = markers
                .GroupBy(item => item.id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(item => item.type.Name))}")
                .OrderBy(value => value)
                .ToArray();
            var mismatchedPaths = markers
                .Where(item => !string.Equals(item.path, EntityIdConvention.GetIconPath(item.id),
                    StringComparison.Ordinal))
                .Select(item => $"{item.type.Name}: {item.path ?? "<none>"}")
                .OrderBy(value => value)
                .ToArray();
            var missingIcons = markers
                .Where(item => string.IsNullOrWhiteSpace(item.path) ||
                               AssetDatabase.LoadAssetAtPath<Texture2D>(item.path) == null)
                .Select(item => $"{item.type.Name}: {item.path ?? "<none>"}")
                .OrderBy(value => value)
                .ToArray();

            Assert.That(invalidIds, Is.Empty,
                $"Entity IDs must use {EntityIdConvention.Description}. Invalid: {string.Join(", ", invalidIds)}");
            Assert.That(duplicateIds, Is.Empty,
                $"Entity IDs must be unique. Duplicates: {string.Join(", ", duplicateIds)}");
            Assert.That(mismatchedPaths, Is.Empty,
                $"Entity icons must be named after their entity IDs. Mismatches: {string.Join(", ", mismatchedPaths)}");
            Assert.That(missingIcons, Is.Empty,
                $"Every authorable entity needs an icon asset. Missing: {string.Join(", ", missingIcons)}");
        }

        [TestCase("func_door")]
        [TestCase("trigger_multiple")]
        [TestCase("info_player_spawn")]
        [TestCase("env_ambient")]
        [TestCase("logic_script")]
        [TestCase("item_pickup")]
        [TestCase("game_capture_point")]
        public void EntityIdConvention_AcceptsCanonicalIds(string entityId)
        {
            Assert.That(EntityIdConvention.IsCanonical(entityId), Is.True);
        }

        [TestCase("spawn")]
        [TestCase("Info_player_spawn")]
        [TestCase("info__spawn")]
        [TestCase("info_spawn_")]
        [TestCase("info_1spawn")]
        [TestCase("ambient_runtime")]
        public void EntityIdConvention_RejectsNonCanonicalIds(string entityId)
        {
            Assert.That(EntityIdConvention.IsCanonical(entityId), Is.False);
        }

        [Test]
        public void NewTrigger_StoresItsDefaultBoundsInTransformScale()
        {
            var markerObject = new GameObject("Trigger");

            try
            {
                var marker = markerObject.AddComponent<TriggerMarker>();
                var serialized = new SerializedObject(marker);

                Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(7f, 2.5f, 7f)));
                Assert.That(marker.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Trigger")));
                Assert.That(marker.GetBoundsXYZ(), Is.EqualTo(marker.transform.localScale));
                Assert.That(serialized.FindProperty("<Bounds>k__BackingField"), Is.Null);
                Assert.That(serialized.FindProperty("<BoundsXZ>k__BackingField"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void TriggerBounds_AreAbsoluteWorldTransformScaleExactlyOnce()
        {
            var parent = new GameObject("Scaled Parent");
            var markerObject = new GameObject("Trigger");
            markerObject.transform.SetParent(parent.transform, false);

            try
            {
                var marker = markerObject.AddComponent<TriggerMarker>();
                parent.transform.localScale = new Vector3(2f, 3f, 4f);
                markerObject.transform.localScale = new Vector3(-0.5f, 2f, 0.25f);

                Assert.That(marker.GetBoundsXYZ(), Is.EqualTo(new Vector3(1f, 6f, 1f)));
                Assert.That(marker.TryGetToolVolumeVisual(out var visual), Is.True);
                Assert.That(visual.Size, Is.EqualTo(new Vector3(1f, 6f, 1f)));
                Assert.That(visual.LocalToWorldMatrix.lossyScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TriggerExecutionContract_EmitsAnOpaqueParticipantSnapshotToken()
        {
            var markerObject = new GameObject("Extraction Zone");

            try
            {
                var marker = markerObject.AddComponent<TriggerMarker>();

                Assert.That(marker.SignalContract.TryFindInput("execute", out var execute), Is.True);
                Assert.That(execute.ValueType, Is.EqualTo(EntitySignalValueType.None));
                Assert.That(marker.SignalContract.TryFindOutput("executed", out var executed), Is.True);
                Assert.That(executed.ValueType, Is.EqualTo(EntitySignalValueType.String));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void TriggerPushAngles_AreLocalToTheMarkerTransform()
        {
            var markerObject = new GameObject("Trigger Push");

            try
            {
                var marker = markerObject.AddComponent<TriggerPushMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("<PushAngles>k__BackingField").vector2Value = new Vector2(-30f, 45f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                markerObject.transform.rotation = Quaternion.Euler(0f, 20f, 0f);

                var expectedLocal = Quaternion.Euler(-30f, 45f, 0f) * Vector3.forward;
                var expectedWorld = markerObject.transform.TransformDirection(expectedLocal).normalized;

                Assert.That(Vector3.Angle(marker.LocalPushDirection, expectedLocal), Is.LessThan(0.001f));
                Assert.That(Vector3.Angle(marker.WorldDirection, expectedWorld), Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void TriggerWaterMarker_ExposesAnOptionalSurfaceMaterial()
        {
            var markerObject = new GameObject("Water");

            try
            {
                var marker = markerObject.AddComponent<TriggerWaterMarker>();
                var serialized = new SerializedObject(marker);
                var materialProperty = serialized.FindProperty("<SurfaceMaterial>k__BackingField");

                Assert.That(EntityMarker.GetEntityClassName(typeof(TriggerWaterMarker)), Is.EqualTo("trigger_water"));
                Assert.That(materialProperty, Is.Not.Null);
                Assert.That(materialProperty.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
                Assert.That(marker.SurfaceMaterial, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void TriggerWaterMarker_DefaultsToPawnRagdollAndItemLayers()
        {
            var markerObject = new GameObject("Water");

            try
            {
                var marker = markerObject.AddComponent<TriggerWaterMarker>();
                var expected = LayerMask.GetMask("Pawn", "Ragdoll", "Item");

                Assert.That(expected, Is.Not.Zero,
                    "The SDK project must define the canonical water interaction layers.");
                Assert.That(marker.Layers.value, Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void LadderMarker_UsesThinTwoSidedVolumeDefaults()
        {
            var markerObject = new GameObject("Ladder");

            try
            {
                var marker = markerObject.AddComponent<LadderMarker>();

                Assert.That(EntityMarker.GetEntityClassName(typeof(LadderMarker)), Is.EqualTo("func_ladder"));
                Assert.That(marker.transform.localScale, Is.EqualTo(new Vector3(1.2f, 3f, 0.25f)));
                Assert.That(marker.GetBoundsXYZ(), Is.EqualTo(marker.transform.localScale));
                Assert.That(marker.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Trigger")));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void DirectionalPointMarkers_ExposeTheirTransformForwardDirection()
        {
            var spawnObject = new GameObject("Spawn");
            var destinationObject = new GameObject("Teleport Destination");

            try
            {
                var spawn = spawnObject.AddComponent<SpawnMarker>();
                var destination = destinationObject.AddComponent<TeleportDestinationMarker>();
                spawnObject.transform.rotation = Quaternion.Euler(-15f, 70f, 0f);
                destinationObject.transform.rotation = Quaternion.Euler(20f, -125f, 0f);

                Assert.That(Vector3.Angle(spawn.WorldDirection, spawnObject.transform.forward),
                    Is.LessThan(0.001f));
                Assert.That(Vector3.Angle(destination.WorldDirection, destinationObject.transform.forward),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(spawnObject);
                Object.DestroyImmediate(destinationObject);
            }
        }

        [Test]
        public void DoorMarker_ExposesSignedLipAndImpactDamageSettings()
        {
            var markerObject = new GameObject("Door");

            try
            {
                var marker = markerObject.AddComponent<DoorMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("<Lip>k__BackingField").floatValue = -3f;
                serialized.FindProperty("<Damage>k__BackingField").floatValue = 75f;
                serialized.FindProperty("<DamageCooldownSeconds>k__BackingField").floatValue = 0.4f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(marker.Lip, Is.EqualTo(-3f));
                Assert.That(marker.Damage, Is.EqualTo(75f));
                Assert.That(marker.DamageCooldownSeconds, Is.EqualTo(0.4f));

                var lipField = typeof(DoorMarker).GetField(
                    "<Lip>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lipField, Is.Not.Null);
                Assert.That(lipField.GetCustomAttribute<MinAttribute>(), Is.Null,
                    "Signed lip must not be clamped by the SDK inspector.");
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void TrackTrainMarker_UsesAnExternalSplineAndTypedRuntimeControls()
        {
            var markerObject = new GameObject("Track Train");
            var splineObject = new GameObject("Track");

            try
            {
                var marker = markerObject.AddComponent<TrackTrainMarker>();
                var spline = splineObject.AddComponent<SplineContainer>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("<SplineContainer>k__BackingField").objectReferenceValue = spline;
                serialized.FindProperty("<StartProgress01>k__BackingField").floatValue = 0.35f;
                serialized.FindProperty("<Damage>k__BackingField").floatValue = 125f;
                serialized.FindProperty("<EndBehavior>k__BackingField").enumValueIndex =
                    (int)TrackTrainMarker.PathEndBehavior.PingPong;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(marker.HasExternalSpline, Is.True);
                Assert.That(marker.StartProgress01, Is.EqualTo(0.35f));
                Assert.That(marker.Damage, Is.EqualTo(125f));
                Assert.That(marker.EndBehavior, Is.EqualTo(TrackTrainMarker.PathEndBehavior.PingPong));
                Assert.That(marker.SignalContract.TryFindInput("set_speed", out var setSpeed), Is.True);
                Assert.That(setSpeed.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(marker.SignalContract.TryFindInput("set_progress", out var setProgress), Is.True);
                Assert.That(setProgress.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(marker.SignalContract.TryFindProperty("progress", out var progress), Is.True);
                Assert.That(progress.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(marker.SignalContract.TryFindState("state", out var state), Is.True);
                Assert.That(state.TypeName, Is.EqualTo(nameof(TrackTrainEntityState)));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
                Object.DestroyImmediate(splineObject);
            }
        }

        [Test]
        public void AmbientVolumetricMode_PreservesSerializedValueAndUsesAbsoluteWorldScale()
        {
            var markerObject = new GameObject("Ambient")
            {
                transform =
                {
                    localScale = new Vector3(-2f, 3f, 0.5f)
                }
            };

            try
            {
                var marker = markerObject.AddComponent<AmbientMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("<Mode>k__BackingField").intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That((int)AmbientMarker.PlacementMode.Point, Is.Zero);
                Assert.That((int)AmbientMarker.PlacementMode.Volumetric, Is.EqualTo(1));
                Assert.That(marker.Mode, Is.EqualTo(AmbientMarker.PlacementMode.Volumetric));
                Assert.That(marker.IsVolumetric, Is.True);
                Assert.That(marker.GetVolumetricSize(), Is.EqualTo(new Vector3(2f, 3f, 0.5f)));
                Assert.That(new SerializedObject(marker).FindProperty("<ZoneBounds>k__BackingField"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void AmbientVolumetricEmitter_ResolvesClosestOrientedBoxBoundaryAndContainment()
        {
            var markerObject = new GameObject("Ambient")
            {
                transform =
                {
                    position = new Vector3(10f, 2f, -5f),
                    rotation = Quaternion.Euler(0f, 90f, 0f),
                    localScale = new Vector3(4f, 6f, 8f)
                }
            };

            try
            {
                var marker = markerObject.AddComponent<AmbientMarker>();
                var outsideListener = marker.transform.position +
                                      marker.transform.rotation * new Vector3(7f, 3f, 1f);
                var expectedBoundary = marker.transform.position +
                                       marker.transform.rotation * new Vector3(2f, 3f, 1f);
                var outsideEmitter = marker.ResolveVolumetricBoundaryPosition(outsideListener);

                Assert.That(marker.ContainsVolumetricPoint(outsideListener), Is.False);
                Assert.That(Vector3.Distance(outsideEmitter, expectedBoundary), Is.LessThan(0.0001f));

                var nearBoundaryListener = marker.transform.position +
                                           marker.transform.rotation * new Vector3(2.03f, 3f, 1f);
                Assert.That(marker.ContainsVolumetricPoint(nearBoundaryListener), Is.False);
                Assert.That(marker.ContainsVolumetricPoint(nearBoundaryListener, 0.05f), Is.True);

                var insideListener = marker.transform.position +
                                     marker.transform.rotation * new Vector3(-1f, 4f, 3f);
                var expectedInsideBoundary = marker.transform.position +
                                             marker.transform.rotation * new Vector3(-2f, 4f, 3f);
                var insideEmitter = marker.ResolveVolumetricBoundaryPosition(insideListener);

                Assert.That(marker.ContainsVolumetricPoint(insideListener), Is.True);
                Assert.That(Vector3.Distance(insideEmitter, expectedInsideBoundary), Is.LessThan(0.0001f));

                var aboveListener = marker.transform.position +
                                    marker.transform.rotation * new Vector3(0f, 9f, 0f);
                var expectedTop = marker.transform.position +
                                  marker.transform.rotation * new Vector3(0f, 6f, 0f);
                var aboveEmitter = marker.ResolveVolumetricBoundaryPosition(aboveListener);

                Assert.That(marker.ContainsVolumetricPoint(aboveListener), Is.False);
                Assert.That(Vector3.Distance(aboveEmitter, expectedTop), Is.LessThan(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void AmbientMarker_ExposesCatalogEventAndAudibleDistanceContracts()
        {
            var markerObject = new GameObject("Ambient");

            try
            {
                var marker = markerObject.AddComponent<AmbientMarker>();
                var serialized = new SerializedObject(marker);
                var eventName = serialized.FindProperty("<EventName>k__BackingField");
                var audibleDistance = serialized.FindProperty("<AudibleDistance>k__BackingField");
                var eventNameField = typeof(AmbientMarker).GetField("<EventName>k__BackingField",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(eventName, Is.Not.Null);
                Assert.That(eventNameField, Is.Not.Null);
                Assert.That(eventNameField.GetCustomAttribute<AmbientEventNameAttribute>(), Is.Not.Null);
                Assert.That(AmbientMarker.EventNamePrefix, Is.EqualTo("Play_amb"));
                Assert.That(audibleDistance, Is.Not.Null);
                Assert.That(AmbientMarker.DefaultAudibleDistance, Is.EqualTo(10f));
                Assert.That(audibleDistance.floatValue, Is.EqualTo(AmbientMarker.DefaultAudibleDistance));

                audibleDistance.floatValue = 24.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(marker.AudibleDistance, Is.EqualTo(24.5f));
                Assert.That(marker.SignalContract.TryFindProperty("audible_distance", out var distanceProperty),
                    Is.True);
                Assert.That(distanceProperty.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(distanceProperty.Readable, Is.True);
                Assert.That(distanceProperty.Writable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void RotateMarker_AxisFlagsAllowCombinations()
        {
            var markerObject = new GameObject("Rotate");

            try
            {
                var marker = markerObject.AddComponent<RotateMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("<RotationAxes>k__BackingField").intValue =
                    (int)(RotateMarker.AxisMask.X | RotateMarker.AxisMask.Z);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(marker.LocalAxes, Is.EqualTo(new Vector3(1f, 0f, 1f)));
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void RotateMarker_ContinuousModeSerializesExplicitStartupBehavior()
        {
            var markerObject = new GameObject("Rotate");

            try
            {
                var marker = markerObject.AddComponent<RotateMarker>();
                var serialized = new SerializedObject(marker);
                serialized.FindProperty("<LoopContinuously>k__BackingField").boolValue = true;
                serialized.FindProperty("<StartPhase>k__BackingField").floatValue = 35f;
                serialized.FindProperty("<StartActive>k__BackingField").boolValue = false;
                serialized.FindProperty("<ReverseDirection>k__BackingField").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(marker.LoopContinuously, Is.True);
                Assert.That(marker.StartPhase, Is.EqualTo(35f));
                Assert.That(marker.StartActive, Is.False);
                Assert.That(marker.ReverseDirection, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        [Test]
        public void DynamicEntityInputs_ExposeTheirConcreteValueTypes()
        {
            var barrierObject = new GameObject("Barrier");
            var rotateObject = new GameObject("Rotate");

            try
            {
                var barrier = barrierObject.AddComponent<TeamBarrierMarker>();
                var rotate = rotateObject.AddComponent<RotateMarker>();

                Assert.That(barrier.SignalContract.TryFindInput("set_allowed_teams", out var teams), Is.True);
                Assert.That(teams.ValueType, Is.EqualTo(EntitySignalValueType.Enum));
                Assert.That(barrier.SignalContract.TryFindInput("set_visual_enabled", out var visual), Is.True);
                Assert.That(visual.ValueType, Is.EqualTo(EntitySignalValueType.Bool));
                Assert.That(rotate.SignalContract.TryFindInput("set_angle", out var angle), Is.True);
                Assert.That(angle.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(rotate.SignalContract.TryFindInput("set_phase", out var phase), Is.True);
                Assert.That(phase.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(rotate.SignalContract.TryFindProperty("is_active", out var active), Is.True);
                Assert.That(active.ValueType, Is.EqualTo(EntitySignalValueType.Bool));
                Assert.That(rotate.SignalContract.TryFindProperty("phase", out var phaseProperty), Is.True);
                Assert.That(phaseProperty.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(rotate.SignalContract.TryFindState("state", out var state), Is.True);
                Assert.That(state.TypeName, Is.EqualTo(nameof(RotateEntityState)));
            }
            finally
            {
                Object.DestroyImmediate(barrierObject);
                Object.DestroyImmediate(rotateObject);
            }
        }

        [Test]
        public void CommonCapabilities_AddLifecycleAndOptInOperationalContracts()
        {
            var lightObject = new GameObject("Light");
            var damageableObject = new GameObject("Damageable");

            try
            {
                var light = lightObject.AddComponent<LightMarker>();
                var damageable = damageableObject.AddComponent<DamageableMarker>();

                Assert.That(light.SignalContract.HasCapability(EntitySignalCapabilities.Lifecycle), Is.True);
                Assert.That(light.SignalContract.TryFindInput("kill", out var kill), Is.True);
                Assert.That(kill.ValueType, Is.EqualTo(EntitySignalValueType.None));
                Assert.That(light.SignalContract.TryFindOutput("killed", out _), Is.True);
                Assert.That(light.SignalContract.HasCapability(EntitySignalCapabilities.Operational), Is.False);
                Assert.That(light.SignalContract.TryFindInput("disable", out _), Is.False);

                Assert.That(damageable.SignalContract.HasCapability(EntitySignalCapabilities.Operational), Is.True);
                Assert.That(damageable.SignalContract.TryFindInput("enable", out _), Is.True);
                Assert.That(damageable.SignalContract.TryFindInput("disable", out _), Is.True);
                Assert.That(damageable.SignalContract.TryFindInput("toggle", out _), Is.True);
                Assert.That(damageable.SignalContract.TryFindOutput("enabled", out _), Is.True);
                Assert.That(damageable.SignalContract.TryFindOutput("disabled", out _), Is.True);
                Assert.That(damageable.SignalContract.TryFindOutput("health_changed", out var healthChanged),
                    Is.True);
                Assert.That(healthChanged.ValueType, Is.EqualTo(EntitySignalValueType.Float));
                Assert.That(damageable.SignalContract.TryFindProperty("is_enabled", out var property), Is.True);
                Assert.That(property.ValueType, Is.EqualTo(EntitySignalValueType.Bool));
                Assert.That(damageable.SignalContract.TryFindState("state", out var damageableState), Is.True);
                Assert.That(damageableState.TypeName, Is.EqualTo(nameof(DamageableEntityState)));
                Assert.That(damageable.BreakPresentation,
                    Is.EqualTo(DamageableMarker.BreakPresentationMode.HideAndDisableCollision));
                Assert.That(damageable.ResolveBreakRoot(), Is.SameAs(damageable.transform));

                var navMeshDefault = typeof(DamageableMarker).GetProperty(
                    "DefaultIgnoreFromNavMeshBuild",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(navMeshDefault, Is.Not.Null);
                Assert.That(navMeshDefault.GetValue(damageable), Is.True);

                var state = new DamageableEntityState(25f, false);
                Assert.That(state.StateType, Is.EqualTo(nameof(DamageableEntityState)));
                Assert.That(state.Health, Is.EqualTo(25f));
                Assert.That(state.IsEnabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(damageableObject);
            }
        }

        [Test]
        public void SignalContext_ForInvocationPreservesActivatorAndAdvancesCallerAndSelf()
        {
            var activator = EntitySignalHandle.ForActor("client:12", "Modder");
            var source = new EntitySignalHandle(EntitySignalHandleKind.Entity, Guid.NewGuid(), "Map", "Map.unity",
                "source");
            var target = new EntitySignalHandle(EntitySignalHandleKind.Entity, Guid.NewGuid(), "Map", "Map.unity",
                "target");
            var context = EntitySignalContext.FromActivator(activator, source);

            var invocation = context.ForInvocation(target);

            Assert.That(invocation.Activator, Is.EqualTo(activator));
            Assert.That(invocation.Caller, Is.EqualTo(source));
            Assert.That(invocation.Self, Is.EqualTo(target));
        }
    }
}
