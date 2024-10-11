/*using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System.Linq;
using static SCP999.Plugin;

namespace SCP999.Patches
{
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PlayerControllerBPatch
    {
        private static ManualLogSource logger = LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        private static void DamagePlayerPostfix(PlayerControllerB __instance)
        {
            try
            {
                if (StartOfRound.Instance.inShipPhase) { return; }

                foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                {
                    if (scp.currentBehaviourStateIndex == (int)SCP999AI.State.Blocking) { continue; }

                    scp.PlayerTookDamageServerRpc(__instance.actualClientId);
                    return;
                }
            }
            catch (System.Exception e)
            {
                logger.LogError(e);
                return;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        private static bool DamagePlayerPrefix(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            try
            {
                if (causeOfDeath == CauseOfDeath.Gunshots)
                {
                    foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                    {
                        if (scp.targetPlayer != null)
                        {
                            if (__instance == scp.targetPlayer && scp.currentBehaviourStateIndex == (int)SCP999AI.State.Blocking)
                            {
                                return false;
                            }
                        }
                    }
                }
                return true;
            }
            catch (System.Exception e)
            {
                logger.LogError(e);
                return true;
            }
        }
    }
}*/