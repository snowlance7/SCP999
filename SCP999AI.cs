using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalLib;
using Unity.Netcode;
using UnityEngine;
using static SCP999.Plugin;
using UnityEngine.UI;
using System.Linq;
using System.Runtime.CompilerServices;
using static Netcode.Transports.Facepunch.FacepunchTransport;
//using SCP999.Patches;
using System.Drawing;
using Unity.Services.Authentication;
using HarmonyLib;
using ES3Types;
using System.Reflection;
using UnityEngine.AI;
using System;
using static LethalLib.Modules.Enemies;

namespace SCP999
{
    // Movement speed 5f
    class SCP999AI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public Transform turnCompass = null!;
#pragma warning restore 0649

        public EnemyAI targetEnemy;

        public float timeSinceHealing;
        float healingBuffTime;
        float hyperTime;

        int playerHealAmount;
        int enemyHealAmount;

        float playerDetectionRange;
        float enemyDetectionRange;
        float rangeMultiplier = 1f;
        float followingRange;

        bool walking = false;
        bool hugging = false;
        bool dancing = false;

        const int maxCandy = 3;
        int candyEaten;

        enum State
        {
            Roaming,
            Following,
            Healing,
            Hyper
        }

        public override void Start()
        {
            base.Start();
            logger.LogDebug("SCP-999 Spawned");

            playerHealAmount = configPlayerHealAmount.Value;
            enemyHealAmount = configEnemyHealAmount.Value;

            playerDetectionRange = configPlayerDetectionRange.Value;
            enemyDetectionRange = configEnemyDetectionRange.Value;

            followingRange = configFollowRange.Value;

            candyEaten = 0;

            timeSinceHealing = 0f;
            healingBuffTime = 0f;
            hyperTime = 0f;

            currentBehaviourStateIndex = (int)State.Roaming;
            RoundManager.Instance.SpawnedEnemies.Add(this);

            StartSearch(transform.position);
        }

        public override void Update()
        {
            base.Update();

            timeSinceHealing += Time.deltaTime;

            if (healingBuffTime > 0f)
            {
                healingBuffTime -= Time.deltaTime;

                if (healingBuffTime <= 0f)
                {
                    candyEaten = 0;
                }
            }

            if (hyperTime > 0f)
            {
                hyperTime -= Time.deltaTime;
            }

            var state = currentBehaviourStateIndex;

            if (targetPlayer != null)
            {
                turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }

            if (state != (int)State.Hyper)
            {
                if (agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f)
                {
                    if (walking && !hugging && !dancing)
                    {
                        walking = false;
                        DoAnimationClientRpc("stopWalking");
                    }
                }
                else
                {
                    if (!walking && !hugging && !dancing)
                    {
                        walking = true;
                        DoAnimationClientRpc("startWalking");
                    }
                }
            }
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            };

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    agent.speed = 5f;
                    agent.stoppingDistance = 0;
                    hugging = false;
                    dancing = false;
                    if (TargetClosestPlayer(1.5f, true) || TargetClosestEnemy(1.5f, true))
                    {
                        StopSearch(currentSearch);
                        SwitchToBehaviourClientRpc((int)State.Following);
                        break;
                    }
                    break;

                case (int)State.Following:
                    if (healingBuffTime > 0f) { agent.speed = 10f; } else { agent.speed = 5f; }
                    agent.stoppingDistance = followingRange;
                    hugging = false;
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
                    
                    //if (MoveToSweetsIfDroppedByPlayer()) { EatSweetsIfClose(); return; } // TODO: Rework and test this if needed
                    FollowTarget();

                    break;

                case (int)State.Healing:
                    agent.speed = 10f; // TODO: Test this
                    agent.stoppingDistance = 0.5f;
                    dancing = false;
                    MoveToHealTarget();
                    break;

