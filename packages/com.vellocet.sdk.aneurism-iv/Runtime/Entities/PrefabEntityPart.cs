using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    // Retains authored geometry while the game supplies this part's private behaviour.
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class PrefabEntityPart : MonoBehaviour
    {
        [SerializeField, HideInInspector] private PrefabEntityMarker owner;
        [SerializeField, HideInInspector] private string part;
        [SerializeField, HideInInspector] private int[] omittedNativeComponents = Array.Empty<int>();
        public IReadOnlyList<int> OmittedNativeComponents => omittedNativeComponents;
        public PrefabEntityMarker Owner => owner;
        public string Part => part;
    }
}
