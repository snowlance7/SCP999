﻿using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using static SCP999.Plugin;

namespace SCP999
{
    // Movement speed 5f
    public class SCP999AI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public Transform turnCompass = null!;
        public List<AudioClip> hitSFXList = null!;
        public List<AudioClip> hugSFXList = null!;
        public List<AudioClip> hurtSFXList = null!;
        public List<AudioClip> roamSFXList = null!;
        public NetworkAnimator networkAnimator = null!;
#pragma warning restore 0649

        EnemyAI? targetEnemy;
        Turret? blockedTurret;

        float timeSinceHealing;
        float healingBuffTime;
        float hyperTime;
        float timeSinceBlockSFX;
        float timeSinceHugSFX;

        int playerHealAmount;
        int enemyHealAmount;

        float playerDetectionRange;
        float enemyDetectionRange;
        float rangeMultiplier = 1f;
        float followingRange;
        float huggingRange = 5f;

        bool walking = false;
        bool hugging = false;
        bool dancing = false;

        int maxCandy;
        int candyEaten;
        bool gettingInJar = false;
        bool blockingAnimation = false;

        bool followPlayer = true;
        bool followEnemy = true;


        public enum State
        {
            Roaming,
            Following,
            Blocking,
            Healing,
            Hyper
        }

        public override void Start()
        {
            base.Start();
            logger.LogDebug("SCP-999 Spawned");
            //debugEnemyAI = true;

            playerHealAmount = configPlayerHealAmount.Value;
            enemyHealAmount = configEnemyHealAmount.Value;
            playerDetectionRange = configPlayerDetectionRange.Value;
            enemyDetectionRange = configEnemyDetectionRange.Value;
            followingRange = configFollowRange.Value;
            maxCandy = configMaxCandy.Value;

            /*if (transform.localScale.y != config999Size.Value)
            {
                ChangeSizeClientRpc(config999Size.Value);
            }*/

            SetOutsideOrInside();

            currentBehaviourStateIndex = (int)State.Roaming;
            //RoundManager.Instance.SpawnedEnemies.Add(this);

            if (IsServerOrHost)
            {
                networkAnimator.SetTrigger("startWalking");
                //DoAnimationClientRpc("startWalking");
                StartSearch(transform.position);
            }
            logger.LogDebug("Finished start");
        }

        public override void Update()
        {
            base.Update();

            timeSinceHealing += Time.deltaTime;
            timeSinceBlockSFX += Time.deltaTime;
            timeSinceHugSFX += Time.deltaTime;

            if (healingBuffTime > 0f)
            {
                healingBuffTime -= Time.deltaTime;

                if (healingBuffTime <= 0.5f)
                {
                    candyEaten = 0;
                }
            }

            if (hyperTime > 0f)
            {
                hyperTime -= Time.deltaTime;
            }

            if (targetPlayer != null && currentBehaviourStateIndex == (int)State.Blocking)
            {
                turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (currentBehaviourStateIndex != (int)State.Hyper && currentBehaviourStateIndex != (int)State.Healing)
                {
                    if (agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f)
                    {
                        if (walking)
                        {
                            walking = false;
                            networkAnimator.SetTrigger("stopWalking");
                            //DoAnimationClientRpc("stopWalking");
                        }
                    }
                    else
                    {
                        if (!walking)
                        {
                            walking = true;
                            networkAnimator.SetTrigger("startWalking");
                            //DoAnimationClientRpc("startWalking");
                        }
                    }
                }
            }

            /*if (currentBehaviourStateIndex == (int)State.Blocking)
            {
                timeSinceBlockSFX += Time.deltaTime;
            }
            if (currentBehaviourStateIndex == (int)State.Healing)
            {
                timeSinceHugSFX += Time.deltaTime;
            }*/
            //logger.LogDebug("Finished update");
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || gettingInJar)
            {
                return;
            };

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    agent.speed = 5f;
                    agent.stoppingDistance = 0f;
                    dancing = false;
                    if ((TargetClosestPlayer(1.5f, true) && followPlayer) || (TargetClosestEnemy(1.5f, true) && followEnemy))
                    {
                        StopSearch(currentSearch);
                        SwitchToBehaviourClientRpc((int)State.Following);
                        break;
                    }
                    break;