                case (int)State.Hyper:
                    agent.speed = 20f;
                    agent.stoppingDistance = 0;
                    hugging = false;
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
        }

        public bool TargetClosestEntity() // TODO: Test this, make sure its prioritizing players over enemies
        {
            if (targetPlayer != null && TargetClosestPlayer() && (Vector3.Distance(transform.position, targetPlayer.transform.position) < playerDetectionRange || CheckLineOfSightForPosition(targetPlayer.transform.position))) { return true; }
            else if (targetEnemy != null && TargetClosestEnemy(5f) && (Vector3.Distance(transform.position, targetEnemy.transform.position) < enemyDetectionRange * 2 || CheckLineOfSightForPosition(targetEnemy.transform.position))) { return true; }
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

        void FollowTarget() // TODO: Test this more
        {
            Vector3 pos;

            if (targetPlayer != null)
            {
                pos = targetPlayer.transform.position;
            }
            else if (targetEnemy != null)
            {
                //logger.LogDebug("Targeting enemy: " + targetEnemy.enemyType.enemyName);
                pos = targetEnemy.transform.position;
            }
            else { return; }

            if (Vector3.Distance(transform.position, pos) < followingRange) // Within following range // TODO: Test this
            {
                if (!dancing && IsNearbyPlayerEmoting(followingRange) && !walking && !hugging)
                {
                    DoAnimationClientRpc("startDancing");
                    dancing = true;
                }
                else if (dancing && !IsNearbyPlayerEmoting(followingRange) && !walking && !hugging)
                {
                    DoAnimationClientRpc("stopWalking");
                    dancing = false;
                }
            }
            else // Not within following range
            {
                SetDestinationToPosition(pos, false);
                dancing = false;
            }
        }

        void MoveToHealTarget() // TODO: Test this
        {
            if (targetPlayer != null)
            {
                if (targetPlayer.health >= 100)
                {
                    if (hugging) { DoAnimationClientRpc("stopHugging"); hugging = false; }
                    SwitchToBehaviourClientRpc((int)State.Following);
                    return;
                }
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= 1f)
                {
                    if (!hugging) { DoAnimationClientRpc("startHugging"); hugging = true; }
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position, false);
            }
            else if (targetEnemy != null)
            {
                int maxHealth = targetEnemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

                if (targetEnemy.enemyHP >= maxHealth)
                {
                    SwitchToBehaviourClientRpc((int)State.Following);
                    if (hugging) { DoAnimationClientRpc("stopHugging"); hugging = false; }
                    return;
                }
                if (Vector3.Distance(transform.position, targetEnemy.transform.position) < 1f)
                {
                    if (!hugging) { DoAnimationClientRpc("startHugging"); hugging = true; }
                    return;
                }

                SetDestinationToPosition(targetEnemy.transform.position, false);
            }
        }

        public bool MoveToSweetsIfDroppedByPlayer() // TODO: Rework this if needed
        {
            foreach (GrabbableObject item in FindObjectsOfType<GrabbableObject>())
            {
                if (Vector3.Distance(transform.position, item.transform.position) < followingRange)
                {
                    if (Sweets.Contains(item.itemProperties.itemName) && item.hasBeenHeld && !item.isHeld)
                    {
                        SetDestinationToPosition(item.transform.position, false);
                        return true;
                    }
                }
            }
            return false;
        }

        public void EatSweetsIfClose() // TODO: Rework this if needed
        {
            foreach(GrabbableObject item in FindObjectsOfType<GrabbableObject>()) // TODO: Make this more efficient
            {
                if (Vector3.Distance(transform.position, item.transform.position) < 1f)
                {
                    if (Sweets.Contains(item.itemProperties.itemName))
                    {
                        if (item.itemProperties.itemName == "SCP-559") { ChangeSizeClientRpc(0.5f); }
                        if (item.itemProperties.itemName == "SCP-999") { MakeHyper(60f); }

                        candyEaten += 1;
                        healingBuffTime += 20f;
                        if (item.itemProperties.itemName == "Cake") { healingBuffTime += 10f; }

                        if (candyEaten >= maxCandy) { MakeHyper(30f); }

                        item.itemProperties.spawnPrefab.GetComponent<NetworkObject>().Despawn(true);
                        Destroy(item.gameObject);
                    }
                }
            }
        }

        public void MakeHyper(float duration) // TODO: Rework this if needed
        {
            hyperTime += duration;
            SwitchToBehaviourClientRpc((int)State.Hyper);
            DoAnimationClientRpc("startHyperDancing");
            dancing = true;
            StartSearch(transform.position);
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            logger.LogDebug("Collided with player");

            if (timeSinceHealing > 1f)
            {
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);

                HealPlayer(player);
            }

            return;
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null) // TODO: Test this
        {
            //logger.LogDebug("Collided with " + collidedEnemy.enemyType.enemyName);
            base.OnCollideWithEnemy(other, collidedEnemy);

            if (timeSinceHealing > 1f)
            {
                if (collidedEnemy != null) // TODO: Test this more
                {
                    HealEnemy(collidedEnemy);
                }
                else { logger.LogError("Collided enemy is null"); }
            }

            return;
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
        {
            base.HitEnemy(0, playerWhoHit, playHitSFX, hitID);
        }

        public bool IsNearbyPlayerEmoting(float distance) // TODO: Test this // TODO: Implement this
        {
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (Vector3.Distance(transform.position, player.transform.position) < distance && player.performingEmote) { return true; }
            }
            return false;
        }

        public bool HealPlayer(PlayerControllerB playerToHeal)
        {
            if (playerToHeal != null && playerToHeal.health < 100)
            {
                logger.LogDebug("Healing player: " + playerToHeal.playerUsername);
                HealPlayerClientRpc(playerToHeal.actualClientId);
                timeSinceHealing = 0f;
                return true;
            }
            return false;
        }

        public bool HealEnemy(EnemyAI enemyToHeal) // TODO: Test this
        {
            SpawnableEnemyWithRarity spawnableEnemy = RoundManager.Instance.currentLevel.Enemies.Where(x => x.enemyType.enemyName == enemyToHeal.enemyType.enemyName).FirstOrDefault();
            if (spawnableEnemy == null) { logger.LogError("Enemy not found: " + enemyToHeal.enemyType.enemyName); return false; }

            int maxHealth = spawnableEnemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

            if (enemyToHeal.enemyHP < maxHealth)
            {
                logger.LogDebug("Healing " + enemyToHeal.enemyType.enemyName + ": " + enemyToHeal.enemyHP);
                logger.LogDebug($"{enemyToHeal.enemyType.enemyName} HP: {enemyToHeal.enemyHP}/{maxHealth}");

                if (healingBuffTime > 0f) { enemyToHeal.enemyHP += enemyHealAmount * 2; }
                else { enemyToHeal.enemyHP += enemyHealAmount; }

                timeSinceHealing = 0f;
                return true;
            }
            return false;
        }

        // RPC's

        [ClientRpc]
        private void DoAnimationClientRpc(string animationName)
        {
            logger.LogDebug("Animation: " + animationName);
            creatureAnimator.SetTrigger(animationName);
        }

        [ClientRpc]
        private void HealPlayerClientRpc(ulong clientId)
        {
            PlayerControllerB player = StartOfRound.Instance.localPlayerController;
            if (player.actualClientId == clientId)
            {
                player.JumpToFearLevel(0f, false); // TODO: Test this
                int newHealthAmount;
                if (healingBuffTime > 0f) { newHealthAmount = player.health + (playerHealAmount * 2); }
                else { newHealthAmount = player.health + playerHealAmount; }

                if (newHealthAmount > 100) { player.health = 100; }
                else { player.health = newHealthAmount; }
                HUDManager.Instance.UpdateHealthUI(newHealthAmount, false);
            }
        }
        
        /*[ClientRpc]
        private void HealEnemyClientRpc(int spawnedEnemyIndex) // TODO: may be unneeded
        {
            if (spawnedEnemyIndex < RoundManager.Instance.SpawnedEnemies.Count)
            {
                EnemyAI enemy = RoundManager.Instance.SpawnedEnemies[spawnedEnemyIndex];


                // TODO: Continue
            }
        }*/

        [ServerRpc(RequireOwnership = false)]
        public void EnemyTookDamageServerRpc(int spawnedEnemyIndex) // TODO: Test this
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {

                EnemyAI enemy = RoundManager.Instance.SpawnedEnemies[spawnedEnemyIndex];
                int maxHealth = enemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

                float multiplier = 2 - (enemy.enemyHP / maxHealth);
                float range = enemyDetectionRange * multiplier;

                if (Vector3.Distance(transform.position, enemy.transform.position) <= range)
                {
                    targetPlayer = null;
                    targetEnemy = enemy;
                    SwitchToBehaviourClientRpc((int)State.Healing);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayerTookDamageServerRpc(ulong clientId) // TODO: Test this
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                PlayerControllerB player = StartOfRound.Instance.allPlayerScripts.Where(x => x.actualClientId == clientId).FirstOrDefault();

                float multiplier = 2 - (player.health / 100f);
                float range = playerDetectionRange * multiplier;

                if (Vector3.Distance(transform.position, player.transform.position) <= range)
                {
                    targetEnemy = null;
                    targetPlayer = player;
                    SwitchToBehaviourClientRpc((int)State.Healing);
                }
            }
        }

        [ClientRpc]
        private void ChangeSizeClientRpc(float size) // TODO: Test this
        {
            transform.localScale = new Vector3(size, size, size);
        }
    }
}