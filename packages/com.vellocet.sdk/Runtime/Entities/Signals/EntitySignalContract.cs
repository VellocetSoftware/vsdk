// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities.Signals
{
    [Flags]
    public enum EntitySignalCapabilities
    {
        None = 0,
        Lifecycle = 1 << 0,
        Operational = 1 << 1
    }

    public enum EntitySignalAuthority
    {
        Server,
        Client,
        Any
    }

    public enum EntitySignalValueType
    {
        None,
        String,
        Int,
        Float,
        Bool,
        EntityRef,
        Enum,
        State
    }

    public enum EntitySignalMemberKind
    {
        Input,
        Output,
        Property,
        State
    }

    public sealed class EntitySignalMember
    {
        public EntitySignalMember(string name, EntitySignalMemberKind kind,
            EntitySignalValueType valueType = EntitySignalValueType.None,
            EntitySignalAuthority authority = EntitySignalAuthority.Server,
            bool readable = false,
            bool writable = false,
            string typeName = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            NormalizedName = EntitySignalNameUtility.Normalize(Name);
            Kind = kind;
            ValueType = valueType;
            Authority = authority;
            Readable = readable;
            Writable = writable;
            TypeName = string.IsNullOrWhiteSpace(typeName) ? null : typeName.Trim();
        }

        public string Name { get; }
        public string NormalizedName { get; }
        public EntitySignalMemberKind Kind { get; }
        public EntitySignalValueType ValueType { get; }
        public EntitySignalAuthority Authority { get; }
        public bool Readable { get; }
        public bool Writable { get; }
        public string TypeName { get; }
    }

    public sealed class EntitySignalContract
    {
        private static readonly EntitySignalContract Empty = new(null, null, null, null, null);

        public EntitySignalContract(string entityClassName,
            IReadOnlyList<EntitySignalMember> inputs,
            IReadOnlyList<EntitySignalMember> outputs,
            IReadOnlyList<EntitySignalMember> properties,
            IReadOnlyList<EntitySignalMember> states,
            EntitySignalCapabilities capabilities = EntitySignalCapabilities.None)
        {
            EntityClassName = entityClassName;
            Inputs = inputs ?? Array.Empty<EntitySignalMember>();
            Outputs = outputs ?? Array.Empty<EntitySignalMember>();
            Properties = properties ?? Array.Empty<EntitySignalMember>();
            States = states ?? Array.Empty<EntitySignalMember>();
            Capabilities = capabilities;
        }

        public string EntityClassName { get; }
        public IReadOnlyList<EntitySignalMember> Inputs { get; }
        public IReadOnlyList<EntitySignalMember> Outputs { get; }
        public IReadOnlyList<EntitySignalMember> Properties { get; }
        public IReadOnlyList<EntitySignalMember> States { get; }
        public EntitySignalCapabilities Capabilities { get; }

        public bool HasCapability(EntitySignalCapabilities capability)
        {
            return (Capabilities & capability) == capability;
        }

        public bool TryFindInput(string name, out EntitySignalMember member)
        {
            return TryFind(Inputs, name, out member);
        }

        public bool TryFindOutput(string name, out EntitySignalMember member)
        {
            return TryFind(Outputs, name, out member);
        }

        public bool TryFindState(string name, out EntitySignalMember member)
        {
            return TryFind(States, name, out member);
        }

        public bool TryFindProperty(string name, out EntitySignalMember member)
        {
            return TryFind(Properties, name, out member);
        }

        private static bool TryFind(IReadOnlyList<EntitySignalMember> members, string name,
            out EntitySignalMember member)
        {
            var normalized = EntitySignalNameUtility.Normalize(name);
            foreach (var candidate in members)
                if (candidate != null &&
                    string.Equals(candidate.NormalizedName, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    member = candidate;
                    return true;
                }

            member = null;
            return false;
        }

        public static EntitySignalContract FromMarker(EntityMarker marker)
        {
            return marker ? marker.SignalContract : Empty;
        }
    }
}