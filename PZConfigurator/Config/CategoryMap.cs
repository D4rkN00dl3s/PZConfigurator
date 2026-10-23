namespace PZConfigurator.Config;

/// <summary>
/// Presentation-level grouping the config files don't contain themselves.
/// Keys missing from the maps fall through to "Other".
/// </summary>
public static class CategoryMap
{
    public const string Other = "Other";

    public static readonly string[] IniOrder =
    [
        "Details", "Map", "Players", "PVP", "Safehouse", "Faction",
        "Vehicles", "Voice", "Radio", "AntiCheat", "Word Filter", "Logs",
        "World", "Loot", "Other",
    ];

    public static readonly string[] SandboxOrder =
    [
        "Zombie", "World", "Loot", "Character", "Farming & Animals",
        "Vehicles", "Firearms", "Events", "Mods", "Other",
    ];

    // Lua nested tables that belong to a whole category; any unlisted
    // nested table is assumed to come from a mod.
    private static readonly Dictionary<string, string> LuaSections = new()
    {
        ["ZombieLore"] = "Zombie",
        ["ZombieConfig"] = "Zombie",
        ["Map"] = "World",
        ["Basement"] = "World",
        ["MultiplierConfig"] = "Character",
    };

    private static readonly Dictionary<string, string> IniCategories = new()
    {
        // Details: identity, announcement, connectivity, infra
        ["PublicName"] = "Details", ["PublicDescription"] = "Details", ["Public"] = "Details",
        ["Password"] = "Details", ["ServerWelcomeMessage"] = "Details",
        ["GlobalChat"] = "Details", ["ChatStreams"] = "Details",
        ["ChatMessageCharacterLimit"] = "Details", ["ChatMessageSlowModeTime"] = "Details",
        ["DefaultPort"] = "Details", ["UDPPort"] = "Details", ["RCONPort"] = "Details",
        ["RCONPassword"] = "Details", ["UPnP"] = "Details",
        ["server_browser_announced_ip"] = "Details",
        ["DiscordEnable"] = "Details", ["DiscordToken"] = "Details",
        ["DiscordChatChannel"] = "Details", ["DiscordLogChannel"] = "Details",
        ["DiscordCommandChannel"] = "Details", ["WebhookAddress"] = "Details",
        ["ResetID"] = "Details", ["ServerPlayerID"] = "Details", ["Seed"] = "Details",
        ["DoLuaChecksum"] = "Details", ["DenyLoginOnOverloadedServer"] = "Details",
        ["MaxAccountsPerUser"] = "Details", ["AllowCoop"] = "Details",
        ["LoginQueueEnabled"] = "Details", ["LoginQueueConnectTimeout"] = "Details",
        ["BackupsCount"] = "Details", ["BackupsOnStart"] = "Details",
        ["BackupsOnVersionChange"] = "Details", ["BackupsPeriod"] = "Details",
        ["SteamScoreboard"] = "Details", ["SteamVAC"] = "Details",
        ["MaxPacketsPerSecond"] = "Details", ["MultiplayerStatisticsPeriod"] = "Details",
        ["AllowNonAsciiUsername"] = "Details", ["PauseEmpty"] = "Details",

        // Map: world content loading, mods, spawn locations
        ["Map"] = "Map", ["Mods"] = "Map", ["WorkshopItems"] = "Map",
        ["SpawnPoint"] = "Map", ["SpawnItems"] = "Map",
        ["ShowCoordinates"] = "Map", ["MapRemotePlayerVisibility"] = "Map",
        ["SaveWorldEveryMinutes"] = "Map",

        // Players: accounts, characters, daily life
        ["Open"] = "Players", ["MaxPlayers"] = "Players", ["PingLimit"] = "Players",
        ["PlayerRespawnWithSelf"] = "Players", ["PlayerRespawnWithOther"] = "Players",
        ["DropOffWhiteListAfterDeath"] = "Players",
        ["DisplayUserName"] = "Players", ["ShowFirstAndLastName"] = "Players",
        ["UsernameDisguises"] = "Players", ["HideDisguisedUserName"] = "Players",
        ["MouseOverToSeeDisplayName"] = "Players", ["HidePlayersBehindYou"] = "Players",
        ["DisableScoreboard"] = "Players", ["HideAdminsInPlayerList"] = "Players",
        ["KnockedDownAllowed"] = "Players", ["SneakModeHideFromOtherPlayers"] = "Players",
        ["UltraSpeedDoesnotAffectToAnimals"] = "Players",
        ["UsePhysicsHitReaction"] = "Players", ["PlayerBumpPlayer"] = "Players",
        ["FastForwardMultiplier"] = "Players", ["SleepAllowed"] = "Players",
        ["SleepNeeded"] = "Players",

        // PVP & safety
        ["PVP"] = "PVP", ["PVPLogToolChat"] = "PVP", ["PVPLogToolFile"] = "PVP",
        ["SafetySystem"] = "PVP", ["ShowSafety"] = "PVP",
        ["SafetyToggleTimer"] = "PVP", ["SafetyCooldownTimer"] = "PVP",
        ["SafetyDisconnectDelay"] = "PVP", ["PVPMeleeWhileHitReaction"] = "PVP",
        ["PVPMeleeDamageModifier"] = "PVP", ["PVPFirearmDamageModifier"] = "PVP",
        ["War"] = "PVP", ["WarStartDelay"] = "PVP", ["WarDuration"] = "PVP",
        ["WarSafehouseHitPoints"] = "PVP",

        // Safehouses
        ["SafehousePreventsLootRespawn"] = "Safehouse",
        ["PlayerSafehouse"] = "Safehouse", ["AdminSafehouse"] = "Safehouse",
        ["SafehouseAllowTrepass"] = "Safehouse", ["SafehouseAllowFire"] = "Safehouse",
        ["SafehouseAllowLoot"] = "Safehouse", ["SafehouseAllowRespawn"] = "Safehouse",
        ["SafehouseDaySurvivedToClaim"] = "Safehouse",
        ["SafeHouseRemovalTime"] = "Safehouse",
        ["SafehouseAllowNonResidential"] = "Safehouse",
        ["SafehouseDisableDisguises"] = "Safehouse",
        ["MaxSafezoneSize"] = "Safehouse",
        ["DisableSafehouseWhenOwnerConnected"] = "Safehouse",
        ["SledgehammerOnlyInSafehouse"] = "Safehouse",
        ["AllowDestructionBySledgehammer"] = "Safehouse",

        // Faction
        ["Faction"] = "Faction",
        ["FactionDaySurvivedToCreate"] = "Faction",
        ["FactionPlayersRequiredForTag"] = "Faction",

        // Vehicles
        ["DisableVehicleTowing"] = "Vehicles", ["DisableTrailerTowing"] = "Vehicles",
        ["DisableBurntTowing"] = "Vehicles",
        ["CarEngineAttractionModifier"] = "Vehicles", ["TrashDeleteAll"] = "Vehicles",

        // Voice chat
        ["VoiceEnable"] = "Voice", ["VoiceMinDistance"] = "Voice",
        ["VoiceMaxDistance"] = "Voice", ["Voice3D"] = "Voice", ["ChatBroadcastVoice"] = "Voice",

        // Radio channels
        ["DisableRadioStaff"] = "Radio", ["DisableRadioAdmin"] = "Radio",
        ["DisableRadioGM"] = "Radio", ["DisableRadioOverseer"] = "Radio",
        ["DisableRadioModerator"] = "Radio", ["DisableRadioInvisible"] = "Radio",

        // Anti-cheat
        ["AntiCheatSafety"] = "AntiCheat", ["AntiCheatSpeed"] = "AntiCheat",
        ["AntiCheatNoClip"] = "AntiCheat", ["AntiCheatHit"] = "AntiCheat",
        ["AntiCheatPacketException"] = "AntiCheat", ["AntiCheatPermission"] = "AntiCheat",
        ["AntiCheatXP"] = "AntiCheat", ["AntiCheatSafeHouse"] = "AntiCheat",
        ["AntiCheatPlayer"] = "AntiCheat", ["AntiCheatChecksum"] = "AntiCheat",
        ["SpeedLimit"] = "AntiCheat",

        // Word filter
        ["BadWordListFile"] = "Word Filter", ["GoodWordListFile"] = "Word Filter",
        ["BadWordPolicy"] = "Word Filter", ["BadWordReplacement"] = "Word Filter",

        // Logs
        ["ClientCommandFilter"] = "Logs", ["ClientActionLogs"] = "Logs",
        ["PerkLogs"] = "Logs", ["AnnounceDeath"] = "Logs",
        ["AnnounceAnimalDeath"] = "Logs", ["BanKickGlobalSound"] = "Logs",

        // World behavior in the .ini
        ["SwitchZombiesOwnershipEachUpdate"] = "World", ["NoFire"] = "World",
        ["RemovePlayerCorpsesOnCorpseRemoval"] = "World", ["BloodSplatLifespanDays"] = "World",
        ["ItemNumbersLimitPerContainer"] = "Loot",
    };

