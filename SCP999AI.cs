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

namespace SCP999
{
    // Movement speed 5f
    class SCP999AI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public Transform turnCompass = null!;
#pragma warning restore 0649

        EnemyAI targetEnemy;

        float timeSinceHealing;
        float healingBuffTime;
        float hyperTime;

        int playerHealAmount;
        int enemyHealAmount;

        float playerDetectionRange;
        float enemyDetectionRange;
        float rangeMultiplier = 1f;
        float followingRange;

        bool walking = true;
        bool hugging = false;
        bool dancing = false;

        int maxCandy = 3;
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

            //StartSearch(transform.position);
            //agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.NoObstacleAvoidance; // TODO: Use this if enemy still collides with other enemies
        }

        public override void Update()
        {
            base.Update();

            timeSinceHealing += Time.deltaTime;

            if (healingBuffTime > 0f)
            {
                healingBuffTime -= Time.deltaTime;
                candyEaten = 0;
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
                    if (TargetClosestPlayer(1.5f, true) || TargetClosestEnemy(1.5f, true))
                    {
                        StopSearch(currentSearch);
                        SwitchToBehaviourClientRpc((int)State.Following);
                        break;
                    }
                    break;

                case (int)State.Following:
                    if (healingBuffTime > 0f) { agent.speed = 10f; } else { agent.speed = 5f; }
                    // Keep targeting closest player, unless they are over playerDetectionRange units away and we can't see them.
                    if (!TargetClosestEntity())
                    {
                        logger.LogDebug("Stop Target Player");
                        targetPlayer = null;
                        targetEnemy = null;
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        StartSearch(transform.position);
                        return;
                    }
                    
                    if (MoveToSweetsIfDroppedByPlayer()) { EatSweetsIfClose(); return; }
                    FollowTarget();

                    break;

                case (int)State.Healing:
                    agent.speed = 10f; // TODO: Test this
                    MoveToHealTarget();
                    break;

                case (int)State.Hyper:
                    agent.speed = 20f;
                    if (hyperTime <= 0f)
                    {
                        candyEaten = 0;
                        DoAnimationClientRpc("stopDancing");
                        SwitchToBehaviourClientRpc((int)State.Roaming);
                        StartSearch(transform.position);
                    }
                    break;

                default:
                    logger.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        public bool TargetClosestEntity()
        {
            if (targetPlayer != null && TargetClosestPlayer() && (Vector3.Distance(transform.position, targetPlayer.transform.position) < playerDetectionRange / 2 || CheckLineOfSightForPosition(targetPlayer.transform.position))) { return true; }
            else if (targetEnemy != null && TargetClosestEnemy() && (Vector3.Distance(transform.position, targetEnemy.transform.position) < enemyDetectionRange / 2 || CheckLineOfSightForPosition(targetEnemy.transform.position))) { return true; }
            else { return false; }
        }

        public bool TargetClosestEnemy(float bufferDistance = 1.5f, bool requireLineOfSight = false, float viewWidth = 70f)
        {
            mostOptimalDistance = 2000f;
            EnemyAI previousTarget = targetEnemy;
            targetEnemy = null;
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (!PathIsIntersectedByLineOfSight(enemy.transform.position, calculatePathDistance: false, avoidLineOfSight: false) && (!requireLineOfSight || CheckLineOfSightForPosition(enemy.transform.position, viewWidth, 40)))
                {
                    tempDist = Vector3.Distance(base.transform.position, enemy.transform.position);
                    if (tempDist < mostOptimalDistance)
                    {
                        mostOptimalDistance = tempDist;
                        targetEnemy = enemy;
                    }
                }
            }
            if (targetEnemy != null && bufferDistance > 0f && previousTarget != null && Mathf.Abs(mostOptimalDistance - Vector3.Distance(base.transform.position, previousTarget.transform.position)) < bufferDistance)
            {
                targetEnemy = previousTarget;
            }
            return targetEnemy != null;
        }

        void FollowTarget() // TODO: Test this
        {
            if (targetPlayer != null)
            {
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) < followingRange)
                {
                    agent.ResetPath();
                    if (walking) { DoAnimationClientRpc("stopWalking"); walking = false; }
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position, false);
                if (!walking) { DoAnimationClientRpc("startWalking"); walking = true; }
            }
            else if (targetEnemy != null)
            {
                if (Vector3.Distance(transform.position, targetEnemy.transform.position) < followingRange)
                {
                    agent.ResetPath();
                    if (walking) { DoAnimationClientRpc("stopWalking"); walking = false; }
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position, false);
                if (!walking) { DoAnimationClientRpc("startWalking"); walking = true; }
            }
        }

        void MoveToHealTarget() // TODO: Test this
        {
            if (targetPlayer != null)
            {
                if (targetPlayer.health >= 100)
                {
                    SwitchToBehaviourClientRpc((int)State.Following);
                    if (hugging) { DoAnimationClientRpc("stopHugging"); hugging = false; }
                    return;
                }
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) < 0.5f)
                {
                    agent.ResetPath();
                    if (!hugging) { DoAnimationClientRpc("startHugging"); hugging = true; }
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position, false);
            }
            else if (targetEnemy != null)
            {
                int maxHealth = GetEnemyMaxHealth(targetEnemy.enemyType.enemyName);

                if (targetEnemy.enemyHP >= maxHealth)
                {
                    SwitchToBehaviourClientRpc((int)State.Following);
                    if (hugging) { DoAnimationClientRpc("stopHugging"); hugging = false; }
                    return;
                }
                if (Vector3.Distance(transform.position, targetEnemy.transform.position) < 0.5f)
                {
                    agent.ResetPath();
                    if (!hugging) { DoAnimationClientRpc("startHugging"); hugging = true; }
                    return;
                }

                SetDestinationToPosition(targetEnemy.transform.position, false);
            }
        }

        public bool MoveToSweetsIfDroppedByPlayer() // TODO: Test this
        {
            foreach (GrabbableObject item in FindObjectsOfType<GrabbableObject>())
            {
                if (Vector3.Distance(transform.position, item.transform.position) < followingRange)
                {
                    if (Sweets.Contains(item.itemProperties.itemName) && item.hasBeenHeld)
                    {
                        SetDestinationToPosition(item.transform.position, false);
                        return true;
                    }
                }
            }
            return false;
        }

        public void EatSweetsIfClose() // TODO: Test this
        {
            foreach(GrabbableObject item in FindObjectsOfType<GrabbableObject>())
            {
                if (Vector3.Distance(transform.position, item.transform.position) < 1f)
                {
                    if (Sweets.Contains(item.itemProperties.itemName))
                    {
                        if (item.itemProperties.itemName == "SCP-559") { ChangeSizeClientRpc(0.5f); }
                        if (item.itemProperties.itemName == "SCP-999") { MakeHyper(60f); }

                        candyEaten += 1;
                        healingBuffTime += 30f;

                        if (candyEaten >= maxCandy) { MakeHyper(30f); }

                        item.itemProperties.spawnPrefab.GetComponent<NetworkObject>().Despawn(true);
                        Destroy(item.gameObject);
                    }
                }
            }
        }

        public void MakeHyper(float duration) // TODO: Test this
        {
            hyperTime += duration;
            DoAnimationClientRpc("startHyperDancing");
            SwitchToBehaviourClientRpc((int)State.Hyper);
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
                player.JumpToFearLevel(0f, false); // TODO: Test this
                if (player != null && player.health < 100)
                {
                    timeSinceHealing = 0f;
                    logger.LogDebug("Healing player: " + player.playerUsername);
                    HealPlayerClientRpc(player.actualClientId);
                }
            }

            return;
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy = null) // TODO: Test this
        {
            logger.LogDebug("Collided with enemy"); // TODO: Make sure this works
            logger.LogDebug("Collided with " + collidedEnemy.enemyType.enemyName);
            base.OnCollideWithEnemy(other, collidedEnemy);

            if (timeSinceHealing > 1f)
            {
                if (collidedEnemy != null) // TODO: Test this more
                {
                    timeSinceHealing = 0f;

                    SpawnableEnemyWithRarity enemy = RoundManager.Instance.currentLevel.Enemies.Where(x => x.enemyType.enemyName == collidedEnemy.enemyType.enemyName).FirstOrDefault();
                    if (enemy == null) { logger.LogDebug("Enemy not found: " + collidedEnemy.enemyType.enemyName); return; }

                    int maxHealth = enemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

                    if (collidedEnemy.enemyHP < maxHealth)
                    {
                        logger.LogDebug("Healing enemy: " + collidedEnemy.enemyType.enemyName);
                        collidedEnemy.enemyHP += 1;
                    }
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

        public static int GetEnemyMaxHealth(string enemyName) // TODO: Test this
        {
            foreach (EnemyAI enemy in Resources.FindObjectsOfTypeAll<EnemyAI>())
            {
                logger.LogDebug(enemy.enemyType.enemyName + ": " + enemy.enemyHP);
                if (enemy.enemyType.enemyName == enemyName) { return enemy.enemyHP; }
            }
            return -1;
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
                int newHealthAmount;
                if (healingBuffTime > 0f) { newHealthAmount = player.health + playerHealAmount * 2; }
                else { newHealthAmount = player.health + playerHealAmount; }

                if (newHealthAmount > 100) { player.health = 100; }
                else { player.health = newHealthAmount; }
                HUDManager.Instance.UpdateHealthUI(newHealthAmount, false);
            }
        }
        
        [ClientRpc]
        private void HealEnemyClientRpc(int spawnedEnemyIndex) // TODO: may be unneeded
        {
            if (spawnedEnemyIndex < RoundManager.Instance.SpawnedEnemies.Count)
            {
                EnemyAI enemy = RoundManager.Instance.SpawnedEnemies[spawnedEnemyIndex];


                // TODO: Continue
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void EnemyTookDamageServerRpc(int spawnedEnemyIndex, float health, float maxHealth)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                targetEnemy = null;
                targetPlayer = null;

                float multiplier = 2 - (health / maxHealth);
                float range = enemyDetectionRange * multiplier;

                EnemyAI enemy = RoundManager.Instance.SpawnedEnemies[spawnedEnemyIndex];
                if (Vector3.Distance(transform.position, enemy.transform.position) < range)
                {
                    targetEnemy = enemy;
                    SwitchToBehaviourClientRpc((int)State.Healing);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayerTookDamageServerRpc(ulong clientId, float health, float maxHealth)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                targetEnemy = null;
                targetPlayer = null;

                float multiplier = 2 - (health / maxHealth);
                float range = playerDetectionRange * multiplier;

                PlayerControllerB player = StartOfRound.Instance.allPlayerScripts.Where(x => x.actualClientId == clientId).FirstOrDefault();
                if (Vector3.Distance(transform.position, player.transform.position) < range)
                {
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