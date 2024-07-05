using BepInEx.Logging;
using HarmonyLib;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static SCP999.Plugin;

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
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (__instance.enemyType.enemyName == "SCP-999") { return; } // TODO: Get this working

                int maxHealth = __instance.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

                float multiplier = 2 - (__instance.enemyHP / maxHealth);
                float range = configEnemyDetectionRange.Value * multiplier;

                foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                {
                    logger.LogDebug(__instance.enemyType.enemyName + " took damage, hp: " + __instance.enemyHP + "/" + maxHealth);

                    if (Vector3.Distance(scp.transform.position, __instance.transform.position) <= range)
                    {
                        scp.targetPlayer = null;
                        scp.targetEnemy = __instance;
                        scp.EnemyTookDamageServerRpc();
                        return;
                    }
                }
            }
        }
    }
}