﻿using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace SCP999.Patches
{
    internal class TESTING : MonoBehaviour
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;


        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {

        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            string msg = __instance.chatTextField.text;
            string[] args = msg.Split(" ");
            logger.LogDebug(msg);


        }
    }
}