// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;
using System.Globalization;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.VSig.Runtime
{
    public enum VSigValueKind
    {
        None,
        String,
        Int,
        Float,
        Bool,
        State
    }

    public readonly struct VSigValue
    {
        private VSigValue(VSigValueKind kind, object value)
        {
            Kind = kind;
            Value = value;
        }

        public VSigValueKind Kind { get; }
        public object Value { get; }
        public bool HasValue => Kind != VSigValueKind.None;

        public bool IsTruthy => Kind switch
        {
            VSigValueKind.Bool => Value is true,
            VSigValueKind.Int => Value is int intValue && intValue != 0,
            VSigValueKind.Float => Value is float floatValue && Math.Abs(floatValue) > float.Epsilon,
            VSigValueKind.String => !string.IsNullOrEmpty(Value as string),
            VSigValueKind.State => Value != null,
            _ => false
        };

        public static VSigValue None => new(VSigValueKind.None, null);

        public static VSigValue FromString(string value)
        {
            return new VSigValue(VSigValueKind.String, value ?? string.Empty);
        }

        public static VSigValue FromObject(object value)
        {
            return value switch
            {
                null => None,
                VSigValue vsigValue => vsigValue,
                string stringValue => FromString(stringValue),
                byte byteValue => new VSigValue(VSigValueKind.Int, (int)byteValue),
                short shortValue => new VSigValue(VSigValueKind.Int, (int)shortValue),
                int intValue => new VSigValue(VSigValueKind.Int, intValue),
                long longValue when longValue is >= int.MinValue and <= int.MaxValue =>
                    new VSigValue(VSigValueKind.Int, (int)longValue),
                float floatValue => new VSigValue(VSigValueKind.Float, floatValue),
                double doubleValue => new VSigValue(VSigValueKind.Float, (float)doubleValue),
                decimal decimalValue => new VSigValue(VSigValueKind.Float, (float)decimalValue),
                bool boolValue => new VSigValue(VSigValueKind.Bool, boolValue),
                IEntityStateSnapshot state => FromState(state),
                _ => FromString(value.ToString())
            };
        }

        public static VSigValue FromSerialized(string value, EntitySignalValueType type)
        {
            value ??= string.Empty;
            switch (type)
            {
                case EntitySignalValueType.Int:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)
                        ? FromObject(intValue)
                        : None;
                case EntitySignalValueType.Float:
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var floatValue)
                        ? FromObject(floatValue)
                        : None;
                case EntitySignalValueType.Bool:
                    return bool.TryParse(value, out var boolValue) ? FromObject(boolValue) : None;
                case EntitySignalValueType.None:
                    return None;
                default:
                    return FromString(value);
            }
        }

        public static VSigValue FromState(IEntityStateSnapshot state)
        {
            return state == null ? None : new VSigValue(VSigValueKind.State, state);
        }

        public bool TryGetState(out IEntityStateSnapshot state)
        {
            state = Value as IEntityStateSnapshot;
            return Kind == VSigValueKind.State && state != null;
        }

        public bool TryGetFloat(out float value)
        {
            switch (Value)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case float floatValue:
                    value = floatValue;
                    return true;
                case string stringValue:
                    return float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
                default:
                    value = 0f;
                    return false;
            }
        }

        public bool TryGetInt(out int value)
        {
            switch (Value)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case float floatValue when floatValue is >= int.MinValue and <= int.MaxValue:
                    value = (int)floatValue;
                    return true;
                case string stringValue:
                    return int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                default:
                    value = 0;
                    return false;
            }
        }

        public bool TryConvert(string typeName, out VSigValue converted)
        {
            converted = this;
            if (string.IsNullOrWhiteSpace(typeName) || typeName.Equals("any", StringComparison.OrdinalIgnoreCase))
                return true;

            switch (typeName.Trim().ToLowerInvariant())
            {
                case "string":
                    converted = FromString(ToString());
                    return true;
                case "int":
                    if (TryGetInt(out var intValue))
                    {
                        converted = FromObject(intValue);
                        return true;
                    }

                    return false;
                case "float":
                case "number":
                    if (TryGetFloat(out var floatValue))
                    {
                        converted = FromObject(floatValue);
                        return true;
                    }

                    return false;
                case "bool":
                    if (Kind == VSigValueKind.Bool)
                        return true;
                    if (Value is string text && bool.TryParse(text, out var boolValue))
                    {
                        converted = FromObject(boolValue);
                        return true;
                    }

                    if (TryGetFloat(out var number))
                    {
                        converted = FromObject(Math.Abs(number) > float.Epsilon);
                        return true;
                    }

                    return false;
                case "state":
                    return Kind is VSigValueKind.State or VSigValueKind.None;
                default:
                    if (Kind == VSigValueKind.None && typeName.EndsWith("State", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (Kind == VSigValueKind.State && Value is IEntityStateSnapshot state)
                        return string.Equals(state.StateType, typeName, StringComparison.OrdinalIgnoreCase);
                    return false;
            }
        }

        public object ToFormatObject()
        {
            return Value;
        }

        public static bool AreEqual(VSigValue left, VSigValue right)
        {
            if (left.TryGetFloat(out var a) && right.TryGetFloat(out var b))
                return Math.Abs(a - b) <= 0.00001f;
            return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return Value switch
            {
                float floatValue => floatValue.ToString("0.######", CultureInfo.InvariantCulture),
                double doubleValue => doubleValue.ToString("0.######", CultureInfo.InvariantCulture),
                int intValue => intValue.ToString(CultureInfo.InvariantCulture),
                bool boolValue => boolValue ? "true" : "false",
                _ => Value?.ToString() ?? string.Empty
            };
        }
    }
}