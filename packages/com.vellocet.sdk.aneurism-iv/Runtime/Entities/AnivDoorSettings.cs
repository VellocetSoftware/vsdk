using System;
using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class AnivDoorSettings : MonoBehaviour
    {
        [Serializable]
        public struct LocalizedText
        {
            public string table;
            public long keyId;
            public string key;
            public int fallback;
            public bool waitForCompletion;
        }
        [Serializable]
        public sealed class DoorOptions
        {
            [Header("Appearance")] public string doorName;
            [TextArea] public string description;
            public Color color;
            [HideInInspector] public LocalizedText localizedName;
            [HideInInspector] public LocalizedText localizedDescription;
            [Header("Interaction")] public bool standardInteractions = true;
            public bool breakable = true;
            [Header("Standalone lock")] public bool allowStandaloneLocking;
            [Tooltip("Fate attributes allowed to operate this standalone lock.")] public int allowedFateAttributes;
            [Header("Property")] public bool isProperty;
            public PropertyMarker property;
        }
        [SerializeField] private DoorOptions options = new();
        public DoorOptions Options => options;
    }
}
