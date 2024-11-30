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
            try
            {
                if (IsServerOrHost)
                {
                    if (__instance.enemyType.name == "SCP999Enemy") { return; }

                    int maxHealth = __instance.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
                    if (maxHealth <= 0 || __instance.isEnemyDead) { return; }
                    logger.LogDebug($"Max health of {__instance.enemyType.name} is {maxHealth}");

                    float multiplier = 2 - (__instance.enemyHP / maxHealth);
                    float range = configEnemyDetectionRange.Value * multiplier;

                    foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                    {
                        if (Vector3.Distance(scp.transform.position, __instance.transform.position) <= range)
                        {
                            logger.LogDebug(__instance.enemyType.enemyName + " took damage");
                            //scp.targetPlayer = null;
                            //scp.targetEnemy = __instance;
                            scp.EnemyTookDamage(__instance);
                            return;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                logger.LogError(e);
                return;
            }
        }
    }
}