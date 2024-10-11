/*using System;
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
    [HarmonyPatch(typeof(Turret))]
    internal class TurretPatch
    {
        private static ManualLogSource logger = LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(Turret.SwitchTurretMode))]
        private static void SwitchTurretModePostfix(Turret __instance)
        {
            try
            {
                if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
                {
                    if (__instance.turretMode == TurretMode.Charging)
                    {
                        PlayerControllerB player = __instance.targetPlayerWithRotation;

                        foreach (var scp in RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>())
                        {
                            if (scp.targetPlayer != null && scp.targetPlayer == player)
                            {
                                if (scp.currentBehaviourStateIndex == (int)SCP999AI.State.Following)
                                {
                                    scp.BlockTurretFireServerRpc(__instance.NetworkObject);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e);
                return;
            }
        }
    }
}*/