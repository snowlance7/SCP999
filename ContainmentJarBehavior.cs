using BepInEx.Logging;
using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;
using HarmonyLib;
using static SCP999.Plugin;
using Unity.Netcode.Components;

namespace SCP999
{
    internal class ContainmentJarBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public Sprite[] ItemIcons = null!;
        public Material[] ItemMaterials = null!;
        public ScanNodeProperties ScanNode = null!;
#pragma warning restore 0649

        internal enum Contents
        {
            Empty,
            SCP999,
            Blob
        }

        public Contents JarContents = Contents.Empty;
        public PlayerControllerB? lastPlayerHeldBy;

        public override void Start()
        {
            base.Start();
            fallTime = 0f;
        }

        public override void GrabItem() // Synced
        {
            base.GrabItem();
            lastPlayerHeldBy = playerHeldBy;
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
            mainObjectRenderer.material = ItemMaterials[(int)contents];
            itemProperties.itemIcon = ItemIcons[(int)contents];
            JarContents = contents;

            if (JarContents == Contents.Empty)
            {
                scrapValue = 0;
                itemProperties.isScrap = false;
                itemProperties.toolTips[0] = "";
            }
            else
            {
                itemProperties.isScrap = true;
                itemProperties.toolTips[0] = $"Release {JarContents.ToString()} [LMB]";

                if (contents == Contents.Blob)
                {
                    SetScrapValue(configJarSlimeValue.Value);
                }
                else if (contents == Contents.SCP999)
                {
                    SetScrapValue(configJar999Value.Value);
                }
            }

            ScanNode.subText = "Contents: " + contents.ToString();

            if (playerHeldBy != null && LocalPlayer == playerHeldBy)
            {
                HUDManager.Instance.itemSlotIcons[LocalPlayer.currentItemSlot].sprite = itemProperties.itemIcon;
            }
        }

        // RPCs
        [ServerRpc(RequireOwnership = false)]
        private void OpenJarServerRpc()
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (JarContents == Contents.SCP999)
                {
                    Enemies.SpawnableEnemy spawnableEnemy = LethalLib.Modules.Enemies.spawnableEnemies.Where(x => x.enemy.name == "SCP999Enemy").FirstOrDefault();
                    if (spawnableEnemy != null)
                    {
                        NetworkObject scpRef = RoundManager.Instance.SpawnEnemyGameObject(playerHeldBy.transform.position + new Vector3(0, 0, 3.5f), playerHeldBy.transform.rotation.y + 180, 0, spawnableEnemy.enemy);
                        if (configSlimeTaming.Value)
                        {
                            scpRef.GetComponent<SCP999AI>().SetTamed(playerHeldBy);
                        }
                    }
                }
                else if (JarContents == Contents.Blob)
                {
                    SpawnableEnemyWithRarity spawnableEnemy = GetEnemies().Where(x => x.enemyType.name == "Blob").FirstOrDefault(); // TODO: Test this
                    if (spawnableEnemy != null)
                    {
                        RoundManager.Instance.SpawnEnemyGameObject(playerHeldBy.transform.position + new Vector3(0, 0, 3.5f), playerHeldBy.transform.rotation.y + 180, 0, spawnableEnemy.enemyType);
                    }
                }

                ChangeJarContentsClientRpc(Contents.Empty);
            }
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
            if (lastPlayerHeldBy != null && lastPlayerHeldBy == LocalPlayer)
            {
                HUDManager.Instance.DisplayTip("You monster!", "You are a horrible person", isWarning: true);
            }
        }
    }
}