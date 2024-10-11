/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;
using static SCP999.Plugin;
using UnityEngine;
using System.Diagnostics.CodeAnalysis;

namespace SCP999.Patches
{
    [HarmonyPatch(typeof(RoundManager))]
    internal class RoundManagerPatch // FOR SPAWNING ITEMS IN SCPDUNGEON
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        private static bool isSCPDungeon = false;
        private static int currentLevelRarity;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(RoundManager.GeneratedFloorPostProcessing))]
        public static void GeneratedFloorPostProcessingPrefix(RoundManager __instance) // SCPFlow // Run by server
        {
            try
            {
                if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
                {
                    string dungeonName = __instance.dungeonGenerator.Generator.DungeonFlow.name;

                    if (dungeonName == "SCPFlow")
                    {
                        logger.LogDebug("SCPFlow detected");

                        SpawnableEnemyWithRarity scp = __instance.currentLevel.Enemies.Where(x => x.enemyType.name == "SCP999Enemy").FirstOrDefault();

                        if (scp != null)
                        {
                            currentLevelRarity = scp.rarity;
                            scp.rarity = config999SCPDungeonRarity.Value;
                            logger.LogDebug($"Rarity for SCP-999 set to {scp.rarity} from {currentLevelRarity}");

                            isSCPDungeon = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(RoundManager.DespawnPropsAtEndOfRound))]
        public static void DespawnPropsAtEndOfRoundPostfix(RoundManager __instance) // SCPFlow
        {
            try
            {
                if (isSCPDungeon)
                {
                    logger.LogDebug("Resetting rarities");
                    isSCPDungeon = false;

                    SpawnableEnemyWithRarity scp = __instance.currentLevel.Enemies.Where(x => x.enemyType.name == "SCP999Enemy").FirstOrDefault();

                    scp.rarity = currentLevelRarity;

                    logger.LogDebug($"Rarity for SCP-999 reset to {scp.rarity} from {currentLevelRarity}");
                }
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }
    }
}*/