                case (int)State.Following:
                    if (healingBuffTime > 0f) { agent.speed = 10f; } else { agent.speed = 5f; }
                    agent.stoppingDistance = followingRange;
                    // Keep targeting closest player, unless they are over playerDetectionRange units away and we can't see them.
                    if (!TargetClosestEntity())
                    {
                        logger.LogDebug("Stop Targeting");
                        targetPlayer = null;
                        targetEnemy = null;
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        StartSearch(transform.position);
                        return;
                    }
                    
                    if (MoveToSweetsIfDroppedByPlayer()) { EatSweetsIfClose(); return; }
                    FollowTarget();
                    MoveToJarIfClose();

                    break;

                case (int)State.Blocking:
                    agent.speed = 20f;
                    agent.stoppingDistance = 0f;
                    if (!MoveInFrontOfTurret())
                    {
                        logger.LogDebug("Stop Blocking");
                        blockedTurret = null;
                        blockingAnimation = false;
                        networkAnimator.SetTrigger("stopStretch");
                        //DoAnimationClientRpc("stopStretch");
                        SwitchToBehaviourClientRpc((int)State.Following);
                        return;
                    }

                    break;

                case (int)State.Healing:
                    agent.speed = 10f;
                    agent.stoppingDistance = huggingRange;
                    if (!hugging)
                    {
                        networkAnimator.SetTrigger("startHugging");
                        //DoAnimationClientRpc("startHugging");
                        hugging = true;
                    }
                    MoveToHealTarget();
                    break;

                case (int)State.Hyper:
                    agent.speed = 20f;
                    agent.stoppingDistance = 0f;
                    if (hyperTime <= 0f)
                    {
                        candyEaten = 0;
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        return;
                    }
                    break;

