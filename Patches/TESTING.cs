using BepInEx.Logging;
using HarmonyLib;
using LethalLib.Extras;
using LethalLib.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using static SCP999.Plugin;
using LethalLib;
using static LethalLib.Modules.Enemies;
using Unity.Netcode;
using GameNetcodeStuff;
using static UnityEngine.ParticleSystem.PlaybackState;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Burst.Intrinsics;
using System.Collections;
using static UnityEngine.VFX.VisualEffectControlTrackController;
using UnityEngine.InputSystem.Utilities;

namespace SCP999.Patches
{
    [HarmonyPatch]
    internal class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        private static PlayerControllerB localPlayer { get { return StartOfRound.Instance.localPlayerController; } }

        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
        public static void PingScan_performedPostFix()
        {
            //List<SCP999AI> scp = RoundManager.Instance.SpawnedEnemies.OfType<SCP999AI>().ToList(); // TODO: This works
            //logger.LogDebug("Found: " + scp.Count);

            //localPlayer.DamagePlayer(10);

            //logger.LogDebug(localPlayer.currentlyHeldObjectServer.itemProperties.itemName);
        }
    }
}
// test if this works
// test if healing works