using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using static SCP999.Plugin;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Collections;
using SCP999;
using Unity.Services.Authentication;

namespace SCP999.Patches
{
    [HarmonyPatch(typeof(EnemyAI))]
    internal class EnemyAIPatch
    {
        private static ManualLogSource logger = LoggerInstance;

        private static PlayerControllerB localPlayer { get { return StartOfRound.Instance.localPlayerController; } }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnemyAI.HitEnemy))]
        private static void HitEnemyPostfix(EnemyAI __instance)
        {
            //List<SCP999AI> scp = RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>().ToList();
            foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
            {
                scp.EnemyTookDamageServerRpc(__instance.thisEnemyIndex);
            }

        }
    }
}