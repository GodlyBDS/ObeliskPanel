namespace ObeliskPanel;

public enum IniKind { GUS, Game }
public enum SType { Float, Int, Bool, Text }

public record SettingDef(string Page, string Group, string Label, IniKind File, string Section,
                         string Key, SType Type, string Default, string Desc = "", GameType? Only = null)
{
    public bool AppliesTo(GameType g) => Only == null || Only == g;
}

public static class SettingDefs
{
    public const string SS = "ServerSettings";
    public const string GM = "/script/shootergame.shootergamemode";
    public const string US = "/script/shootergame.shootergameusersettings";

    const SType F = SType.Float, I = SType.Int, B = SType.Bool;

    static SettingDef G(string page, string group, string label, string key, SType t, string def, string desc = "")
        => new(page, group, label, IniKind.GUS, SS, key, t, def, desc);
    static SettingDef M(string page, string group, string label, string key, SType t, string def, string desc = "")
        => new(page, group, label, IniKind.Game, GM, key, t, def, desc);
    static SettingDef AsaOnly(SettingDef d) => d with { Only = GameType.ASA };
    static SettingDef AseOnly(SettingDef d) => d with { Only = GameType.ASE };
    static SettingDef U(string page, string group, string label, string key, SType t, string def, string desc = "")
        => new(page, group, label, IniKind.GUS, US, key, t, def, desc);

    const string Gen = "General", Din = "Dinos General and Taming", Rat = "Rates",
                 Bre = "Breeding", Day = "Day and Night Cycle", Dec = "Decay and Spoil";

