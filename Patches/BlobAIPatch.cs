using BepInEx.Logging;
using HarmonyLib;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static SCP999.Plugin;

namespace SCP999.Patches
{
    [HarmonyPatch(typeof(BlobAI))]
    internal class BlobAIPatch
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BlobAI.DoAIInterval))]
        public static void DoAIIntervalPostfix(BlobAI __instance) // TODO: Test this
        {
            try
            {
                if (IsServerOrHost)
                {
                    foreach (var item in HoarderBugAI.grabbableObjectsInMap.OfType<ContainmentJarBehavior>())
                    {
                        if (Vector3.Distance(__instance.transform.position, item.transform.position) < 2f)
                        {
                            item.ChangeJarContentsClientRpc(ContainmentJarBehavior.Contents.Blob);
                            RoundManager.Instance.DespawnEnemyOnServer(__instance.NetworkObject);
                            return;
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }
    }
}