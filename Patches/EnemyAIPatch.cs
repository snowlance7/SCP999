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

        [HarmonyPostfix]
        [HarmonyPatch(nameof(EnemyAI.HitEnemy))]
        private static void HitEnemyPostfix(EnemyAI __instance)
        {
            if (__instance.enemyType.enemyName == "SCP-999") { return; } // TODO: Get this working

            int maxHealth = __instance.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
            float multiplier = 2 - (__instance.enemyHP / maxHealth);

            foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
            {
                logger.LogDebug("Enemy took damage, hp: " + __instance.enemyHP + "/" + maxHealth); // TODO: Not working, sometimes returns 1/1

                float range = scp.enemyDetectionRange * multiplier;

                if (Vector3.Distance(scp.transform.position, __instance.transform.position) <= range)
                {
                    scp.EnemyTookDamageServerRpc(__instance.thisEnemyIndex);
                    return;
                }
            }
        }
    }
}