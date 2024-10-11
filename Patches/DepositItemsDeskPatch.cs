/*using BepInEx.Logging;
using HarmonyLib;
using Unity.Netcode;

namespace SCP999
{
    [HarmonyPatch(typeof(DepositItemsDesk))]
    internal class DepositItemsDeskPatch
    {
        private static ManualLogSource logger = Plugin.LoggerInstance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(DepositItemsDesk.delayedAcceptanceOfItems))]
        public static void delayedAcceptanceOfItemsPostFix(GrabbableObject[] objectsOnDesk) // TODO: Test this
        {
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                foreach (GrabbableObject item in objectsOnDesk)
                {
                    if (item.GetComponent<ContainmentJarBehavior>() != null)
                    {
                        ContainmentJarBehavior jar = item.GetComponent<ContainmentJarBehavior>();
                        if (jar.JarContents == ContainmentJarBehavior.Contents.SCP999)
                        {
                            jar.NotifyPlayerClientRpc();
                        }
                    }
                }
            }
        }
    }
}*/