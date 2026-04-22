using HarmonyLib;
using System.Linq;
using UnityEngine;
using static SCP999.Plugin;

/* bodyparts
 * 0 head
 * 1 right arm
 * 2 left arm
 * 3 right leg
 * 4 left leg
 * 5 chest
 * 6 feet
 * 7 right hip
 * 8 crotch
 * 9 left shoulder
 * 10 right shoulder */

namespace SCP999
{
    [HarmonyPatch]
    public class TESTING : MonoBehaviour
    {
        public static bool localPlayerImmune = false;
        public static string currentAnim = "";

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {
            if (!Utils.isBeta) { return; }
            if (!Utils.testing) { return; }

            logger.LogDebug("PingScanTestPerformed");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            if (localPlayer.playerSteamId != 76561198253760639) { return; }
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            logger.LogDebug(msg);

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