    public static readonly List<SettingDef> All = new()
    {
        M(Gen, "Miscellaneous", "Max Tribe Members", "MaxNumberOfPlayersInTribe", I, "0", "Max players in a tribe, 0 = unlimited"),
        G(Gen, "Miscellaneous", "Always Allow Structure Pickup", "AlwaysAllowStructurePickup", B, "False", "Structures can always be picked up"),
        G(Gen, "Miscellaneous", "Structure Pickup Hold Duration", "StructurePickupHoldDuration", F, "0.5", "Seconds to hold for picking up"),
        G(Gen, "Miscellaneous", "Global Item Stack Size Multiplier", "ItemStackSizeMultiplier", F, "1", "Multiplies the stack size of all items"),
        M(Gen, "Miscellaneous", "No Friendly Fire PvE", "bPvEDisableFriendlyFire", B, "False"),
        M(Gen, "Miscellaneous", "No Friendly Fire PvP", "bDisableFriendlyFire", B, "False"),
        M(Gen, "Miscellaneous", "Structure Damage Repair Cooldown", "StructureDamageRepairCooldown", I, "180", "Seconds after damage before a structure can be repaired"),
        G(Gen, "Miscellaneous", "Auto Save Period (minutes)", "AutoSavePeriodMinutes", F, "15"),
        G(Gen, "Miscellaneous", "Kick Idle Players Period (sec.)", "KickIdlePlayersPeriod", F, "3600"),
        G(Gen, "Miscellaneous", "Allow Hit Markers", "AllowHitMarkers", B, "True"),
        G(Gen, "Miscellaneous", "Tribe Name Change Cooldown (min.)", "TribeNameChangeCooldown", F, "15"),
        M(Gen, "Miscellaneous", "Loot Quality Multiplier", "SupplyCrateLootQualityMultiplier", F, "1", "Higher values increase the quality of loot drops"),
        AsaOnly(M(Gen, "Miscellaneous", "Speed Leveling", "bAllowSpeedLeveling", B, "False", "Allow points in speed for players and ground creatures")),
        G(Gen, "Miscellaneous", "Show Floating Damage Text", "ShowFloatingDamageText", B, "False", "Show damage numbers"),
        M(Gen, "Miscellaneous", "Crop Growth Speed Multiplier", "CropGrowthSpeedMultiplier", F, "1", "Higher values increase crop growth speed"),
        M(Gen, "Miscellaneous", "Unlimited Respecs", "bAllowUnlimitedRespecs", B, "False", "Removes the 24h cooldown on mindwipe tonics"),
        M(Gen, "Miscellaneous", "Corpse Life Span Multiplier", "GlobalCorpseDecompositionTimeMultiplier", F, "1", "Higher values keep corpses around longer"),
        M(Gen, "Miscellaneous", "Show Corpse Locator", "bUseCorpseLocator", B, "True", "Map marker at the location of player deaths"),
        G(Gen, "Miscellaneous", "Prevent Spawn Animations", "PreventSpawnAnimations", B, "False"),
        U(Gen, "Miscellaneous", "Show Steam Name in Chat", "bChatShowSteamName", B, "False", "Show platform name in chat"),
        G(Gen, "Miscellaneous", "Force All Structure Locking", "ForceAllStructureLocking", B, "False", "New structures are locked by default"),
        G(Gen, "Miscellaneous", "Disable Gamma Settings In PvE", "DisablePvEGamma", B, "False", "Prevents the 'gamma' command in PvE"),
        G(Gen, "Miscellaneous", "Enable PvP Gamma Changes", "EnablePvPGamma", B, "False", "Allows the 'gamma' command in PvP"),

        G(Gen, "Player", "XP Multiplier", "XPMultiplier", F, "1"),
        G(Gen, "Player", "Difficulty Offset", "DifficultyOffset", F, "1"),
        G(Gen, "Player", "Override Official Difficulty", "OverrideOfficialDifficulty", F, "0", "0 = off, 5 = wild creatures up to level 150"),
        G(Gen, "Player", "Player Damage Multiplier", "PlayerDamageMultiplier", F, "1"),
        G(Gen, "Player", "Player Resistance Multiplier", "PlayerResistanceMultiplier", F, "1", "Lower values mean less damage taken"),
        G(Gen, "Player", "Player Food Drain Multiplier", "PlayerCharacterFoodDrainMultiplier", F, "1"),
        G(Gen, "Player", "Player Water Drain Multiplier", "PlayerCharacterWaterDrainMultiplier", F, "1"),
        G(Gen, "Player", "Player Stamina Drain Multiplier", "PlayerCharacterStaminaDrainMultiplier", F, "1"),
        G(Gen, "Player", "Player Health Recovery Multiplier", "PlayerCharacterHealthRecoveryMultiplier", F, "1"),
        G(Gen, "Player", "Allow Third Person", "AllowThirdPersonPlayer", B, "True"),
        G(Gen, "Player", "Server Crosshair", "ServerCrosshair", B, "True"),
        G(Gen, "Player", "Show Map Player Location", "ShowMapPlayerLocation", B, "True"),
        G(Gen, "Player", "Hardcore", "ServerHardcore", B, "False", "Reset to level 1 on death"),

        G(Gen, "Structures", "Max Structures In Range", "TheMaxStructuresInRange", I, "10500"),
        G(Gen, "Structures", "Structure Resistance Multiplier", "StructureResistanceMultiplier", F, "1"),
        G(Gen, "Structures", "Structure Damage Multiplier", "StructureDamageMultiplier", F, "1"),
        G(Gen, "Structures", "Per Platform Max Structures Multiplier", "PerPlatformMaxStructuresMultiplier", F, "1"),
        G(Gen, "Structures", "Platform Saddle Build Area Multiplier", "PlatformSaddleBuildAreaBoundsMultiplier", F, "1"),
        G(Gen, "Structures", "Max Platform Saddle Structure Limit", "MaxPlatformSaddleStructureLimit", I, "75"),
        M(Gen, "Structures", "Allow Platform Saddle Multi Floors", "bAllowPlatformSaddleMultiFloors", B, "False"),
        AsaOnly(G(Gen, "Structures", "Allow Cryo Fridge On Saddle", "AllowCryoFridgeOnSaddle", B, "False")),
        G(Gen, "Structures", "Allow Cave Building PvE", "AllowCaveBuildingPvE", B, "False"),
        G(Gen, "Structures", "Allow Cave Building PvP", "AllowCaveBuildingPvP", B, "True"),
        M(Gen, "Structures", "Disable Structure Placement Collision", "bDisableStructurePlacementCollision", B, "False", "Structures can clip into the terrain"),
        M(Gen, "Structures", "Ignore Structure Prevention Volumes", "bIgnoreStructuresPreventionVolumes", B, "False", "Allow building in caves and artifact areas"),

        G(Gen, "Cluster Transfers", "Allow Foreign Dino Downloads", "CrossARKAllowForeignDinoDownloads", B, "False", "Allow dinos from other maps"),
        G(Gen, "Cluster Transfers", "Prevent Download Survivors", "PreventDownloadSurvivors", B, "False"),
        G(Gen, "Cluster Transfers", "Prevent Download Items", "PreventDownloadItems", B, "False"),
        G(Gen, "Cluster Transfers", "Prevent Download Dinos", "PreventDownloadDinos", B, "False"),
        G(Gen, "Cluster Transfers", "Prevent Upload Survivors", "PreventUploadSurvivors", B, "False"),
        G(Gen, "Cluster Transfers", "Prevent Upload Items", "PreventUploadItems", B, "False"),
        G(Gen, "Cluster Transfers", "Prevent Upload Dinos", "PreventUploadDinos", B, "False"),

        G(Din, "", "Flyers Can Carry Players In PvE", "AllowFlyerCarryPvE", B, "False"),
        M(Din, "", "Flyer Speed Levelling", "bAllowFlyerSpeedLeveling", B, "False", "Allow points in speed for flyers"),
        G(Din, "", "Tame Speed Multiplier", "TamingSpeedMultiplier", F, "1", "Higher values make taming faster"),
        G(Din, "", "Server Max Tames", "MaxTamedDinos", I, "5000", "Max tamed dinos on the server"),
        G(Din, "", "Max Tribe Tames", "MaxPersonalTamedDinos", I, "0", "Max tames per tribe (0 = off, official 500)"),
        G(Din, "", "Creature Food Drain Multiplier", "DinoCharacterFoodDrainMultiplier", F, "1"),
        M(Din, "", "Tamed Creature Food Drain Multiplier", "TamedDinoCharacterFoodDrainMultiplier", F, "1"),
        M(Din, "", "Tamed Creature Torpor Drain Multiplier", "TamedDinoTorporDrainMultiplier", F, "1"),
        M(Din, "", "No Creature Riding", "bDisableDinoRiding", B, "False"),
        G(Din, "", "Creature Count Multiplier", "DinoCountMultiplier", F, "1", "More creatures per spawn point"),
        G(Din, "", "Dino Damage Multiplier", "DinoDamageMultiplier", F, "1"),
        G(Din, "", "Dino Resistance Multiplier", "DinoResistanceMultiplier", F, "1"),
        G(Din, "", "Tamed Dino Damage Multiplier", "TamedDinoDamageMultiplier", F, "1"),
        G(Din, "", "Tamed Dino Resistance Multiplier", "TamedDinoResistanceMultiplier", F, "1"),
        G(Din, "Raid Settings", "Allow Titanosaur Feeding", "AllowRaidDinoFeeding", B, "False"),
        G(Din, "Raid Settings", "Raid Dino Food Drain Multiplier", "RaidDinoCharacterFoodDrainMultiplier", F, "1"),

        G(Rat, "", "Global Harvest Rate Multiplier", "HarvestAmountMultiplier", F, "1"),
        G(Rat, "", "Harvest Health Multiplier", "HarvestHealthMultiplier", F, "1"),
        M(Rat, "", "Player Harvest Damage Multiplier", "PlayerHarvestingDamageMultiplier", F, "1"),
        M(Rat, "", "Creature Harvest Damage Multiplier", "DinoHarvestingDamageMultiplier", F, "3.2"),
        G(Rat, "", "Use Optimized Harvesting Rates", "UseOptimizedHarvestingHealth", B, "False"),
        G(Rat, "", "Clamp Harvesting Damage", "ClampResourceHarvestDamage", B, "False"),
        G(Rat, "", "Resource Respawn Period Multiplier", "ResourcesRespawnPeriodMultiplier", F, "1", "Lower values respawn resources faster"),

        M(Bre, "", "Mature Speed Multiplier", "BabyMatureSpeedMultiplier", F, "1", "Higher values make babies mature faster"),
        M(Bre, "", "Incubation Speed Multiplier", "EggHatchSpeedMultiplier", F, "1", "Higher values make eggs hatch faster"),
        M(Bre, "", "Egg Lay Period Multiplier", "LayEggIntervalMultiplier", F, "1", "Lower values increase the egg laying rate"),
        M(Bre, "", "Baby Food Consumption Multiplier", "BabyFoodConsumptionSpeedMultiplier", F, "1"),
        M(Bre, "", "Mating Speed Multiplier", "MatingSpeedMultiplier", F, "1", "Higher values make mating faster"),
        M(Bre, "", "Mating Interval Multiplier", "MatingIntervalMultiplier", F, "1", "Lower values let creatures mate again sooner"),
        M(Bre, "", "Imprint Period Multiplier", "BabyCuddleIntervalMultiplier", F, "1", "Lower values allow imprinting more often"),
        M(Bre, "", "Imprint Stat Scale Multiplier", "BabyImprintingStatScaleMultiplier", F, "1"),
        M(Bre, "", "Imprint Grace Period Multiplier", "BabyCuddleGracePeriodMultiplier", F, "1"),
        M(Bre, "", "Imprint Loss Speed Multiplier", "BabyCuddleLoseImprintQualitySpeedMultiplier", F, "1"),
        M(Bre, "", "Imprint Amount Multiplier", "BabyImprintAmountMultiplier", F, "1", "More imprint per cuddle"),
        G(Bre, "", "Anyone Can Imprint", "AllowAnyoneBabyImprintCuddle", B, "False"),

        G(Day, "", "Day Cycle Speed Scale", "DayCycleSpeedScale", F, "1", "Overall speed of the day/night cycle"),
        G(Day, "", "Day Time Speed Scale", "DayTimeSpeedScale", F, "1", "Higher values make days pass faster"),
        G(Day, "", "Night Time Speed Scale", "NightTimeSpeedScale", F, "1", "Higher values make nights pass faster"),

        G(Dec, "Decay", "Disable Structure Decay PvE", "DisableStructureDecayPvE", B, "False"),
        G(Dec, "Decay", "PvE Structure Decay Multiplier", "PvEStructureDecayPeriodMultiplier", F, "1"),
        G(Dec, "Decay", "Disable Dino Decay PvE", "DisableDinoDecayPvE", B, "False"),
        G(Dec, "Decay", "PvE Dino Decay Multiplier", "PvEDinoDecayPeriodMultiplier", F, "1"),
        G(Dec, "Decay", "PvP Structure Decay", "PvPStructureDecay", B, "False"),
        G(Dec, "Decay", "PvP Dino Decay", "PvPDinoDecay", B, "False"),
        G(Dec, "Decay", "Auto Destroy Old Structures Multiplier", "AutoDestroyOldStructuresMultiplier", F, "0"),
        G(Dec, "Decay", "Auto Destroy Decayed Dinos", "AutoDestroyDecayedDinos", B, "False"),
        M(Dec, "Spoil", "Global Spoiling Time Multiplier", "GlobalSpoilingTimeMultiplier", F, "1", "Higher values make food spoil slower"),
        M(Dec, "Spoil", "Item Decomposition Time Multiplier", "GlobalItemDecompositionTimeMultiplier", F, "1", "Higher values keep dropped items around longer"),
        G(Dec, "Spoil", "Clamp Item Spoiling Times", "ClampItemSpoilingTimes", B, "False"),

        AsaOnly(G(Din, "Cryopods & Tame Limits", "Disable Cryopod Enemy Check", "DisableCryopodEnemyCheck", B, "False", "Cryopods work even when enemies are nearby")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Disable Cryopod Fridge Requirement", "DisableCryopodFridgeRequirement", B, "False", "Cryopods can be used without a Cryofridge nearby")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Soft Tame Limit", "MaxTamedDinos_SoftTameLimit", I, "5000", "Tames above this count get marked for deletion")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Soft Tame Limit Deletion Countdown (sec.)", "MaxTamedDinos_SoftTameLimit_CountdownForDeletionDuration", I, "604800", "Time before tames over the soft limit are deleted")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Destroy Tames Over The Soft Tame Limit", "DestroyTamesOverTheSoftTameLimit", B, "False")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Armadoggo Death Cooldown (sec.)", "ArmadoggoDeathCooldown", F, "3600")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Young Ice Fox Death Cooldown (sec.)", "YoungIceFoxDeathCooldown", F, "3600")),
        AsaOnly(G(Din, "Cryopods & Tame Limits", "Disable Burrow Decay Timers", "DisableBurrowDecayTimers", B, "False")),

        AsaOnly(G(Gen, "Bunkers", "Limit Bunkers Per Tribe", "LimitBunkersPerTribe", B, "True")),
        AsaOnly(G(Gen, "Bunkers", "Bunkers Per Tribe", "LimitBunkersPerTribeNum", I, "3")),
        AsaOnly(G(Gen, "Bunkers", "Min Distance Between Bunkers", "MinDistanceBetweenBunkers", F, "3000", "Unreal units")),
        AsaOnly(G(Gen, "Bunkers", "Allow Bunkers In Prevention Zones", "AllowBunkersInPreventionZones", B, "False")),
        AsaOnly(G(Gen, "Bunkers", "Allow Bunker Modules Above Ground", "AllowBunkerModulesAboveGround", B, "False")),
        AsaOnly(G(Gen, "Bunkers", "Allow Bunker Modules In Prevention Zones", "AllowBunkerModulesInPreventionZones", B, "False")),
        AsaOnly(G(Gen, "Bunkers", "Allow Riding Dinos Inside Bunkers", "AllowRidingDinosInsideBunkers", B, "True")),
        AsaOnly(G(Gen, "Bunkers", "Allow Dino AI Inside Bunkers", "AllowDinoAIInsideBunkers", B, "True")),
        AsaOnly(G(Gen, "Bunkers", "Enemy Access Bunker HP Threshold", "EnemyAccessBunkerHPThreshold", F, "0.25", "Enemies can enter below this HP fraction")),
        AsaOnly(G(Gen, "Bunkers", "Bunker Under HP Threshold Damage Multiplier", "BunkerUnderHPThresholdDmgMultiplier", F, "0.05")),

        AsaOnly(G(Bre, "Cryo Hospital", "Hours To Regen HP", "CryoHospitalHoursToRegenHP", F, "1")),
        AsaOnly(G(Bre, "Cryo Hospital", "Hours To Regen Food", "CryoHospitalHoursToRegenFood", F, "24")),
        AsaOnly(G(Bre, "Cryo Hospital", "Hours To Drain Torpor", "CryoHospitalHoursToDrainTorpor", F, "1")),
        AsaOnly(G(Bre, "Cryo Hospital", "Mating Cooldown Reduction", "CryoHospitalMatingCooldownReduction", F, "2")),

        AsaOnly(G(Gen, "Bloodforge & Outposts", "Bloodforge Reinforce Extra Durability", "BloodforgeReinforceExtraDurability", F, "0.3")),
        AsaOnly(G(Gen, "Bloodforge & Outposts", "Bloodforge Reinforce Resource Cost Multiplier", "BloodforgeReinforceResourceCostMultiplier", F, "3")),
        AsaOnly(G(Gen, "Bloodforge & Outposts", "Bloodforge Reinforce Speed Multiplier", "BloodforgeReinforceSpeedMultiplier", F, "0.1")),
        AsaOnly(G(Gen, "Bloodforge & Outposts", "Outpost Sigil Reward Multiplier", "OutpostSigilRewardMultiplier", F, "1")),
        AsaOnly(M(Gen, "Bloodforge & Outposts", "Tribe Tower Bonus Multiplier", "TribeTowerBonusMultiplier", F, "2")),

        AsaOnly(M(Gen, "Tek & Crafting", "Disable Wireless Crafting", "bDisableWirelessCrafting", B, "False", "No crafting from Tek Dedicated Storage")),
        AsaOnly(M(Gen, "Tek & Crafting", "Disable Wireless Crafting For Players", "bDisableWirelessCraftingForPlayers", B, "False")),
        AsaOnly(M(Gen, "Tek & Crafting", "Disable Wireless Crafting For Dinos", "bDisableWirelessCraftingForDinos", B, "False")),
        AsaOnly(M(Gen, "Tek & Crafting", "Disable Wireless Crafting For Structures", "bDisableWirelessCraftingForStructures", B, "False")),
        AsaOnly(M(Gen, "Tek & Crafting", "Wireless Crafting Range", "WirelessCraftingRangeOverride", I, "3000", "Unreal units")),
        AsaOnly(M(Gen, "Tek & Crafting", "Limit Generators Number", "LimitGeneratorsNum", I, "3", "Generators allowed within the range below")),
        AsaOnly(M(Gen, "Tek & Crafting", "Limit Generators Range", "LimitGeneratorsRange", I, "15000", "Unreal units")),
        AsaOnly(G(Gen, "Tek & Crafting", "Max Train Cars", "MaxTrainCars", I, "8")),
        AsaOnly(G(Gen, "Tek & Crafting", "Cosmo Weapon Ammo Reload Amount", "CosmoWeaponAmmoReloadAmount", F, "1")),
        AsaOnly(G(Gen, "Tek & Crafting", "Max Cosmo Weapon Ammo", "MaxCosmoWeaponAmmo", F, "-1", "-1 = default")),

        AsaOnly(G(Gen, "ASA Misc", "Implant Suicide Cooldown (sec.)", "ImplantSuicideCD", F, "28800")),
        AsaOnly(G(Gen, "ASA Misc", "Ignore PvP Mounted Weaponry Restrictions", "IgnorePVPMountedWeaponryRestrictions", B, "False")),
        AsaOnly(G(Gen, "ASA Misc", "Use Character Tracker", "UseCharacterTracker", B, "False")),
        AsaOnly(G(Gen, "ASA Misc", "Use Astraeos Traversal Buff", "UseAstraeosTraversalBuff", B, "True")),
        AsaOnly(G(Gen, "ASA Misc", "King Kaiju Spawn Time", "WorldBossKingKaijuSpawnTime", SType.Text, "19:00:00", "hh:mm:ss")),
        AsaOnly(M(Gen, "ASA Misc", "Disable Photo Mode", "bDisablePhotoMode", B, "False")),
        AsaOnly(M(Gen, "ASA Misc", "Photo Mode Range Limit", "PhotoModeRangeLimit", I, "3000", "Max camera distance from the player")),
        AsaOnly(G(Gen, "ASA Misc", "Admin List URL", "AdminListURL", SType.Text, "", "Web alternative to AllowedCheaterAccountIDs.txt")),

        AseOnly(G(Gen, "ASE Misc", "Destroy Unconnected Water Pipes", "DestroyUnconnectedWaterPipes", B, "False")),
        AseOnly(G(Gen, "ASE Misc", "Allowed Cheaters URL", "AllowedCheatersURL", SType.Text, "", "Web alternative to AllowedCheaterAccountIDs.txt")),
        AseOnly(G(Gen, "ASE Misc", "Active Total Conversion (Mod ID)", "ActiveTotalConversion", SType.Text, "", "Workshop ID of a total conversion mod")),
    };
}
