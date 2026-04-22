using Dusk;
using GameNetcodeStuff;
using ItemSCPs;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using static SCP999.Plugin;

namespace SCP999
{
    internal class ContainmentJarBehavior : PhysicsProp
    {
        public Sprite[] ItemIcons = null!;
        public Material[] ItemMaterials = null!;
        public ScanNodeProperties ScanNode = null!;
        public MeshRenderer renderer = null!;

        int jar999Value = ContentHandler<SCP999ContentHandler>.Instance.ContainmentJar!.GetConfig<int>("SCP-999 Value").Value; // 1
        int jarSlimeValue = ContentHandler<SCP999ContentHandler>.Instance.ContainmentJar!.GetConfig<int>("Slime Value").Value; // 50
        bool slimeTaming = ContentHandler<SCP999ContentHandler>.Instance.ContainmentJar!.GetConfig<bool>("Slime Taming").Value; // true

        internal enum Contents
        {
            Empty,
            SCP999,
            Blob
        }

        public Contents JarContents = Contents.Empty;
        public PlayerControllerB? lastPlayerHeldBy;

        public override void Update()
        {
            base.Update();

            if (playerHeldBy != null)
            {
                lastPlayerHeldBy = playerHeldBy;

                if (playerHeldBy == localPlayer)
                {
                    int slot = localPlayer.ItemSlots.IndexOf(this);
                    HUDManager.Instance.itemSlotIcons[slot].sprite = ItemIcons[(int)JarContents];
                }
            }
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            if (buttonDown)
            {
                if (JarContents != Contents.Empty && !StartOfRound.Instance.inShipPhase)
                {
                    OpenJarServerRpc();
                }
            }
        }

        public override int GetItemDataToSave()
        {
            logger.LogDebug("GetItemDataToSave: " + JarContents);
            return (int)JarContents;
        }

        public override void LoadItemSaveData(int saveData)
        {
            logger.LogDebug("LoadItemSaveData: " + (Contents)saveData);
            fallTime = 0f;
            ChangeJarContentsOnLocalClient((Contents)saveData);
        }

        public void ChangeJarContentsOnLocalClient(Contents contents)
        {
            logger.LogDebug("ChangeJarContentsOnLocalClient: " + contents);
            renderer.material = ItemMaterials[(int)contents];
            JarContents = contents;

            switch (JarContents)
            {
                case Contents.Empty:
                    SetScrapValue(0);
                    break;
                case Contents.SCP999:
                    SetScrapValue(jar999Value);
                    break;
                case Contents.Blob:
                    SetScrapValue(jarSlimeValue);
                    break;
                default:
                    break;
            }

            ScanNode.subText = "Contents: " + contents.ToString();
        }

        public override void SetControlTipsForItem()
        {
            string[] toolTips = JarContents == Contents.Empty ? [] : [$"Release {JarContents.ToString()} [LMB]"];
            HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
        }

        // RPCs
        [ServerRpc(RequireOwnership = false)]
        private void OpenJarServerRpc()
        {
            if (!IsServer) { return; }

            if (JarContents == Contents.SCP999)
            {
                SCP999AI scp999 = (SCP999AI)Utils.SpawnEnemy(SCP999Keys.SCP999, playerHeldBy.transform.position + new Vector3(0, 0, 3.5f))!;
                if (slimeTaming)
                    scp999.SetTamed(playerHeldBy);
            }
            else if (JarContents == Contents.Blob)
            {
                Utils.SpawnEnemy(Dawn.EnemyKeys.Blob, playerHeldBy.transform.position + new Vector3(0, 0, 3.5f));
            }

            ChangeJarContentsClientRpc(Contents.Empty);
        }

        [ClientRpc]
        public void ChangeJarContentsClientRpc(Contents contents)
        {
            logger.LogDebug("ChangeJarContentsClientRpc: " + contents);
            ChangeJarContentsOnLocalClient(contents);
        }

        [ClientRpc]
        public void NotifyPlayerClientRpc()
        {
            if (lastPlayerHeldBy != null && lastPlayerHeldBy == localPlayer)
            {
                HUDManager.Instance.DisplayTip("You monster!", "You are a horrible person", isWarning: true);
            }
        }
    }
}