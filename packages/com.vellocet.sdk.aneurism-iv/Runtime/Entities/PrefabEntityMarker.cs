using System;
using System.Collections.Generic;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class PrefabEntityMarker : EntityMarker, IMapModelMarker, IEntityInteractionMarker, IEntityImportGroup
    {
        [Serializable]
        public struct PersistenceEntry
        {
            public string part;
            public string id;
        }

        [SerializeField, HideInInspector] private PersistenceEntry[] legacyPersistence = Array.Empty<PersistenceEntry>();
        public IReadOnlyList<PersistenceEntry> LegacyPersistence => legacyPersistence;
        public abstract string ArchetypeId { get; }
    }
}
