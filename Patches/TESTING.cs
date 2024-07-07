/*using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace SCP999.Patches
{
    [HarmonyPatch]
    internal class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        private static PlayerControllerB localPlayer { get { return StartOfRound.Instance.localPlayerController; } }

        static bool invis = false;
        static bool noSpawning = false;
        static bool godMode = false;

        static List<string> commands = new List<string> { "/spawn", "/spawnitem", "/invis", "/teleport", "/nospawning", "/money", "/godmode", "/damage", "/refresh", "/lights", "/enemies", "/players", "/items", "/turret", "/followplayer", "/followenemy" };

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {
            foreach (var level in StartOfRound.Instance.levels)
            {
                logger.LogDebug(level.PlanetName);
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            logger.LogDebug(msg);


            // Comment these out
            if (args[0] == "/help")
            {
                foreach (var command in commands)
                {
                    logger.LogDebug(command);
                }
            }
            if (args[0] == "/spawn")
            {
                Vector3 pos = localPlayer.transform.forward * 2f + localPlayer.transform.position;
                int index = RoundManager.Instance.currentLevel.Enemies.FindIndex(x => x.enemyType.enemyName == args[1]);
                RoundManager.Instance.SpawnEnemyOnServer(pos, UnityEngine.Random.Range(0f, 360f), index);
                HUDManager.Instance.DisplayTip("Testing", $"Spawned enemy: {args[1]}");
                RoundManager.Instance.RefreshEnemiesList();
            }
            if (args[0] == "/spawnitem")
            {
                Vector3 pos = localPlayer.transform.forward * 2f + localPlayer.transform.position;
                Item item = StartOfRound.Instance.allItemsList.itemsList.Where(x => x.itemName == args[1]).FirstOrDefault();
                if (item == null) { return; }

                GameObject obj = UnityEngine.Object.Instantiate(item.spawnPrefab, pos, Quaternion.identity, StartOfRound.Instance.propsContainer);
                obj.GetComponent<NetworkObject>().Spawn();
                HUDManager.Instance.DisplayTip("Testing", $"Spawned item: {item.itemName}");
            }
            if (args[0] == "/invis")
            {
                if (args[1] == "true")
                {
                    invis = true;
                }
                else if (args[1] == "false")
                {
                    invis = false;
                }
                HUDManager.Instance.DisplayTip("Testing", $"Invis set to {invis}");
            }
            if (args[0] == "/teleport")
            {
                if (args[1] == "main")
                {
                    Vector3 pos = RoundManager.FindMainEntrancePosition(true, true);
                    localPlayer.TeleportPlayer(pos);
                }
            }
            if (args[0] == "/nospawning")
            {
                if (args[1] == "true")
                {
                    noSpawning = true;
                }
                else if (args[1] == "false")
                {
                    noSpawning = false;
                }
                HUDManager.Instance.DisplayTip("Testing", $"Enemy spawning set to {!noSpawning}");
            }
            if (args[0] == "/money")
            {
                int amount = Convert.ToInt32(args[1]);
                FindObjectOfType<Terminal>().groupCredits += amount;
                HUDManager.Instance.DisplayTip("Testing", $"Credits added: {amount}");
            }
            if (args[0] == "/godmode")
            {
                if (args[1] == "true")
                {
                    godMode = true;
                }
                else if (args[1] == "false")
                {
                    godMode = false;
                }
                HUDManager.Instance.DisplayTip("Testing", $"Godmode set to {godMode}");
            }
            if (args[0] == "/damage")
            {
                int amount = Convert.ToInt32(args[1]);
                localPlayer.DamagePlayer(amount);
                HUDManager.Instance.DisplayTip("Testing", $"Damaged self: {amount}");
            }
            if (args[0] == "/refresh")
            {
                RoundManager.Instance.RefreshEnemiesList();
                RoundManager.Instance.RefreshEnemyVents();
                RoundManager.Instance.RefreshLightsList();
            }
            if (args[0] == "/lights")
            {
                bool value = Convert.ToBoolean(args[1]);
                RoundManager.Instance.TurnOnAllLights(value);
                HUDManager.Instance.DisplayTip("Testing", $"Lights set to {value}");
            }
            if (msg == "/enemies")
            {
                logger.LogDebug("ENEMIES:");
                foreach (var enemy in GetEnemies())
                {
                    logger.LogDebug(enemy.enemyType.enemyName);
                }
            }
            if (msg == "/players")
            {
                logger.LogDebug("PLAYERS:");
                foreach (var player in StartOfRound.Instance.allPlayerScripts)
                {
                    logger.LogDebug(player.playerUsername);
                }
            }
            if (msg == "/items")
            {
                logger.LogDebug("ITEMS:");
                foreach (var item in StartOfRound.Instance.allItemsList.itemsList)
                {
                    logger.LogDebug(item.itemName);
                }
            }
            if (msg == "/turret")
            {
                Vector3 pos = localPlayer.transform.forward * 2f + localPlayer.transform.position;
                GameObject turretObj = UnityEngine.Object.Instantiate(StartOfRound.Instance.levels.Where(x => x.PlanetName == "41 Experimentation").First().spawnableMapObjects.Where(x => x.prefabToSpawn.name == "TurretContainer").First().prefabToSpawn, pos, Quaternion.identity, RoundManager.Instance.mapPropsContainer.transform);
                turretObj.GetComponent<NetworkObject>().Spawn(true);
                HUDManager.Instance.DisplayTip("Testing", "Spawned Turret");
            }
            if (args[0] == "/followplayer")
            {
                foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                {
                    if (args[1] == "true")
                    {
                        scp.followPlayer = true;
                    }
                    else if (args[1] == "false")
                    {
                        scp.followPlayer = false;
                    }
                }
                HUDManager.Instance.DisplayTip("Testing", $"Follow player set to {args[1]}");
            }
            if (args[0] == "/followenemy")
            {
                foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                {
                    if (args[1] == "true")
                    {
                        scp.followEnemy = true;
                    }
                    else if (args[1] == "false")
                    {
                        scp.followEnemy = false;
                    }
                }
                HUDManager.Instance.DisplayTip("Testing", $"Follow enemy set to {args[1]}");
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayerIsTargetable))]
        public static bool PlayerIsTargetablePrefix(ref bool __result)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (invis)
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnEnemiesOutside))]
        public static bool SpawnEnemiesOutsidePrefix()
        {

            if (noSpawning)
            {
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnInsideEnemiesFromVentsIfReady))]
        public static bool SpawnInsideEnemiesFromVentsIfReadyPrefix()
        {

            if (noSpawning)
            {
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        public static bool DamagePlayerPrefix(int damageNumber)
        {
            if (godMode)
            {
                HUDManager.Instance.DisplayTip("Testing", $"Godmode prevented {damageNumber} damage to player");
                return false;
            }
            return true;
        }

        public static List<SpawnableEnemyWithRarity> GetEnemies()
        {
            logger.LogDebug("Getting enemies");
            List<SpawnableEnemyWithRarity> enemies = new List<SpawnableEnemyWithRarity>();
            enemies = GameObject.Find("Terminal")
                .GetComponentInChildren<Terminal>()
                .moonsCatalogueList
                .SelectMany(x => x.Enemies.Concat(x.DaytimeEnemies).Concat(x.OutsideEnemies))
                .Where(x => x != null && x.enemyType != null && x.enemyType.name != null)
                .GroupBy(x => x.enemyType.name, (k, v) => v.First())
                .ToList();

            logger.LogDebug($"Enemy types: {enemies.Count}");
            return enemies;
        }
    }
}*/