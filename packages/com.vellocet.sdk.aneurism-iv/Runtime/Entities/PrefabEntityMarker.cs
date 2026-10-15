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
        public bool SupportsInteraction => EntityClassName is
            "func_aniv_door" or
            "prop_armory" or
            "prop_autostitcher" or
            "prop_boulder" or
            "prop_bulletin_board" or
            "prop_capitron" or
            "prop_capitron_rack" or
            "prop_civil_terminal" or
            "prop_container" or
            "prop_dealer_pickup" or
            "prop_distillery" or
            "prop_evil_one" or
            "prop_foundry" or
            "prop_item_stand" or
            "prop_kitchen" or
            "prop_lathe" or
            "prop_laundry_chute" or
            "prop_laundry_machine" or
            "prop_loading_bay" or
            "prop_loading_crate" or
            "prop_locker" or
            "prop_lunch_machine" or
            "prop_metro_checkpoint" or
            "prop_musicbox" or
            "prop_neuroconsole" or
            "prop_night_market" or
            "prop_packaging_station" or
            "prop_paystation" or
            "prop_pharma_machine" or
            "prop_planter" or
            "prop_rat_racer" or
            "prop_ration_station" or
            "prop_rolling_mill" or
            "prop_security_camera" or
            "prop_slot_machine" or
            "prop_soul_fetcher" or
            "prop_terminal" or
            "prop_vault" or
            "prop_vending_machine" or
            "prop_vent" or
            "prop_wardrobe" or
            "prop_water_source" or
            "prop_workbench";
    }
}
