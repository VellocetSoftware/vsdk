// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Collections.Generic;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Entities.Signals
{
    public sealed class EntitySignalContractBuilder
    {
        private readonly List<EntitySignalMember> _inputs = new();
        private readonly List<EntitySignalMember> _outputs = new();
        private readonly List<EntitySignalMember> _properties = new();
        private readonly List<EntitySignalMember> _states = new();
        private EntitySignalCapabilities _capabilities;

        public EntitySignalContractBuilder(string entityClassName)
        {
            EntityClassName = entityClassName;
        }

        public string EntityClassName { get; }

        public EntitySignalContractBuilder AddCapability(EntitySignalCapabilities capability)
        {
            _capabilities |= capability;
            return this;
        }

        public EntitySignalContractBuilder AddInput(string name,
            EntitySignalValueType valueType = EntitySignalValueType.String,
            EntitySignalAuthority authority = EntitySignalAuthority.Server)
        {
            AddOrReplace(_inputs, new EntitySignalMember(name, EntitySignalMemberKind.Input, valueType, authority));
            return this;
        }

        public EntitySignalContractBuilder AddOutput(string name,
            EntitySignalValueType valueType = EntitySignalValueType.String,
            EntitySignalAuthority authority = EntitySignalAuthority.Server)
        {
            AddOrReplace(_outputs, new EntitySignalMember(name, EntitySignalMemberKind.Output, valueType, authority));
            return this;
        }

        public EntitySignalContractBuilder AddProperty(string name, EntitySignalValueType valueType,
            bool readable, bool writable, string typeName = null,
            EntitySignalAuthority authority = EntitySignalAuthority.Server)
        {
            AddOrReplace(_properties,
                new EntitySignalMember(name, EntitySignalMemberKind.Property, valueType, authority, readable, writable,
                    typeName));
            return this;
        }

        public EntitySignalContractBuilder AddState(string name, string stateTypeName,
            bool readable = true, bool writable = true,
            EntitySignalAuthority authority = EntitySignalAuthority.Server)
        {
            AddOrReplace(_states,
                new EntitySignalMember(name, EntitySignalMemberKind.State, EntitySignalValueType.State, authority,
                    readable, writable, stateTypeName));
            return this;
        }

        public EntitySignalContract Build()
        {
            return new EntitySignalContract(EntityClassName, _inputs.ToArray(), _outputs.ToArray(),
                _properties.ToArray(), _states.ToArray(), _capabilities);
        }

        public static EntitySignalContractBuilder FromMarker(EntityMarker marker)
        {
            var builder = new EntitySignalContractBuilder(marker ? marker.EntityClassName : null);

            AddSignalNames(builder._inputs, marker ? marker.Inputs : Array.Empty<string>(),
                EntitySignalMemberKind.Input);
            AddSignalNames(builder._outputs, marker ? marker.Outputs : Array.Empty<string>(),
                EntitySignalMemberKind.Output);
            if (marker)
            {
                builder.AddCapability(marker.SignalCapabilities);
                AddCommonCapabilities(builder, marker.SignalCapabilities);
                marker.ConfigureSignalContract(builder);
            }

            return builder;
        }

        private static void AddCommonCapabilities(EntitySignalContractBuilder builder,
            EntitySignalCapabilities capabilities)
        {
            if ((capabilities & EntitySignalCapabilities.Lifecycle) != 0)
            {
                builder.AddInput("kill", EntitySignalValueType.None);
                builder.AddOutput("killed", EntitySignalValueType.None);
            }

            if ((capabilities & EntitySignalCapabilities.Operational) == 0)
                return;

            builder.AddInput("enable", EntitySignalValueType.None);
            builder.AddInput("disable", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("enabled", EntitySignalValueType.None);
            builder.AddOutput("disabled", EntitySignalValueType.None);
            builder.AddProperty("is_enabled", EntitySignalValueType.Bool, true, false);
        }

        private static void AddSignalNames(ICollection<EntitySignalMember> destination, string[] names,
            EntitySignalMemberKind kind)
        {
            if (names == null)
                return;

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                AddUnique(destination,
                    new EntitySignalMember(name, kind, EntitySignalValueType.String));
            }
        }

        private static void AddUnique(ICollection<EntitySignalMember> destination, EntitySignalMember member)
        {
            if (member == null || string.IsNullOrWhiteSpace(member.NormalizedName))
                return;

            foreach (var existing in destination)
            {
                if (existing == null)
                    continue;

                if (string.Equals(existing.NormalizedName, member.NormalizedName,
                        StringComparison.OrdinalIgnoreCase))
                    return;
            }

            destination.Add(member);
        }

        private static void AddOrReplace(List<EntitySignalMember> destination, EntitySignalMember member)
        {
            if (member == null || string.IsNullOrWhiteSpace(member.NormalizedName))
                return;

            for (var i = 0; i < destination.Count; i++)
            {
                var existing = destination[i];
                if (existing == null || !string.Equals(existing.NormalizedName, member.NormalizedName,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                destination[i] = member;
                return;
            }

            destination.Add(member);
        }
    }
}