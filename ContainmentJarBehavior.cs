using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;
using static SCP999.ContainmentJarBehavior;
using static SCP999.Plugin;

namespace SCP999
{
    internal class ContainmentJarBehavior : PhysicsProp
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Sprite[] ItemIcons;
        public Material[] ItemMaterials;
        public ScanNodeProperties ScanNode;
        public MeshRenderer renderer;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

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
            FallToGround(false, true);
        }

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
            logIfDebug("GetItemDataToSave: " + JarContents);
            return (int)JarContents;
        }

        public override void LoadItemSaveData(int saveData)
        {
            logIfDebug("LoadItemSaveData: " + (Contents)saveData);
            fallTime = 0f;
            ChangeJarContentsOnLocalClient((Contents)saveData);
        }

        public void ChangeJarContentsOnLocalClient(Contents contents)
        {
            logIfDebug("ChangeJarContentsOnLocalClient: " + contents);
            renderer.material = ItemMaterials[(int)contents];
            JarContents = contents;

            switch (JarContents)
            {
                case Contents.Empty:
                    SetScrapValue(0);
                    break;
                case Contents.SCP999:
                    SetScrapValue(configJar999Value.Value);
                    break;
                case Contents.Blob:
                    SetScrapValue(configJarSlimeValue.Value);
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
            if (IsServerOrHost)
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
            logIfDebug("ChangeJarContentsClientRpc: " + contents);
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