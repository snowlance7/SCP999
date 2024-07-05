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
    [HarmonyPatch(typeof(PlayerControllerB))]
    internal class PlayerControllerBPatch
    {
        private static ManualLogSource logger = LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        private static void DamagePlayerPostfix(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            if (StartOfRound.Instance.inShipPhase) { return; }

            PlayerControllerB player = __instance;

            foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
            {
                if (scp.currentBehaviourStateIndex == (int)SCP999AI.State.Blocking) { continue; }

                float multiplier = 2 - (player.health / 100f);
                float range = scp.playerDetectionRange * multiplier;

                if (Vector3.Distance(scp.transform.position, player.transform.position) <= range)
                {
                    scp.PlayerTookDamageServerRpc(player.actualClientId);
                    return;
                }
            }

        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PlayerControllerB.DamagePlayer))]
        private static bool DamagePlayerPrefix(PlayerControllerB __instance, CauseOfDeath causeOfDeath)
        {
            PlayerControllerB player = __instance;

            if (causeOfDeath == CauseOfDeath.Gunshots)
            {
                foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                {
                    if (scp.targetPlayer != null)
                    {
                        if (player == scp.targetPlayer && scp.currentBehaviourStateIndex == (int)SCP999AI.State.Blocking)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }
    }
}