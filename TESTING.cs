using HarmonyLib;
using System.Linq;
using UnityEngine;
using static SCP999.Plugin;
using SnowyLib;

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
        [HarmonyPostfix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.PingScan_performed))]
        public static void PingScan_performedPostFix()
        {
            try
            {
                if (!Utils.testing) { return; }
            }
            catch
            {
                return;
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(HUDManager), nameof(HUDManager.SubmitChat_performed))]
        public static void SubmitChat_performedPrefix(HUDManager __instance)
        {
            try
            {
                if (!Utils.testing || !__instance.IsServer) { return; }
                string msg = __instance.chatTextField.text;
                string[] args = msg.Split(" ");
                logger.LogDebug(msg);

                switch (args[0])
                {
                    default:
                        break;
                }
            }
            catch
            {
                return;
            }
        }
    }
}