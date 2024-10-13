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

        public void ChangeJarContents(Contents contents)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                int newValue = 0;
                if (contents == Contents.Blob)
                {
                    newValue = configJarSlimeValue.Value;
                }
                else if (contents == Contents.SCP999)
                {
                    newValue = configJar999Value.Value;
                }

                ChangeJarContentsClientRpc(contents, newValue);
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
                        RoundManager.Instance.SpawnEnemyGameObject(playerHeldBy.transform.position + new Vector3(0, 0, 3.5f), playerHeldBy.transform.rotation.y + 180, 0, spawnableEnemy.enemy);
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

                ChangeJarContents(Contents.Empty);
            }
        }

        [ClientRpc]
        private void ChangeJarContentsClientRpc(Contents contents, int _scrapValue)
        {
            mainObjectRenderer.material = ItemMaterials[(int)contents];
            itemProperties.itemIcon = ItemIcons[(int)contents];
            ScanNode.subText = "Contents: " + contents.ToString();
            scrapValue = _scrapValue;
            JarContents = contents;

            if (JarContents == Contents.Empty)
            {
                itemProperties.toolTips[0] = "";
            }
            else
            {
                itemProperties.toolTips[0] = $"Release {JarContents.ToString()} [LMB]";
            }

            if (LocalPlayer == playerHeldBy)
            {
                HUDManager.Instance.itemSlotIcons[LocalPlayer.currentItemSlot].sprite = itemProperties.itemIcon;
            }
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