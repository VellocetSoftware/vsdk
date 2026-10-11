// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities.Signals
{
    public enum EntitySignalHandleKind
    {
        None = 0,
        Entity = 1,
        Actor = 2
    }

    /// <summary>
    ///     Opaque identity carried with an entity signal. It deliberately does not expose a Unity object or a
    ///     network object, so provenance can cross VSig boundaries without granting access to arbitrary objects.
    /// </summary>
    public readonly struct EntitySignalHandle : IEquatable<EntitySignalHandle>
    {
        public EntitySignalHandle(EntitySignalHandleKind kind, Guid stableGuid, string sceneName,
            string scenePath, string displayName, string runtimeIdentity = null)
        {
            Kind = kind;
            StableGuid = stableGuid;
            SceneName = sceneName ?? string.Empty;
            ScenePath = scenePath ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            RuntimeIdentity = runtimeIdentity ?? string.Empty;
        }

        public EntitySignalHandleKind Kind { get; }
        public Guid StableGuid { get; }
        public string SceneName { get; }
        public string ScenePath { get; }
        public string DisplayName { get; }
        public string RuntimeIdentity { get; }
        public bool IsEmpty => Kind == EntitySignalHandleKind.None;
        public bool IsEntity => Kind == EntitySignalHandleKind.Entity && StableGuid != Guid.Empty;

        public static EntitySignalHandle ForEntity(EntityMarker marker)
        {
            if (!marker)
                return default;
            var scene = marker.gameObject.scene;
            return new EntitySignalHandle(EntitySignalHandleKind.Entity, marker.Guid,
                scene.IsValid() ? scene.name : string.Empty,
                scene.IsValid() ? scene.path : string.Empty,
                marker.name);
        }

        public static EntitySignalHandle ForActor(string runtimeIdentity, string displayName = null)
        {
            return string.IsNullOrWhiteSpace(runtimeIdentity)
                ? default
                : new EntitySignalHandle(EntitySignalHandleKind.Actor, Guid.Empty, string.Empty, string.Empty,
                    displayName, runtimeIdentity.Trim());
        }

        public bool Equals(EntitySignalHandle other)
        {
            return Kind == other.Kind && StableGuid.Equals(other.StableGuid) &&
                   string.Equals(SceneName, other.SceneName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(ScenePath, other.ScenePath, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(RuntimeIdentity, other.RuntimeIdentity, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EntitySignalHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ StableGuid.GetHashCode();
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(SceneName ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(ScenePath ?? string.Empty);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(RuntimeIdentity ?? string.Empty);
                return hash;
            }
        }

        public override string ToString()
        {
            if (IsEntity)
                return string.IsNullOrWhiteSpace(DisplayName) ? StableGuid.ToString("N") : DisplayName;
            return IsEmpty ? "<none>" : string.IsNullOrWhiteSpace(DisplayName) ? RuntimeIdentity : DisplayName;
        }
    }

    /// <summary>Immutable provenance for one signal execution chain.</summary>
    public readonly struct EntitySignalContext
    {
        public EntitySignalContext(EntitySignalHandle activator, EntitySignalHandle caller,
            EntitySignalHandle self)
        {
            Activator = activator;
            Caller = caller;
            Self = self;
        }

        public EntitySignalHandle Activator { get; }
        public EntitySignalHandle Caller { get; }
        public EntitySignalHandle Self { get; }
        public bool IsEmpty => Activator.IsEmpty && Caller.IsEmpty && Self.IsEmpty;
        public static EntitySignalContext Empty => default;

        public EntitySignalContext WithSelf(EntitySignalHandle self)
        {
            return new EntitySignalContext(Activator, Caller, self);
        }

        public EntitySignalContext ForInvocation(EntitySignalHandle target)
        {
            return new EntitySignalContext(Activator, Self, target);
        }

        public static EntitySignalContext FromActivator(EntitySignalHandle activator,
            EntitySignalHandle self = default)
        {
            return new EntitySignalContext(activator, default, self);
        }
    }

    public interface IEntityStateSnapshot : Vertex.Contracts.IEntityStateSnapshot
    {
        new string StateType { get; }
    }

    public interface IEntitySignalRuntime
    {
        EntitySignalContract SignalContract { get; }
        EntitySignalHandle SignalHandle { get; }
        bool InvokeInput(string inputName, string parameters, EntitySignalContext context);
        bool TryReadProperty(string propertyName, out object value);
        bool TryCaptureState(out IEntityStateSnapshot state);
        bool TryRestoreState(IEntityStateSnapshot state);
    }

    public interface IEntitySignalContextSource
    {
        EntitySignalHandle SignalHandle { get; }
        EntitySignalContext CurrentSignalContext { get; }
    }

    public readonly struct EntityOperationalState : IEntityStateSnapshot
    {
        public EntityOperationalState(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }
        public string StateType => "entity_operational";
    }

    public readonly struct EntityKilledState : IEntityStateSnapshot
    {
        public string StateType => "entity_killed";
    }

    public interface IEntityStateful<TState> where TState : IEntityStateSnapshot
    {
        TState CaptureState();
        bool RestoreState(TState state);
    }

    public interface IEntityStatefulSignalRuntime : IEntitySignalRuntime
    {
        Type StateSnapshotType { get; }
    }

    public interface IEntityStatefulSignalRuntime<TState> : IEntityStatefulSignalRuntime, IEntityStateful<TState>
        where TState : IEntityStateSnapshot
    {
    }
}
