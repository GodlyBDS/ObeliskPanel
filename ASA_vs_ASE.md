# ASA vs. ASE – server settings compared

Source: [ark.wiki.gg – Server configuration](https://ark.wiki.gg/wiki/Server_configuration) (read 2026-11-19, 552 documented options). The wiki marks every option with *inASA* / *inASE*.

## Summary

| | INI settings | Command line options |
|---|---|---|
| Both games | 161 | 34 |
| **ASA only** | 52 | 18 |
| **ASE only** | 7 | 23 |
| ASE confirmed, ASA *unknown* in the wiki | 187 | 57 |
| Other / unclear | 3 | 10 |

> "Unknown" means the wiki has not confirmed it for ASA yet – most of these are old ASE settings that still work in ASA. ObeliskPanel therefore shows them in both modes.

## INI settings – ASA only (52)

| Setting | Where | Default | What it does |
|---|---|---|---|
| `AllowCryoFridgeOnSaddle` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `ArmadoggoDeathCooldown` | GameUserSettings.ini `[ServerSettings]` | 3600 |  |
| `CosmeticWhitelistOverride` | GameUserSettings.ini `[ServerSettings]` | – |  |
| `CosmoWeaponAmmoReloadAmount` | GameUserSettings.ini `[ServerSettings]` | 1 |  |
| `DestroyTamesOverTheSoftTameLimit` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `DisableBurrowDecayTimers` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `DisableCryopodEnemyCheck` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `DisableCryopodFridgeRequirement` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `IgnorePVPMountedWeaponryRestrictions` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `ImplantSuicideCD` | GameUserSettings.ini `[ServerSettings]` | 28800 |  |
| `MaxCosmoWeaponAmmo` | GameUserSettings.ini `[ServerSettings]` | -1 |  |
| `MaxTamedDinos_SoftTameLimit` | GameUserSettings.ini `[ServerSettings]` | 5000 |  |
| `MaxTamedDinos_SoftTameLimit_CountdownForDeletionDuration` | GameUserSettings.ini `[ServerSettings]` | 604800 |  |
| `MaxTrainCars` | GameUserSettings.ini `[ServerSettings]` | 8 |  |
| `UseAstraeosTraversalBuff` | GameUserSettings.ini `[ServerSettings]` | True |  |
| `YoungIceFoxDeathCooldown` | GameUserSettings.ini `[ServerSettings]` | 3600 |  |
| `WorldBossKingKaijuSpawnTime` | GameUserSettings.ini `[ServerSettings]` | 19:00:00 |  |
| `LimitBunkersPerTribe` | GameUserSettings.ini `[ServerSettings]` | True |  |
| `LimitBunkersPerTribeNum` | GameUserSettings.ini `[ServerSettings]` | 3 |  |
| `AllowBunkersInPreventionZones` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `AllowRidingDinosInsideBunkers` | GameUserSettings.ini `[ServerSettings]` | True |  |
| `AllowBunkerModulesAboveGround` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `AllowDinoAIInsideBunkers` | GameUserSettings.ini `[ServerSettings]` | True |  |
| `AllowBunkerModulesInPreventionZones` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `MinDistanceBetweenBunkers` | GameUserSettings.ini `[ServerSettings]` | 3000.0 |  |
| `EnemyAccessBunkerHPThreshold` | GameUserSettings.ini `[ServerSettings]` | 0.25 |  |
| `BunkerUnderHPThresholdDmgMultiplier` | GameUserSettings.ini `[ServerSettings]` | 0.05 |  |
| `CryoHospitalHoursToRegenHP` | GameUserSettings.ini `[ServerSettings]` | 1.0 |  |
| `CryoHospitalHoursToRegenFood` | GameUserSettings.ini `[ServerSettings]` | 24.0 |  |
| `CryoHospitalHoursToDrainTorpor` | GameUserSettings.ini `[ServerSettings]` | 1.0 |  |
| `CryoHospitalMatingCooldownReduction` | GameUserSettings.ini `[ServerSettings]` | 2.0 |  |
| `BloodforgeReinforceExtraDurability` | GameUserSettings.ini `[ServerSettings]` | 0.3 |  |
| `BloodforgeReinforceResourceCostMultiplier` | GameUserSettings.ini `[ServerSettings]` | 3.0 |  |
| `BloodforgeReinforceSpeedMultiplier` | GameUserSettings.ini `[ServerSettings]` | 0.1 |  |
| `MaxActiveOutposts` | GameUserSettings.ini `[ServerSettings]` | – |  |
| `MaxActiveResourceCaches` | GameUserSettings.ini `[ServerSettings]` | – |  |
| `MaxActiveCityOutposts` | GameUserSettings.ini `[ServerSettings]` | – |  |
| `OutpostSigilRewardMultiplier` | GameUserSettings.ini `[ServerSettings]` | 1.0 |  |
| `AdminListURL` | GameUserSettings.ini `[ServerSettings]` | N/A | Alternative to AllowedCheaterAccountIDs.txt (see Administrator Whitelisting) using a web resource. The interval at which the server queries … |
| `UseCharacterTracker` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `bAllowSpeedLeveling` | Game.ini `[/script/shootergame.shootergamemode]` | False | Specifies whether players and non-flyer creatures can have their Movement Speed levelled up. |
| `bDisablePhotoMode` | Game.ini `[/script/shootergame.shootergamemode]` | False | Defines if photo mode is allowed (False) or not (True). |
| `bDisableWirelessCrafting` | Game.ini `[/script/shootergame.shootergamemode]` | false | If True, prevents wireless crafting from Tek Dedicated Storage. |
| `bDisableWirelessCraftingForDinos` | Game.ini `[/script/shootergame.shootergamemode]` | false | If True, prevents wireless crafting from Tek Dedicated Storage when crafting in dino inventories. |
| `bDisableWirelessCraftingForPlayers` | Game.ini `[/script/shootergame.shootergamemode]` | false | If True, prevents wireless crafting from Tek Dedicated Storage when crafting in the player inventory. |
| `bDisableWirelessCraftingForStructures` | Game.ini `[/script/shootergame.shootergamemode]` | false | If True, prevents wireless crafting from Tek Dedicated Storage when crafting in structure inventories. |
| `LimitGeneratorsNum` | Game.ini `[/script/shootergame.shootergamemode]` | 3 | Limits the number of generators in the area defined by LimitGeneratorsRange. Official servers have it set to 3. |
| `LimitGeneratorsRange` | Game.ini `[/script/shootergame.shootergamemode]` | 15000 | Sets the area range (in Unreal Units) in which the option LimitGeneratorsNum applies. Official servers have it set to 15000. |
| `PhotoModeRangeLimit` | Game.ini `[/script/shootergame.shootergamemode]` | 3000 | Defines the maximum distance between photo mode camera position and player position. |
| `TribeTowerBonusMultiplier` | Game.ini `[/script/shootergame.shootergamemode]` | 2.0 | Multiplier for Tribe Tower bonus. |
| `WirelessCraftingRangeOverride` | Game.ini `[/script/shootergame.shootergamemode]` | 3000 | Sets the wireless crafting range (in Unreal Units) on Tek Dedicated Storage. |
| `ValgueroMemorialEntries` | Game.ini `[/script/shootergame.shootergamemode]` | N/A | The Valguero Memorial is now interactable, honouring those who have ascended by displaying their names. Server owners can customize the list… |

## INI settings – ASE only (7)

| Setting | Where | Default | What it does |
|---|---|---|---|
| `ActiveTotalConversion` | GameUserSettings.ini `[ServerSettings]` | – |  |
| `DestroyUnconnectedWaterPipes` | GameUserSettings.ini `[ServerSettings]` | False |  |
| `AllowedCheatersURL` | GameUserSettings.ini `[ServerSettings]` | N/A |  |
| `MaxPlayers` | GameUserSettings.ini `[/Script/Engine.GameSession]` | 70 | Specifies the maximum number of players that can play on the server simultaneously.ARK: Survival Ascended: This setting is replaced with -Wi… |
| `PreventTransferForClassNames` | Game.ini `[/script/shootergame.shootergamemode]` | N/A | Prevents transfer of specific creatures via classname. E.g. PreventTransferForClassNames="Argent_Character_BP_C"Creature classnames can be f… |
| `ModIDS` | Game.ini `[ModInstaller]` | – | Specifies a single Steam Workshop Mods/Maps/TC ID to download/install/update on the server. To handle multiple IDs, multiple lines must be a… |
| `ActiveEventColors` | Game.ini `[ModInstaller]` | N/A | This will activate the associated colours of an event: |

## Command line – ASA only (18)

| Setting | Where | Default | What it does |
|---|---|---|---|
| `-AlwaysTickDedicatedSkeletalMeshes` | Command line | – | Disables optimization to dynamically not tick idle/inactive creature animations on the dedicated server based on server frame rate (only bec… |
| `-DoCustomCosmeticValidation` | Command line | – | Enables validation of custom cosmetic mods. See this post for details. |
| `-DisableCustomCosmetics` | Command line | – | Disables the Custom Cosmetic system (allowing players to use and display special mods that should only be skins and will be downloaded autom… |
| `-disabledinonetrangescaling` | Command line | – | Disables creature's network replication range optimization. |
| `-EasterColors` | Command line | – | Chance for Easter colors when creatures spawn. |
| `-GBUsageToForceRestart=<value>` | Command line | 35 | Enabled an out of memory protection for servers. In the event that a server reaches an excessive amount of memory, the world will be forced … |
| `-mods=<ModId1>[,<ModId2>[...]]` | Command line | – | Specifies CurseForge Mod Project IDs. Mods are updated automatically when starting the server. |
| `-NoWildBabies` | Command line | – | Disables spawning of wild babies. |
| `-passivemods=<ModId1>[,<ModId2>[...]]` | Command line | – | This option disables a mod's functionality while still loading its data. This is useful if, for example, you want to have S-Dinos on Astraeo… |
| `-port=<Server Port>` | Command line | – | This is the server port used to host the dedicated server on. This is required if you want to run it on a port other than the default 7777. |
| `-ServerUseEventColors` | Command line | – | Enables event colors. |
| `-WinLiveMaxPlayers=<integer>` | Command line | 70 | Sets the maximum of players for the server. This currently replaces the MaxPlayers setting from the GameUserSettings.ini option. |
| `-disableCharacterTracker` | Command line | – | Used to disable character tracking. Alternatively, this option can be configured with UseCharacterTracker in file GameUserSettings.ini under… |
| `-EnableSteelShield` | Command line | – | Used to enable SteelShield anti-DDOS protection. This only works when your server is hosted by Nitrado. Undocumented by Wildcard. |
| `-ip=<ipv4_address>` | Command line | – | Option found in Nitrado server setup. Expected to work in a -MULTIHOME context. Undocumented by Wildcard. |
| `-NitradoQueryPort` | Command line | – | Specifics unknown. Undocumented by Wildcard. |
| `-ServerIP=<ipv4_address>` | Command line | – | Option found in Nitrado server setup. Expected to work in a -MULTIHOME context. Undocumented by Wildcard. |
| `-ServerPlatform=<plat1>[+<plat2>[...]]` | Command line | – | Allows the server to accept specified platforms. Options are PC for Steam, PS5 for PlayStation 5, XSX for XBOX, WINGDK for Microsoft Store, … |

## Command line – ASE only (23)

| Setting | Where | Default | What it does |
|---|---|---|---|
| `-AllowFlyerSpeedLeveling` | Command line | – | Enables Level Up for Flyer Movement Speed. Note: alternatively, it can be enabled adding bAllowFlyerSpeedLeveling=True to the Game.ini file. |
| `-automanagedmods` | Command line | – | Steam only, automatic MOD download/installation/updating. Mod IDs are listed in Game.ini under [ModInstaller] section. |
| `-crossplay` | Command line | – | Enables crossplay (i.e.: EPIC and Steam) on dedicated server. Note: PublicIPForEpic must also be set. |
| `-epiconly` | Command line | – | Enables Epic Game Store only players to connect to the dedicated server. |
| `?GameModIds=<ModID1>[,<ModID2>[...]]` | Command line | – | Steam only. Specifies the order and which mods are loaded, ModIDs need to be separated with commas (,). Mod priority is in descending order … |
| `-insecure` | Command line | – | Disable Valve Anti-Cheat (VAC) system. |
| `-MapModID=<ModID>` | Command line | – | Dedicated servers can now optionally load custom maps via ModID directly, rather than having to specify the map name, using this syntax (whe… |
| `-newsaveformat` | Command line | – | Enables the new save format (version 11), which is quicker at saving cryopods and requires less storage space for them. Use only if necessar… |
| `?NewYear1UTC=<epoch time>` | Command line | – | You can use https://www.epochconverter.com to get your appropriate timestamp. Please keep in mind that this event cannot be run before Midni… |
| `?NewYear2UTC=<epoch time>` | Command line | – | You can use https://www.epochconverter.com to get your appropriate timestamp. Please keep in mind that this event cannot be run before Midni… |
| `-NewYearEvent` | Command line | – | Enables the ARK: Happy New Year! event; it will automatically start at Midnight EST and Noon EST on the 1st of January, unless specified wit… |
| `-PublicIPForEpic=<IPAddress>` | Command line | – | This is the public IP that EGS clients will attempt to connect to, if this option is missing and -MULTIHOME is specified, then EGS clients w… |
| `-ServerAllowAnsel` | Command line | – | Sets the server to allow clients to use NVIDIA Ansel. See link for details on its support. |
| `-UseVivox` | Command line | – | Enables Vivox on Steam only servers. Default for EPIC servers but can be enabled for Steam server. |
| `?bRawSockets` | Command line | – | Direct UDP socket connections rather than Steam P2P. Deprecated since 311.78 and no longer present in the executable code. |
| `-forcenetthreading` | Command line | – | Defaulted dedicated server ?bRawSockets mode to not use threaded networking, seemed to generally be a net performance loss. Use this to forc… |
| `-nonetthreading` | Command line | – | Option for ?bRawSockets servers to only utilize a single thread for networking (useful to improve performance for machines with more servers… |
| `-allowansel` | Command line | – | Activates NVIDIA Ansel support in single player. Can be enabled on multiplayer servers with -ServerAllowAnsel since patch 278.0 for servers. |
| `-d3d10 -dx10 -sm4` | Command line | – | Any of these options forces the use DirectX 10 using shader model 4. This will reduce the graphics engine to a lesser version, reducing some… |
| `-d3d12 -dx12` | Command line | – | In ARK: Survival Evolved any of these options were used to force the DirectX 12 back-end. This mode no longer works due the removal of D3D12… |
| `-noaafonts` | Command line | – | Removed fonts anti-aliasing. Option is now deprecated. |
| `-nosteamclient` | Command line | – | Used automatically by client (ShooterGame) to launch the server from HOST\LOCAL menu. Launching the server (ShooterGameServer) directly or t… |
| `-USEALLAVAILABLECORES` | Command line | – | This is an Unreal Engine parameter, used while compiling shaders and cooking assets (i.e.: the ARK DevKit). This option is useless for dedic… |

## Important differences for running servers

| Topic | ASA | ASE |
|---|---|---|
| Server executable | `ArkAscendedServer.exe` (Steam app 2430930) | `ShooterGameServer.exe` (Steam app 376030) |
| Max players | `-WinLiveMaxPlayers=` on the command line (the INI value would be reset) | `[/Script/Engine.GameSession] MaxPlayers` or `?MaxPlayers=` |
| Mods | CurseForge project IDs via `-mods=` (`-passivemods=` to load data only) | Steam Workshop IDs: `ActiveMods=` in GUS + `[ModInstaller] ModIDS=` lines in Game.ini + `-automanagedmods` |
| Query port | not used | `QueryPort` (Steam server list) |
| Crossplay | `-ServerPlatform=PC+XSX+PS5+WINGDK` | `-crossplay` / `-epiconly` (Epic) |
| Speed leveling | `bAllowSpeedLeveling` (players and ground dinos) | always on for players, `-AllowFlyerSpeedLeveling` for flyers |
| Wild babies | `-NoWildBabies` | – |
| Cluster | `-clusterid` / `-ClusterDirOverride` – ASA and ASE clusters are not compatible | same options, separate cluster |

## What ObeliskPanel changed because of this comparison

- `OverrideOfficialDifficulty` default corrected to **0** (0 = override off). Max wild level = 30 × value: 5 = level 150, 10 = level 300. With the override at 0, `DifficultyOffset` 1.0 gives level 120 on The Island / Scorched Earth / Aberration and 150 on Extinction / Genesis.
- `bAllowPlatformSaddleMultiFloors` and `bDisableStructurePlacementCollision` moved to **Game.ini**.
- `AllowCryoFridgeOnSaddle` and `bAllowSpeedLeveling` only show in **ASA mode**.
- "Flyers Ridable In Caves" is the command line option `-ForceAllowCaveFlyers` (both games), now on the Main page instead of an INI entry.
- New ASA-only option "No Wild Babies" (`-NoWildBabies`) on the Main page.
- Max Players: ASA uses only `-WinLiveMaxPlayers`; the GameSession INI value is written for ASE only.
- ASE mods are now also written as `[ModInstaller] ModIDS=` lines in Game.ini, as `-automanagedmods` requires.
- 45 new ASA-only settings (cryopods, soft tame limit, bunkers, cryo hospital, bloodforge, outposts, Tek wireless crafting, generators, photo mode, …) and 3 ASE-only settings, each shown only in its mode.
