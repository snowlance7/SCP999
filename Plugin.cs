using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SCP999
{
    [BepInPlugin(modGUID, modName, modVersion)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    public class Plugin : BaseUnityPlugin
    {
        private const string modGUID = "ProjectSCP.SCP999";
        private const string modName = "SCP999";
        private const string modVersion = "1.0.0";

        public static Plugin PluginInstance;
        public static ManualLogSource LoggerInstance;
        private readonly Harmony harmony = new Harmony(modGUID);
        public static PlayerControllerB localPlayer { get { return StartOfRound.Instance.localPlayerController; } }

        public static AssetBundle? ModAssets;

        //public static AudioClip? WarningSoundShortsfx;

        // SCP-999 Rarity Configs
        public static ConfigEntry<int> configExperimentationLevelRarity;
        public static ConfigEntry<int> configAssuranceLevelRarity;
        public static ConfigEntry<int> configVowLevelRarity;
        public static ConfigEntry<int> configOffenseLevelRarity;
        public static ConfigEntry<int> configMarchLevelRarity;
        public static ConfigEntry<int> configRendLevelRarity;
        public static ConfigEntry<int> configDineLevelRarity;
        public static ConfigEntry<int> configTitanLevelRarity;
        public static ConfigEntry<int> configModdedLevelRarity;
        public static ConfigEntry<int> configOtherLevelRarity;

        // SCP-999 General Configs
        public static ConfigEntry<int> configPlayerHealAmount;
        public static ConfigEntry<int> configEnemyHealAmount;

        public static ConfigEntry<float> configPlayerDetectionRange;
        public static ConfigEntry<float> configEnemyDetectionRange;

        public static ConfigEntry<float> configFollowRange;

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
            configExperimentationLevelRarity = Config.Bind("Rarity", "ExperimentationLevelRarity", 50, "Experimentation Level Rarity");
            configAssuranceLevelRarity = Config.Bind("Rarity", "AssuranceLevelRarity", 50, "Assurance Level Rarity");
            configVowLevelRarity = Config.Bind("Rarity", "VowLevelRarity", 50, "Vow Level Rarity");
            configOffenseLevelRarity = Config.Bind("Rarity", "OffenseLevelRarity", 30, "Offense Level Rarity");
            configMarchLevelRarity = Config.Bind("Rarity", "MarchLevelRarity", 30, "March Level Rarity");
            configRendLevelRarity = Config.Bind("Rarity", "RendLevelRarity", 10, "Rend Level Rarity");
            configDineLevelRarity = Config.Bind("Rarity", "DineLevelRarity", 10, "Dine Level Rarity");
            configTitanLevelRarity = Config.Bind("Rarity", "TitanLevelRarity", 20, "Titan Level Rarity");
            configModdedLevelRarity = Config.Bind("Rarity", "ModdedLevelRarity", 10, "Modded Level Rarity");
            configOtherLevelRarity = Config.Bind("Rarity", "OtherLevelRarity", 10, "Other Level Rarity");

            // General
            configPlayerHealAmount = Config.Bind("General", "Player Heal Amount", 10, "How much SCP-999 heals the player per second");
            configEnemyHealAmount = Config.Bind("General", "Enemy Heal Amount", 1, "How much SCP-999 heals the enemy per second");

            configPlayerDetectionRange = Config.Bind("General", "Player Detection Range", 20f, "How far SCP-999 can detect you");
            configEnemyDetectionRange = Config.Bind("General", "Enemy Detection Range", 10f, "How far SCP-999 can detect enemies");

            configFollowRange = Config.Bind("General", "Follow Range", 7f, "How far SCP-999 can follow you or other enemies");
        

            // Loading Assets
            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ModAssets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), "scp999_assets"));
            if (ModAssets == null)
            {
                Logger.LogError($"Failed to load custom assets.");
                return;
            }
            LoggerInstance.LogDebug($"Got AssetBundle at: {Path.Combine(sAssemblyLocation, "scp999_assets")}");

            // Getting Audio
            //WarningSoundShortsfx = ModAssets.LoadAsset<AudioClip>("Assets/ModAssets/Tickler/Audio/gurgle.mp3");
            //LoggerInstance.LogDebug($"Got sounds from assets");

            EnemyType Tickler = ModAssets.LoadAsset<EnemyType>("Assets/ModAssets/SCP999/TickleMonster.asset");
            if (Tickler == null) { LoggerInstance.LogError("Error: Couldnt get SCP-999 from assets"); return; }
            LoggerInstance.LogDebug($"Got SCP-999 prefab");
            TerminalNode TicklerTN = ModAssets.LoadAsset<TerminalNode>("Assets/ModAssets/SCP999/Bestiary/TickleMonsterTN.asset");
            TerminalKeyword TicklerTK = ModAssets.LoadAsset<TerminalKeyword>("Assets/ModAssets/SCP999/Bestiary/TickleMonsterTK.asset");

            LoggerInstance.LogDebug("Setting rarities");
            var SCP999LevelRarities = new Dictionary<Levels.LevelTypes, int> {
                {Levels.LevelTypes.ExperimentationLevel, configExperimentationLevelRarity.Value},
                {Levels.LevelTypes.AssuranceLevel, configAssuranceLevelRarity.Value},
                {Levels.LevelTypes.VowLevel, configVowLevelRarity.Value},
                {Levels.LevelTypes.OffenseLevel, configOffenseLevelRarity.Value},
                {Levels.LevelTypes.MarchLevel, configMarchLevelRarity.Value},
                {Levels.LevelTypes.RendLevel, configRendLevelRarity.Value},
                {Levels.LevelTypes.DineLevel, configDineLevelRarity.Value},
                {Levels.LevelTypes.TitanLevel, configTitanLevelRarity.Value},
                {Levels.LevelTypes.All, configOtherLevelRarity.Value},
                {Levels.LevelTypes.Modded, configModdedLevelRarity.Value},
                };

            LoggerInstance.LogDebug("Registering enemy network prefab...");
            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(Tickler.enemyPrefab);
            LoggerInstance.LogDebug("Registering enemy...");
            Enemies.RegisterEnemy(Tickler, SCP999LevelRarities, null, TicklerTN, TicklerTK);

            // Finished
            Logger.LogInfo($"{modGUID} v{modVersion} has loaded!");
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
