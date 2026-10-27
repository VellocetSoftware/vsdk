using System;
using System.Collections.Generic;
using System.Linq;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public static class GameItem
    {
        public const string Ammo12Gauge = "ammo_12_gauge";
        public const string Ammo357Magnum = "ammo_357_magnum";
        public const string Ammo6_5x55mm = "ammo_6_5x55mm";
        public const string Ammo7_62x39mm = "ammo_7_62x39mm";
        public const string Ammo9mm = "ammo_9mm";
        public const string Ammo9x39 = "ammo_9x39";
        public const string A4Original = "a4_original";
        public const string A4Premium = "a4_premium";
        public const string A4Suisoda = "a4_suisoda";
        public const string AG42B = "ag42_b";
        public const string AchillesEnergy = "achilles_energy";
        public const string AdvancedGunParts = "advanced_gun_parts";
        public const string Antibacterium = "antibacterium";
        public const string AntiradPackage = "antirad_package";
        public const string Axle = "axle";
        public const string Bandage = "bandage";
        public const string Basketball = "basketball";
        public const string Benadrizole = "benadrizole";
        public const string BileBinty = "bile_binty";
        public const string Biofuel = "biofuel";
        public const string BloodPackage = "blood_package";
        public const string Bloodbagel = "bloodbagel";
        public const string Bloodysolyanka = "bloodysolyanka";
        public const string BoneBread = "bone_bread";
        public const string BoneashFlour = "boneash_flour";
        public const string BonecornSack = "bonecorn_sack";
        public const string Bonescone = "bonescone";
        public const string Bougiebigos = "bougiebigos";
        public const string BrainScorcher = "brain_scorcher";
        public const string BrainScorcherAlt = "brain_scorcher_alt";
        public const string CTXAhti = "ctx_ahti";
        public const string CTXAmmoPack = "ctx_ammo_pack";
        public const string CTXArbiter = "ctx_arbiter";
        public const string CTXConciliator = "ctx_conciliator";
        public const string CTXSurma = "ctx_surma";
        public const string CTXValta = "ctx_valta";
        public const string Cannedbread = "cannedbread";
        public const string CapitronBuildingKit = "capitron_building_kit";
        public const string CarcinogenicSlop = "carcinogenic_slop";
        public const string Cash = "cash";
        public const string Chainsaw = "chainsaw";
        public const string Chlebnadzianykielbasa = "chlebnadzianykielbasa";
        public const string Cigarettes = "cigarettes";
        public const string Cigarettesoup = "cigarettesoup";
        public const string CircuitBoard = "circuit_board";
        public const string Coal = "coal";
        public const string CrakMef = "crak_mef";
        public const string Cremator = "cremator";
        public const string CrimsonKing = "crimson_king";
        public const string CtxBattleSolution = "ctx_battle_solution";
        public const string CtxCombatJuice = "ctx_combat_juice";
        public const string Cube = "cube";
        public const string DeathRattle = "death_rattle";
        public const string DemonCore = "demon_core";
        public const string Dentedfoodcan = "dentedfoodcan";
        public const string Dewormer = "dewormer";
        public const string DirectorsDeath = "directors_death";
        public const string DirtyLaundry = "dirty_laundry";
        public const string DoozerBooze = "doozer_booze";
        public const string Dumpsterrat = "dumpsterrat";
        public const string Eldritchbeans = "eldritchbeans";
        public const string EmptyBottle = "empty_bottle";
        public const string Epinephrine = "epinephrine";
        public const string EvilCrakMef = "evil_crak_mef";
        public const string FesteringSerum = "festering_serum";
        public const string Fish = "fish";
        public const string Fists = "fists";
        public const string FistsAlt = "fists_alt";
        public const string Flashlight = "flashlight";
        public const string FoldedLaundry = "folded_laundry";
        public const string Frommer = "frommer";
        public const string Gabagool = "gabagool";
        public const string GalvanizedIngot = "galvanized_ingot";
        public const string Ghettospaghetti = "ghettospaghetti";
        public const string Glue = "glue";
        public const string Greasesiphon = "greasesiphon";
        public const string Guitar = "guitar";
        public const string GunPowder = "gun_powder";
        public const string GunsmithingTools = "gunsmithing_tools";
        public const string Gutteroil = "gutteroil";
        public const string Hemoblocker = "hemoblocker";
        public const string Hijacker = "hijacker";
        public const string IED = "ied";
        public const string Immunoplus = "immunoplus";
        public const string Jenkem = "jenkem";
        public const string JenkemLeftovers = "jenkem_leftovers";
        public const string Jerrycan = "jerrycan";
        public const string KevlarVest = "kevlar_vest";
        public const string KidneyBundle = "kidney_bundle";
        public const string KidneySplitter = "kidney_splitter";
        public const string Kielbasa = "kielbasa";
        public const string Knife = "knife";
        public const string LacedA4Premium = "laced_a4_premium";
        public const string LacedAdrenipen = "laced_adrenipen";
        public const string LacedCiggie = "laced_ciggie";
        public const string LacedRation = "laced_ration";
        public const string Leech = "leech";
        public const string Lighter = "lighter";
        public const string LiverFief = "liver_fief";
        public const string LiverFiefAlt = "liver_fief_alt";
        public const string LungBundle = "lung_bundle";
        public const string LungPuncher = "lung_puncher";
        public const string MachineParts = "machine_parts";
        public const string Maggotcheese = "maggotcheese";
        public const string Mbsecretformula = "mbsecretformula";
        public const string MetalOre = "metal_ore";
        public const string MetalPlate = "metal_plate";
        public const string Methanol = "methanol";
        public const string Moonchild = "moonchild";
        public const string Motleycanobullets = "motleycanobullets";
        public const string MuscleBundle = "muscle_bundle";
        public const string MuscleRipper = "muscle_ripper";
        public const string NailGun = "nail_gun";
        public const string NailTrap = "nail_trap";
        public const string Nails = "nails";
        public const string NerveBundle = "nerve_bundle";
        public const string Nightstick = "nightstick";
        public const string OldAmmo = "old_ammo";
        public const string OldGunParts = "old_gun_parts";
        public const string Organ = "organ";
        public const string PITY = "pity";
        public const string PM9EvilGun = "pm9_evil_gun";
        public const string ParasitePackage = "parasite_package";
        public const string PeetRock = "peet_rock";
        public const string Pemmican = "pemmican";
        public const string PetRat = "pet_rat";
        public const string PharmaTools = "pharma_tools";
        public const string Pickaxe = "pickaxe";
        public const string Pipe = "pipe";
        public const string Plastic = "plastic";
        public const string Pot = "pot";
        public const string Psychostatica = "psychostatica";
        public const string RDZ = "rdz";
        public const string Radio = "radio";
        public const string Radiobiotics = "radiobiotics";
        public const string Radiomitigant = "radiomitigant";
        public const string Ratglizzy = "ratglizzy";
        public const string Ration = "ration";
        public const string RepairKitLarge = "repair_kit_large";
        public const string RepairKitSmall = "repair_kit_small";
        public const string RitualAxe = "ritual_axe";
        public const string RotBomb = "rot_bomb";
        public const string RotPod = "rot_pod";
        public const string RotShroom = "rot_shroom";
        public const string Rottersgoulash = "rottersgoulash";
        public const string SanguineGrapesSack = "sanguine_grapes_sack";
        public const string SanityPackage = "sanity_package";
        public const string Sausageroll = "sausageroll";
        public const string Scarysoup = "scarysoup";
        public const string Schizopram = "schizopram";
        public const string ScrapMetal = "scrap_metal";
        public const string Screwdriver = "screwdriver";
        public const string SepsisAntiserum = "sepsis_antiserum";
        public const string SepsisPackage = "sepsis_package";
        public const string SepsisTonic = "sepsis_tonic";
        public const string Sievertsushi = "sievertsushi";
        public const string Skintape = "skintape";
        public const string SlopCan = "slop_can";
        public const string Slopsoup = "slopsoup";
        public const string SmugglingToolkit = "smuggling_toolkit";
        public const string Soil = "soil";
        public const string StimulantPackage = "stimulant_package";
        public const string StolenWarheadCore = "stolen_warhead_core";
        public const string StorageBox = "storage_box";
        public const string Stuffedratskewer = "stuffedratskewer";
        public const string Surstromming = "surstromming";
        public const string SuspiciousMeat = "suspicious_meat";
        public const string Switchblade = "switchblade";
        public const string Tantalizer = "tantalizer";
        public const string Textile = "textile";
        public const string TransitCard = "transit_card";
        public const string Tumorcrisps = "tumorcrisps";
        public const string TumortatoSack = "tumortato_sack";
        public const string Tushonka = "tushonka";
        public const string UnfoldedLaundry = "unfolded_laundry";
        public const string ViktorEVodka = "viktor_e_vodka";
        public const string VomitWeapon = "vomit_weapon";
        public const string WarheadCasing = "warhead_casing";
        public const string WarheadCore = "warhead_core";
        public const string WarheadDetonator = "warhead_detonator";
        public const string WarheadReceiver = "warhead_receiver";
        public const string WeakIngot = "weak_ingot";
        public const string WetLaundry = "wet_laundry";
        public const string Wood = "wood";
        public const string Wrench = "wrench";

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
        {
            Ammo12Gauge, Ammo357Magnum, Ammo6_5x55mm, Ammo7_62x39mm, Ammo9mm,
            Ammo9x39, A4Original, A4Premium, A4Suisoda, AG42B,
            AchillesEnergy, AdvancedGunParts, Antibacterium, AntiradPackage, Axle,
            Bandage, Basketball, Benadrizole, BileBinty, Biofuel,
            BloodPackage, Bloodbagel, Bloodysolyanka, BoneBread, BoneashFlour,
            BonecornSack, Bonescone, Bougiebigos, BrainScorcher, BrainScorcherAlt,
            CTXAhti, CTXAmmoPack, CTXArbiter, CTXConciliator, CTXSurma,
            CTXValta, Cannedbread, CapitronBuildingKit, CarcinogenicSlop, Cash,
            Chainsaw, Chlebnadzianykielbasa, Cigarettes, Cigarettesoup, CircuitBoard,
            Coal, CrakMef, Cremator, CrimsonKing, CtxBattleSolution,
            CtxCombatJuice, Cube, DeathRattle, DemonCore, Dentedfoodcan,
            Dewormer, DirectorsDeath, DirtyLaundry, DoozerBooze, Dumpsterrat,
            Eldritchbeans, EmptyBottle, Epinephrine, EvilCrakMef, FesteringSerum,
            Fish, Fists, FistsAlt, Flashlight, FoldedLaundry,
            Frommer, Gabagool, GalvanizedIngot, Ghettospaghetti, Glue,
            Greasesiphon, Guitar, GunPowder, GunsmithingTools, Gutteroil,
            Hemoblocker, Hijacker, IED, Immunoplus, Jenkem,
            JenkemLeftovers, Jerrycan, KevlarVest, KidneyBundle, KidneySplitter,
            Kielbasa, Knife, LacedA4Premium, LacedAdrenipen, LacedCiggie,
            LacedRation, Leech, Lighter, LiverFief, LiverFiefAlt,
            LungBundle, LungPuncher, MachineParts, Maggotcheese, Mbsecretformula,
            MetalOre, MetalPlate, Methanol, Moonchild, Motleycanobullets,
            MuscleBundle, MuscleRipper, NailGun, NailTrap, Nails,
            NerveBundle, Nightstick, OldAmmo, OldGunParts, Organ,
            PITY, PM9EvilGun, ParasitePackage, PeetRock, Pemmican,
            PetRat, PharmaTools, Pickaxe, Pipe, Plastic,
            Pot, Psychostatica, RDZ, Radio, Radiobiotics,
            Radiomitigant, Ratglizzy, Ration, RepairKitLarge, RepairKitSmall,
            RitualAxe, RotBomb, RotPod, RotShroom, Rottersgoulash,
            SanguineGrapesSack, SanityPackage, Sausageroll, Scarysoup, Schizopram,
            ScrapMetal, Screwdriver, SepsisAntiserum, SepsisPackage, SepsisTonic,
            Sievertsushi, Skintape, SlopCan, Slopsoup, SmugglingToolkit,
            Soil, StimulantPackage, StolenWarheadCore, StorageBox, Stuffedratskewer,
            Surstromming, SuspiciousMeat, Switchblade, Tantalizer, Textile,
            TransitCard, Tumorcrisps, TumortatoSack, Tushonka, UnfoldedLaundry,
            ViktorEVodka, VomitWeapon, WarheadCasing, WarheadCore, WarheadDetonator,
            WarheadReceiver, WeakIngot, WetLaundry, Wood, Wrench,
        });

        public static bool IsDefined(string id) => All.Contains(id);
    }
}