                default:
                    logger.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
            //logger.LogDebug("Finished doaiinterval");
        }

        public void SetOutsideOrInside()
        {
            GameObject closestOutsideNode = GetClosestAINode(false);
            GameObject closestInsideNode = GetClosestAINode(true);

            if (Vector3.Distance(transform.position, closestOutsideNode.transform.position) < Vector3.Distance(transform.position, closestInsideNode.transform.position))
            {
                SetEnemyOutsideClientRpc(true);
            }
        }

        public GameObject GetClosestAINode(bool inside)
        {
            float closestDistance = Mathf.Infinity;
            GameObject closestNode = null!;

            List<GameObject> nodes = inside ? GameObject.FindGameObjectsWithTag("AINode").ToList() : GameObject.FindGameObjectsWithTag("OutsideAINode").ToList();

            foreach (GameObject node in nodes)
            {
                float distanceToNode = Vector3.Distance(transform.position, node.transform.position);
                if (distanceToNode < closestDistance)
                {
                    closestDistance = distanceToNode;
                    closestNode = node;
                }
            }
            return closestNode;
        }

        public bool MoveToJarIfClose()
        {
            foreach (var item in UnityEngine.Object.FindObjectsOfType<ContainmentJarBehavior>())
            {
                if (Vector3.Distance(transform.position, item.transform.position) < 15f) // TODO: Test this
                {
                    if (SetDestinationToPosition(item.transform.position, true))
                    {
                        if (Vector3.Distance(transform.position, item.transform.position) < 1f)
                        {
                            item.ChangeJarContents(ContainmentJarBehavior.Contents.SCP999);
                            RoundManager.Instance.DespawnEnemyOnServer(this.NetworkObject);
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        public bool TargetClosestEntity()
        {
            if (targetPlayer != null && followPlayer && TargetClosestPlayer() && (Vector3.Distance(transform.position, targetPlayer.transform.position) < playerDetectionRange || CheckLineOfSightForPosition(targetPlayer.transform.position))) { return true; }
            else if (targetEnemy != null && followEnemy && TargetClosestEnemy(5f) && (Vector3.Distance(transform.position, targetEnemy.transform.position) < enemyDetectionRange * 2 || CheckLineOfSightForPosition(targetEnemy.transform.position))) { return true; }
            else { return false; }
        }

        public bool TargetClosestEnemy(float bufferDistance = 1.5f, bool requireLineOfSight = false, float viewWidth = 70f)
        {
            mostOptimalDistance = 2000f;
            EnemyAI previousTarget = targetEnemy;
            targetEnemy = null;
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.isEnemyDead) { continue; }
                if (enemy == this) { continue; }
                if (!PathIsIntersectedByLineOfSight(enemy.transform.position, calculatePathDistance: false, avoidLineOfSight: false) && (!requireLineOfSight || CheckLineOfSightForPosition(enemy.transform.position, viewWidth, 40)))
                {
                    tempDist = Vector3.Distance(transform.position, enemy.transform.position);
                    if (tempDist < mostOptimalDistance)
                    {
                        mostOptimalDistance = tempDist;
                        targetEnemy = enemy;
                    }
                }
            }
            if (targetEnemy != null && bufferDistance > 0f && previousTarget != null && Mathf.Abs(mostOptimalDistance - Vector3.Distance(transform.position, previousTarget.transform.position)) < bufferDistance)
            {
                targetEnemy = previousTarget;
            }

            return targetEnemy != null;
        }

        void FollowTarget()
        {
            Vector3 pos;

            if (targetPlayer != null)
            {
                pos = targetPlayer.transform.position;
            }
            else if (targetEnemy != null)
            {
                pos = targetEnemy.transform.position;
            }
            else { return; }

            if (Vector3.Distance(transform.position, pos) < followingRange) // Within following range
            {
                if (!dancing && IsNearbyPlayerEmoting(followingRange) && !walking)
                {
                    networkAnimator.SetTrigger("startDancing");
                    //DoAnimationClientRpc("startDancing");
                    dancing = true;
                }
                else if (dancing && !IsNearbyPlayerEmoting(followingRange) && !walking)
                {
                    networkAnimator.SetTrigger("stopWalking");
                    //DoAnimationClientRpc("stopWalking");
                    dancing = false;
                }
            }
            else // Not within following range
            {
                SetDestinationToPosition(pos, false);
                dancing = false;
            }
        }

        void MoveToHealTarget()
        {
            if (targetPlayer != null)
            {
                logger.LogDebug("Player hp: " + targetPlayer.health);
                if (targetPlayer.health >= 100 || targetPlayer.isPlayerDead)
                {
                    if (hugging)
                    {
                        hugging = false;
                        networkAnimator.SetTrigger("stopHugging");
                        //DoAnimationClientRpc("stopHugging");
                    }
                    SwitchToBehaviourClientRpc((int)State.Following);
                    return;
                }
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= huggingRange)
                {
                    HealPlayer(targetPlayer);
                    PlayHugSFX();
                }

                SetDestinationToPosition(targetPlayer.transform.position, false);
            }
            else if (targetEnemy != null)
            {
                int maxHealth = targetEnemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
                logger.LogDebug("Enemy hp: " + targetEnemy.enemyHP + "/" + maxHealth);

                if (targetEnemy.enemyHP >= maxHealth || targetEnemy.isEnemyDead)
                {
                    SwitchToBehaviourClientRpc((int)State.Following);
                    if (hugging)
                    {
                        hugging = false;
                        networkAnimator.SetTrigger("stopHugging");
                        //DoAnimationClientRpc("stopHugging");
                    }
                    return;
                }
                if (Vector3.Distance(transform.position, targetEnemy.transform.position) < huggingRange)
                {
                    HealEnemy(targetEnemy);
                    PlayHugSFX();
                }

                SetDestinationToPosition(targetEnemy.transform.position, false);
            }
        }

        bool MoveInFrontOfTurret()
        {
            if (blockedTurret != null)
            {
                if (blockedTurret.turretMode == TurretMode.Firing || blockedTurret.turretMode == TurretMode.Charging)
                {
                    Vector3 interpolatedPosition = Vector3.Lerp(targetPlayer.transform.position, blockedTurret.transform.position, 0.5f);
                    SetDestinationToPosition(interpolatedPosition, false);

                    if (Vector3.Distance(transform.position, interpolatedPosition) < 1f)
                    {
                        if (timeSinceBlockSFX > 0.5f)
                        {
                            if (!blockingAnimation)
                            {
                                networkAnimator.SetTrigger("startStretch");
                                //DoAnimationClientRpc("startStretch");
                                blockingAnimation = true;
                            }
                            logger.LogDebug("Playing hitSFX");
                            int randomIndex = Random.Range(0, hitSFXList.Count - 1);
                            creatureSFX.PlayOneShot(hitSFXList[randomIndex], 1f);
                            timeSinceBlockSFX = 0f;
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        public bool MoveToSweetsIfDroppedByPlayer()
        {
            foreach (GrabbableObject item in FindObjectsOfType<GrabbableObject>())
            {
                if (Vector3.Distance(transform.position, item.transform.position) <= followingRange)
                {
                    if (Sweets.Contains(item.itemProperties.itemName) && item.hasBeenHeld && !item.heldByPlayerOnServer)
                    {
                        logger.LogDebug("Moving to item: " + item.itemProperties.itemName);
                        agent.stoppingDistance = 0f;
                        SetDestinationToPosition(item.transform.position, false);
                        return true;
                    }
                }
            }
            return false;
        }

        public void EatSweetsIfClose()
        {
            foreach(GrabbableObject item in FindObjectsOfType<GrabbableObject>())
            {
                if (Vector3.Distance(transform.position, item.transform.position) < 1f)
                {
                    if (Sweets.Contains(item.itemProperties.itemName))
                    {
                        logger.LogDebug("Eating item: " + item.itemProperties.itemName);
                        if (item.itemProperties.itemName == "SCP-559") { ChangeSizeClientRpc(transform.localScale.y / 2); } // TODO: Test this
                        if (item.itemProperties.itemName == "Black Candy") { MakeHyper(60f); } // TODO: Test this
                        if (item.itemProperties.itemName == "Cake") { healingBuffTime += 10f; } // TODO: Test this

                        item.GetComponent<NetworkObject>().Despawn(true);
                        Destroy(item.gameObject);

                        candyEaten += 1;
                        logger.LogDebug("Candy eaten: " + candyEaten);
                        healingBuffTime += 20f;

                        if (candyEaten >= maxCandy) { MakeHyper(30f); }
                    }
                }
            }
        }

        public void MakeHyper(float duration)
        {
            hyperTime += duration;
            SwitchToBehaviourClientRpc((int)State.Hyper);
            networkAnimator.SetTrigger("startHyperDancing");
            //DoAnimationClientRpc("startHyperDancing");
            dancing = true;
            StartSearch(transform.position);
            targetPlayer = null;
            targetEnemy = null;
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
        }

        public override void ReachedNodeInSearch()
        {
            base.ReachedNodeInSearch();
            int randomIndex = Random.Range(0, roamSFXList.Count - 1);
            creatureVoice.PlayOneShot(roamSFXList[randomIndex], 1f);
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);

            if (timeSinceHealing > 1f)
            {
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);
                HealPlayer(player);
            }
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);

            if (timeSinceHealing > 1f)
            {
                if (collidedEnemy != null)
                {
                    HealEnemy(collidedEnemy);
                }
                else { logger.LogError("Collided enemy is null"); }
            }
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null!, bool playHitSFX = true, int hitID = -1)
        {
            int randomIndex = Random.Range(0, hitSFXList.Count - 1);
            creatureVoice.PlayOneShot(hurtSFXList[randomIndex], 1f);
        }

        public bool IsNearbyPlayerEmoting(float distance)
        {
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (Vector3.Distance(transform.position, player.transform.position) < distance && player.performingEmote) { return true; }
            }
            return false;
        }

        public void PlayHugSFX()
        {
            if (timeSinceHugSFX > 3.3f)
            {
                int randomIndex = Random.Range(0, hugSFXList.Count - 1);
                creatureVoice.PlayOneShot(hugSFXList[randomIndex], 1f);
                timeSinceHugSFX = 0f;
            }
        }

        public void HealPlayer(PlayerControllerB player)
        {
            if (player != null && player.health < 100 && timeSinceHealing > 1f)
            {
                logger.LogDebug("Healing player: " + player.playerUsername);

                int newHealthAmount;
                if (healingBuffTime > 0f) { newHealthAmount = player.health + (playerHealAmount * 2); }
                else { newHealthAmount = player.health + playerHealAmount; }

                if (newHealthAmount > 100) { newHealthAmount = 100; }

                //HealPlayerClientRpc(player.actualClientId, newHealthAmount);
                player.health = newHealthAmount;
                HUDManager.Instance.UpdateHealthUI(player.health, false);
                timeSinceHealing = 0f;
            }
        }

        public void HealEnemy(EnemyAI enemyToHeal)
        {
            //SpawnableEnemyWithRarity spawnableEnemy = GetEnemies().Where(x => x.enemyType == enemyToHeal.enemyType).FirstOrDefault();
            //if (spawnableEnemy == null) { logger.LogError("Enemy to heal not found: " + enemyToHeal.enemyType.enemyName); return; }

            //int maxHealth = spawnableEnemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
            int maxHealth = enemyToHeal.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP; // TODO: Test this

            logger.LogDebug($"{enemyToHeal.enemyType.enemyName} HP: {enemyToHeal.enemyHP}/{maxHealth}");

            if (enemyToHeal.enemyHP < maxHealth && timeSinceHealing > 1f)
            {

                if (healingBuffTime > 0f) { enemyToHeal.enemyHP += enemyHealAmount * 2; }
                else { enemyToHeal.enemyHP += enemyHealAmount; }

                //logger.LogDebug($"{enemyToHeal.enemyType.enemyName} HP: {enemyToHeal.enemyHP}/{maxHealth}");

                timeSinceHealing = 0f;
            }
        }

        // RPC's

        /*[ClientRpc]
        private void DoAnimationClientRpc(string animationName)
        {
            logger.LogDebug($"Doing animation: {animationName}");
            creatureAnimator.SetTrigger(animationName);
        }*/

        [ClientRpc]
        private void SetEnemyOutsideClientRpc(bool value)
        {
            SetEnemyOutside(value);
        }

        [ClientRpc]
        private void HealPlayerClientRpc(ulong clientId, int newHealthAmount) // TODO: May be unneeded
        {
            PlayerControllerB player = PlayerFromId(clientId);
            if (player == null) { return; }

            player.playersManager.fearLevel = 0f;

            player.health = newHealthAmount;
            HUDManager.Instance.UpdateHealthUI(newHealthAmount, false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayerTookDamageServerRpc(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                PlayerControllerB player = PlayerFromId(clientId);
                if (player == null) { return; }

                float multiplier = 2 - (player.health / 100f);
                float range = configPlayerDetectionRange.Value * multiplier;

                if (Vector3.Distance(transform.position, player.transform.position) <= range)
                {
                    targetEnemy = null;
                    targetPlayer = player;

                    if (hyperTime > 0f) { return; }
                    if (currentSearch != null) { StopSearch(currentSearch); }
                    SwitchToBehaviourClientRpc((int)State.Healing);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void EnemyTookDamageServerRpc(NetworkObjectReference netObjRef)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (hyperTime > 0f) { return; }

                if (netObjRef.TryGet(out NetworkObject networkObject)) // TODO: Test this
                {
                    targetEnemy = networkObject.GetComponent<EnemyAI>();
                    if (currentSearch != null) { StopSearch(currentSearch); }
                    SwitchToBehaviourClientRpc((int)State.Healing);
                }
            }
        }

        [ClientRpc]
        private void ChangeSizeClientRpc(float size) // TODO: Test this
        {
            transform.localScale = new Vector3(size, size, size);
        }

        [ServerRpc(RequireOwnership = false)]
        public void BlockTurretFireServerRpc(NetworkObjectReference netObjRef)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (netObjRef.TryGet(out NetworkObject networkObject)) // TODO: Test this
                {
                    blockedTurret = networkObject.GetComponent<Turret>();
                    SwitchToBehaviourClientRpc((int)State.Blocking);
                }
                /*foreach (Turret turret in UnityEngine.Object.FindObjectsOfType<Turret>())
                {
                    if (turret.targetPlayerWithRotation == targetPlayer)
                    {
                        blockedTurret = turret;
                        SwitchToBehaviourClientRpc((int)State.Blocking);
                        return;
                    }
                }*/
            }
        }
    }
}