    private static readonly Dictionary<string, string> LuaCategories = new()
    {
        // Zombie behaviour & population
        ["Zombies"] = "Zombie", ["Distribution"] = "Zombie",
        ["ZombieVoronoiNoise"] = "Zombie", ["ZombieRespawn"] = "Zombie",
        ["ZombieMigrate"] = "Zombie", ["MultiHitZombies"] = "Zombie",
        ["RearVulnerability"] = "Zombie", ["ZombieAttractionMultiplier"] = "Zombie",
        ["SirenEffectsZombies"] = "Zombie", ["AttackBlockMovements"] = "Zombie",
        ["AnimalSoundAttractZombies"] = "Zombie",

        // World: time, weather, utilities, map zones
        ["DayLength"] = "World", ["StartYear"] = "World", ["StartMonth"] = "World",
        ["StartDay"] = "World", ["StartTime"] = "World",
        ["DayNightCycle"] = "World", ["ClimateCycle"] = "World", ["FogCycle"] = "World",
        ["Temperature"] = "World", ["Rain"] = "World", ["MaxFogIntensity"] = "World",
        ["MaxRainFxIntensity"] = "World", ["EnableSnowOnGround"] = "World",
        ["ErosionSpeed"] = "World", ["ErosionDays"] = "World",
        ["WaterShut"] = "World", ["ElecShut"] = "World", ["AlarmDecay"] = "World",
        ["WaterShutModifier"] = "World", ["ElecShutModifier"] = "World",
        ["AlarmDecayModifier"] = "World", ["TimeSinceApo"] = "World",
        ["Alarm"] = "World", ["LockedHouses"] = "World", ["NatureAbundance"] = "World",
        ["FishAbundance"] = "World", ["AnnotatedMapChance"] = "World",
        ["AllowExteriorGenerator"] = "World", ["GeneratorFuelConsumption"] = "World",
        ["GeneratorSpawning"] = "World", ["GeneratorTileRange"] = "World",
        ["GeneratorVerticalPowerRange"] = "World", ["LightBulbLifespan"] = "World",
        ["ClayLakeChance"] = "World", ["ClayRiverChance"] = "World",
        ["MaximumFireFuelHours"] = "World", ["FireSpread"] = "World",
        ["EnablePoisoning"] = "World",

        // Loot
        ["FoodLootNew"] = "Loot", ["LiteratureLootNew"] = "Loot",
        ["SkillBookLoot"] = "Loot", ["RecipeResourceLoot"] = "Loot",
        ["MedicalLootNew"] = "Loot", ["SurvivalGearsLootNew"] = "Loot",
        ["CannedFoodLootNew"] = "Loot", ["WeaponLootNew"] = "Loot",
        ["RangedWeaponLootNew"] = "Loot", ["AmmoLootNew"] = "Loot",
        ["MechanicsLootNew"] = "Loot", ["OtherLootNew"] = "Loot",
        ["ClothingLootNew"] = "Loot", ["ContainerLootNew"] = "Loot",
        ["KeyLootNew"] = "Loot", ["MediaLootNew"] = "Loot",
        ["MementoLootNew"] = "Loot", ["CookwareLootNew"] = "Loot",
        ["MaterialLootNew"] = "Loot", ["FarmingLootNew"] = "Loot",
        ["ToolLootNew"] = "Loot", ["RollsMultiplier"] = "Loot",
        ["LootItemRemovalList"] = "Loot", ["RemoveStoryLoot"] = "Loot",
        ["RemoveZombieLoot"] = "Loot", ["ZombiePopLootEffect"] = "Loot",
        ["InsaneLootFactor"] = "Loot", ["ExtremeLootFactor"] = "Loot",
        ["RareLootFactor"] = "Loot", ["NormalLootFactor"] = "Loot",
        ["CommonLootFactor"] = "Loot", ["AbundantLootFactor"] = "Loot",
        ["SeenHoursPreventLootRespawn"] = "Loot", ["HoursForLootRespawn"] = "Loot",
        ["MaxItemsForLootRespawn"] = "Loot",
        ["ConstructionPreventsLootRespawn"] = "Loot",
        ["WorldItemRemovalList"] = "Loot", ["HoursForWorldItemRemoval"] = "Loot",
        ["ItemRemovalListBlacklistToggle"] = "Loot",
        ["DaysForRottenFoodRemoval"] = "Loot", ["FoodRotSpeed"] = "Loot",
        ["FridgeFactor"] = "Loot", ["DaysUntilMaximumLooted"] = "Loot",
        ["MaximumLooted"] = "Loot", ["RuralLooted"] = "Loot",
        ["MaximumDiminishedLoot"] = "Loot",
        ["DaysUntilMaximumDiminishedLoot"] = "Loot",
        ["MaximumLootedBuildingRooms"] = "Loot",

        // Character: stats, injuries, progression
        ["StarterKit"] = "Character", ["CharacterFreePoints"] = "Character",
        ["ConstructionBonusPoints"] = "Character", ["StatsDecrease"] = "Character",
        ["Nutrition"] = "Character", ["EndRegen"] = "Character",
        ["BoneFracture"] = "Character", ["InjurySeverity"] = "Character",
        ["HoursForCorpseRemoval"] = "Character",
        ["DecayingCorpseHealthImpact"] = "Character",
        ["BloodLevel"] = "Character", ["ClothingDegradation"] = "Character",
        ["MuscleStrainFactor"] = "Character", ["DiscomfortFactor"] = "Character",
        ["WoundInfectionFactor"] = "Character", ["EasyClimbing"] = "Character",
        ["AllClothesUnlocked"] = "Character", ["NoBlackClothes"] = "Character",
        ["EnableTaintedWaterText"] = "Character",
        ["MetaKnowledge"] = "Character", ["SeeNotLearntRecipe"] = "Character",
        ["NegativeTraitsPenalty"] = "Character", ["MinutesPerPage"] = "Character",
        ["LiteratureCooldown"] = "Character", ["LevelForMediaXPCutoff"] = "Character",
        ["LevelForDismantleXPCutoff"] = "Character",

        // Farming & Animals
        ["Farming"] = "Farming & Animals", ["CompostTime"] = "Farming & Animals",
        ["PlantResilience"] = "Farming & Animals",
        ["PlantAbundance"] = "Farming & Animals",
        ["KillInsideCrops"] = "Farming & Animals",
        ["PlantGrowingSeasons"] = "Farming & Animals",
        ["PlaceDirtAboveground"] = "Farming & Animals",
        ["FarmingSpeedNew"] = "Farming & Animals", ["FarmingAmountNew"] = "Farming & Animals",
        ["MaggotSpawn"] = "Farming & Animals",
        ["AnimalStatsModifier"] = "Farming & Animals",
        ["AnimalMetaStatsModifier"] = "Farming & Animals",
        ["AnimalPregnancyTime"] = "Farming & Animals",
        ["AnimalAgeModifier"] = "Farming & Animals",
        ["AnimalMilkIncModifier"] = "Farming & Animals",
        ["AnimalWoolIncModifier"] = "Farming & Animals",
        ["AnimalRanchChance"] = "Farming & Animals",
        ["AnimalGrassRegrowTime"] = "Farming & Animals",
        ["AnimalMetaPredator"] = "Farming & Animals",
        ["AnimalMatingSeason"] = "Farming & Animals",
        ["AnimalEggHatch"] = "Farming & Animals",
        ["AnimalTrackChance"] = "Farming & Animals",
        ["AnimalPathChance"] = "Farming & Animals",
        ["MaximumRatIndex"] = "Farming & Animals",
        ["DaysUntilMaximumRatIndex"] = "Farming & Animals",

        // Vehicles
        ["EnableVehicles"] = "Vehicles", ["CarSpawnRate"] = "Vehicles",
        ["VehicleEasyUse"] = "Vehicles", ["InitialGas"] = "Vehicles",
        ["FuelStationGasInfinite"] = "Vehicles", ["FuelStationGasMin"] = "Vehicles",
        ["FuelStationGasMax"] = "Vehicles", ["FuelStationGasEmptyChance"] = "Vehicles",
        ["LockedCar"] = "Vehicles", ["CarGasConsumption"] = "Vehicles",
        ["CarGeneralCondition"] = "Vehicles", ["CarDamageOnImpact"] = "Vehicles",
        ["DamageToPlayerFromHitByACar"] = "Vehicles", ["TrafficJam"] = "Vehicles",
        ["CarAlarm"] = "Vehicles", ["PlayerDamageFromCrash"] = "Vehicles",
        ["SirenShutoffHours"] = "Vehicles", ["ChanceHasGas"] = "Vehicles",
        ["RecentlySurvivorVehicles"] = "Vehicles", ["BAM_Server_MinSuccessChance"] = "Vehicles",

        // Firearms
        ["FirearmUseDamageChance"] = "Firearms",
        ["FirearmNoiseMultiplier"] = "Firearms",
        ["FirearmJamMultiplier"] = "Firearms",
        ["FirearmMoodleMultiplier"] = "Firearms",
        ["FirearmWeatherMultiplier"] = "Firearms",
        ["FirearmHeadGearEffect"] = "Firearms",
        ["NightDarkness"] = "World", ["NightLength"] = "World",
        ["ZombieHealthImpact"] = "Zombie",

        // World events
        ["Helicopter"] = "Events", ["MetaEvent"] = "Events",
        ["SleepingEvent"] = "Events", ["SurvivorHouseChance"] = "Events",
        ["ZoneStoryChance"] = "Events", ["VehicleStoryChance"] = "Events",
    };

    public static string ResolveIni(string key) =>
        IniCategories.TryGetValue(key, out var cat) ? cat : Other;

    /// <summary>Nested tables are resolved by section name; unlisted sections
    /// are assumed to be mod-added. Top-level keys fall back to "Other".</summary>
    public static string ResolveLua(string section, string key)
    {
        if (section != "General")
            return LuaSections.TryGetValue(section, out var cat) ? cat : "Mods";
        return LuaCategories.TryGetValue(key, out var cat2) ? cat2 : Other;
    }
}
