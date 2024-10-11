/*using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

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
                if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
                {
                    foreach (var item in UnityEngine.Object.FindObjectsOfType<ContainmentJarBehavior>())
                    {
                        if (Vector3.Distance(__instance.transform.position, item.transform.position) < 2f) // TODO: Test this
                        {
                            item.ChangeJarContents(ContainmentJarBehavior.Contents.Blob);
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
}*/