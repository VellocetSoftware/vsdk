using System;
using UnityEngine;

namespace Vellocet.Sdk.Entities.Base
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EntityStringChoicesAttribute : PropertyAttribute
    {
        public string[] Choices { get; }
        public EntityStringChoicesAttribute(params string[] idAndLabel)
        {
            if (idAndLabel == null || idAndLabel.Length % 2 != 0) throw new ArgumentException("String choices need ID/label pairs.");
            Choices = idAndLabel;
        }
    }
}
