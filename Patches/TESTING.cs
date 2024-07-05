using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace SCP999.Patches
{
    [HarmonyPatch]
    internal class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        private static PlayerControllerB localPlayer { get { return StartOfRound.Instance.localPlayerController; } }

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {
            //RoundManager.Instance.RefreshEnemiesList();


            //localPlayer.DamagePlayer(50);
            //HUDManager.Instance.UpdateHealthUI(localPlayer.health);

            /*foreach (var enemy in Resources.FindObjectsOfTypeAll<EnemyAI>())
            {
                //var type = enemy.enemyType.enemyPrefab.GetComponent<NavMeshAgent>().obstacleAvoidanceType;
                //logger.LogDebug(enemy.enemyType.enemyName + " - " + type.ToString());
                var priority = enemy.enemyType.enemyPrefab.GetComponent<NavMeshAgent>().avoidancePriority;
                logger.LogDebug(enemy.enemyType.enemyName + " - " + priority.ToString());
            }

            SCP999AI scp = RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>().FirstOrDefault();
            if (scp != null)
            {
                logger.LogDebug($"\n TargetPlayer: { scp.targetPlayer } || TargetEnemy: { scp.targetEnemy }");
            }

            RoundManager.Instance.RefreshEnemiesList();*/

            //List<SCP999AI> scp = RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>().ToList(); // TODO: This works
            //logger.LogDebug("Found: " + scp.Count);

            //localPlayer.DamagePlayer(10);

            //logger.LogDebug(localPlayer.currentlyHeldObjectServer.itemProperties.itemName);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            //string msg = __instance.chatTextField.text;
            //logger.LogDebug(msg);
        }

        /*public static List<SpawnableEnemyWithRarity> GetEnemies()
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
        }*/
    }
}