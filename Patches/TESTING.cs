using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
using static SCP999.Plugin;

namespace SCP999.Patches
{
    [HarmonyPatch]
    internal class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            if (localPlayer.playerSteamId != 76561198253760639) { return; }
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            logIfDebug(msg);

            switch (args[0])
            {
                case "/spawn999":
                    SpawnableEnemyWithRarity enemy = RoundManager.Instance.currentLevel.Enemies.Where(x => x.enemyType.name == "SCP999Enemy").FirstOrDefault();
                    int index = RoundManager.Instance.currentLevel.Enemies.IndexOf(enemy);
                    RoundManager.Instance.SpawnEnemyOnServer(localPlayer.transform.position + localPlayer.transform.forward * 2, Quaternion.identity.y, index);
                    break;
                case "/despawn999":
                    SCP999AI scp999 = RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>().FirstOrDefault();
                    if (scp999 == null) { return; }
                    RoundManager.Instance.DespawnEnemyOnServer(scp999.NetworkObject);
                    break;
                case "/tame":
                    SCP999AI scp = RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>().FirstOrDefault();
                    if (scp == null) { return; }
                    scp.SetTamedPlayerServerRpc(localPlayer.actualClientId);
                    break;
                case "/refresh":
                    if (IsServerOrHost)
                    {
                        RoundManager.Instance.RefreshEnemiesList();
                        HoarderBugAI.RefreshGrabbableObjectsInMapList();
                    }
                    break;
                default:
                    break;
            }
        }
    }
}