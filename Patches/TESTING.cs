/*using BepInEx.Logging;
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
            LocalPlayer.DamagePlayer(25);
            HUDManager.Instance.UpdateHealthUI(LocalPlayer.health);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            logger.LogDebug(msg);

            switch (args[0])
            {
                case "/hugRange":
                    SCP999AI.huggingRange = float.Parse(args[1]);
                    break;
                case "/followRange":
                    SCP999AI.followingRange = float.Parse(args[1]);
                    break;
                case "/damage":
                    LocalPlayer.DamagePlayer(int.Parse(args[1]));
                    HUDManager.Instance.UpdateHealthUI(LocalPlayer.health);
                    break;
                case "/999":
                    EnemyType scp999 = LethalLib.Modules.Enemies.spawnableEnemies.Where(x => x.enemy.name == "SCP999Enemy").FirstOrDefault().enemy;
                    RoundManager.Instance.SpawnEnemyGameObject(LocalPlayer.transform.forward, Quaternion.identity.y, 0, scp999);
                    break;
                default:
                    break;
            }
        }
    }
}*/