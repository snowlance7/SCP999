using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace SCP999
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin PluginInstance;
        public static ManualLogSource LoggerInstance;
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        public static PlayerControllerB LocalPlayer { get { return StartOfRound.Instance.localPlayerController; } }
        public static PlayerControllerB PlayerFromId(ulong id) { return StartOfRound.Instance.allPlayerScripts.Where(x => x.actualClientId == id).FirstOrDefault(); }
        public static bool IsServerOrHost { get { return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost; } }

        public static AssetBundle? ModAssets;

        // SCP-999 Rarity Configs
        public static ConfigEntry<string> config999LevelRarities;
        public static ConfigEntry<string> config999CustomLevelRarities;
        public static ConfigEntry<int> config999SCPDungeonRarity;

        // SCP-999 General Configs
        public static ConfigEntry<float> config999Size;
        public static ConfigEntry<int> configPlayerHealAmount;
        public static ConfigEntry<int> configEnemyHealAmount;
        public static ConfigEntry<float> configPlayerDetectionRange;
        public static ConfigEntry<float> configEnemyDetectionRange;
        public static ConfigEntry<float> configFollowRange;
        public static ConfigEntry<float> configHuggingRange;
        public static ConfigEntry<int> configMaxCandy;

        // Containment Jar Configs
        public static ConfigEntry<bool> configEnableJar;
        public static ConfigEntry<int> configJarPrice;
        public static ConfigEntry<int> configJar999Value;
        public static ConfigEntry<int> configJarSlimeValue;
        public static ConfigEntry<bool> configSlimeTaming;

        public static List<string> Sweets = new List<string> { "Blue Candy", "Green Candy", "Pink Candy", "Purple Candy", "Rainbow Candy", "Red Candy", "Yellow Candy", "Black Candy", "Candy", "Cake", "SCP-559" };

        private void Awake()
        {
            if (PluginInstance == null)
            {
                PluginInstance = this;
            }

            LoggerInstance = PluginInstance.Logger;

            harmony.PatchAll();

            InitializeNetworkBehaviours();

            // Configs

            // Rarity
            config999LevelRarities = Config.Bind("SCP-999 Rarities", "Level Rarities", "ExperimentationLevel:100, AssuranceLevel:75, VowLevel:75, OffenseLevel:50, AdamanceLevel:65, MarchLevel:50, RendLevel:45, DineLevel:45, TitanLevel:75, ArtificeLevel:70, EmbrionLevel:100, Modded:50", "Rarities for each level. See default for formatting.");
            config999CustomLevelRarities = Config.Bind("SCP-999 Rarities", "Custom Level Rarities", "Secret LabsLevel:150", "Rarities for modded levels. Same formatting as level rarities.");
            config999SCPDungeonRarity = Config.Bind("SCP-999 Rarities", "SCP Dungeon Rarity", 150, "The rarity of SCP-999 in the SCP Dungeon. Set to -1 to use level rarities.");

            // General
            config999Size = Config.Bind("General", "Size", 1f, "How big SCP-999 is");
            configPlayerHealAmount = Config.Bind("General", "Player Heal Amount", 10, "How much SCP-999 heals the player per second");
            configEnemyHealAmount = Config.Bind("General", "Enemy Heal Amount", 1, "How much SCP-999 heals the enemy per second");
            configPlayerDetectionRange = Config.Bind("General", "Player Detection Range", 50f, "How far SCP-999 can detect you");
            configEnemyDetectionRange = Config.Bind("General", "Enemy Detection Range", 15f, "How far SCP-999 can detect enemies");
            configFollowRange = Config.Bind("General", "Follow Range", 5f, "How far SCP-999 can follow you or other enemies");
            configHuggingRange = Config.Bind("General", "Hugging Range", 2f, "How far SCP-999 will be to you when rushing over to hug/heal you");
            configMaxCandy = Config.Bind("General", "Max Candy", 3, "Max amount of candy SCP-999 can eat before something bad happens");

            // Containment Jar
            configEnableJar = Config.Bind("Containment Jar", "Enable", true, "Enable Containment Jar");
            configJarPrice = Config.Bind("Containment Jar", "Price", 25, "Price of Containment Jar");
            configJar999Value = Config.Bind("Containment Jar", "SCP-999 Value", 1, "Value of Containment Jar with SCP-999 inside it");
            configJarSlimeValue = Config.Bind("Containment Jar", "Slime Value", 50, "Value of Containment Jar");
            configSlimeTaming = Config.Bind("Containment Jar", "Slime Taming", true, "When true, releasing SCP-999 from the containment jar will make it tamed to the person who released it. It will only follow that person unless it becomes hyper.");

            // Loading Assets
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ModAssets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), "scp999_assets"));
            if (ModAssets == null)
            {
                Logger.LogError($"Failed to load custom assets.");
                return;
            }
            LoggerInstance.LogDebug($"Got AssetBundle at: {Path.Combine(sAssemblyLocation, "scp999_assets")}");

            if (configEnableJar.Value)
            {
                Item Jar = ModAssets.LoadAsset<Item>("Assets/ModAssets/ContainmentJar/ContainmentJarItem.asset");
                if (Jar == null) { LoggerInstance.LogError("Error: Couldnt get Containment Jar from assets"); return; }
                LoggerInstance.LogDebug($"Got Containment Jar prefab");

                LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Jar.spawnPrefab);
                LethalLib.Modules.Utilities.FixMixerGroups(Jar.spawnPrefab);
                LethalLib.Modules.Items.RegisterShopItem(Jar, configJarPrice.Value);
            }

            EnemyType SCP999 = ModAssets.LoadAsset<EnemyType>("Assets/ModAssets/SCP999/SCP999Enemy.asset");
            if (SCP999 == null) { LoggerInstance.LogError("Error: Couldnt get SCP-999 from assets"); return; }
            LoggerInstance.LogDebug($"Got SCP-999 prefab");
            TerminalNode SCP999TN = ModAssets.LoadAsset<TerminalNode>("Assets/ModAssets/SCP999/Bestiary/SCP999TN.asset");
            TerminalKeyword SCP999TK = ModAssets.LoadAsset<TerminalKeyword>("Assets/ModAssets/SCP999/Bestiary/SCP999TK.asset");

            Dictionary<Levels.LevelTypes, int>? levelRarities = GetLevelRarities(config999LevelRarities.Value);
            Dictionary<string, int>? customLevelRarities = GetCustomLevelRarities(config999CustomLevelRarities.Value);

            LoggerInstance.LogDebug("Registering enemy network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(SCP999.enemyPrefab);
            LoggerInstance.LogDebug("Registering enemy...");
            LethalLib.Modules.Enemies.RegisterEnemy(SCP999, levelRarities, customLevelRarities, SCP999TN, SCP999TK);
            LoggerInstance.LogDebug("Registered enemy");

            // Finished
            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        }

        public Dictionary<Levels.LevelTypes, int>? GetLevelRarities(string levelsString)
        {
            try
            {
                Dictionary<Levels.LevelTypes, int> levelRaritiesDict = new Dictionary<Levels.LevelTypes, int>();

                if (levelsString != null && levelsString != "")
                {
                    string[] levels = levelsString.Split(',');

                    foreach (string level in levels)
                    {
                        string[] levelSplit = level.Split(':');
                        if (levelSplit.Length != 2) { continue; }
                        string levelType = levelSplit[0].Trim();
                        string levelRarity = levelSplit[1].Trim();

                        if (Enum.TryParse<Levels.LevelTypes>(levelType, out Levels.LevelTypes levelTypeEnum) && int.TryParse(levelRarity, out int levelRarityInt))
                        {
                            levelRaritiesDict.Add(levelTypeEnum, levelRarityInt);
                        }
                        else
                        {
                            LoggerInstance.LogError($"Error: Invalid level rarity: {levelType}:{levelRarity}");
                        }
                    }
                }
                return levelRaritiesDict;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error: {e}");
                return null;
            }
        }

        public Dictionary<string, int>? GetCustomLevelRarities(string levelsString)
        {
            try
            {
                Dictionary<string, int> customLevelRaritiesDict = new Dictionary<string, int>();

                if (levelsString != null)
                {
                    string[] levels = levelsString.Split(',');

                    foreach (string level in levels)
                    {
                        string[] levelSplit = level.Split(':');
                        if (levelSplit.Length != 2) { continue; }
                        string levelType = levelSplit[0].Trim();
                        string levelRarity = levelSplit[1].Trim();

                        if (int.TryParse(levelRarity, out int levelRarityInt))
                        {
                            customLevelRaritiesDict.Add(levelType, levelRarityInt);
                        }
                        else
                        {
                            LoggerInstance.LogError($"Error: Invalid level rarity: {levelType}:{levelRarity}");
                        }
                    }
                }
                return customLevelRaritiesDict;
            }
            catch (Exception e)
            {
                Logger.LogError($"Error: {e}");
                return null;
            }
        }

        public static List<SpawnableEnemyWithRarity> GetEnemies()
        {
            LoggerInstance.LogDebug("Getting enemies");
            List<SpawnableEnemyWithRarity> enemies = new List<SpawnableEnemyWithRarity>();
            enemies = GameObject.Find("Terminal")
                .GetComponentInChildren<Terminal>()
                .moonsCatalogueList
                .SelectMany(x => x.Enemies.Concat(x.DaytimeEnemies).Concat(x.OutsideEnemies))
                .Where(x => x != null && x.enemyType != null && x.enemyType.name != null)
                .GroupBy(x => x.enemyType.name, (k, v) => v.First())
                .ToList();

            LoggerInstance.LogDebug($"Enemy types: {enemies.Count}");
            return enemies;
        }

        private static void InitializeNetworkBehaviours()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            LoggerInstance.LogDebug("Finished initializing network behaviours");
        }
    }
}
