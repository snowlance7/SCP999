using BepInEx.Logging;
using GameNetcodeStuff;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using static SCP999.Plugin;

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

        public Turret blockedTurret;

        int playerHealAmount;
        int enemyHealAmount;

        public float playerDetectionRange;
        public float enemyDetectionRange;
        float rangeMultiplier = 1f;
        float followingRange;
        float huggingRange = 5f;

        bool walking = false;
        bool hugging = false;
        bool dancing = false;

        const int maxCandy = 3;
        int candyEaten;

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

                if (healingBuffTime <= 0.5f)
                {
                    candyEaten = 0;
                }
            }

            if (hyperTime > 0f)
            {
                hyperTime -= Time.deltaTime;
            }

            if (targetPlayer != null)
            {
                turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }

            var state = currentBehaviourStateIndex;

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (state != (int)State.Hyper && state != (int)State.Healing)
                {
                    if (agent.remainingDistance <= agent.stoppingDistance + 0.1f && agent.velocity.sqrMagnitude < 0.01f)
                    {
                        if (walking)
                        {
                            walking = false;
                            DoAnimationClientRpc("stopWalking");
                        }
                    }
                    else
                    {
                        if (!walking)
                        {
                            walking = true;
                            DoAnimationClientRpc("startWalking");
                        }
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
                    agent.stoppingDistance = 0f;
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

                    break;

                case (int)State.Blocking:
                    agent.speed = 20f;
                    agent.stoppingDistance = 0f;
                    if (!MoveInFrontOfTurret())
                    {
                        logger.LogDebug("Stop Blocking");
                        blockedTurret = null;
                        SwitchToBehaviourClientRpc((int)State.Following);
                        return;
                    }
                    break;

                case (int)State.Healing:
                    agent.speed = 10f;
                    agent.stoppingDistance = huggingRange;
                    if (!hugging) { DoAnimationClientRpc("startHugging"); hugging = true; }
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
        }

        public bool TargetClosestEntity()
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
                    DoAnimationClientRpc("startDancing");
                    dancing = true;
                }
                else if (dancing && !IsNearbyPlayerEmoting(followingRange) && !walking)
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

        void MoveToHealTarget()
        {
            if (targetPlayer != null)
            {
                logger.LogDebug("Player hp: " + targetPlayer.health);
                if (targetPlayer.health >= 100 || targetPlayer.isPlayerDead)
                {
                    if (hugging) { hugging = false; DoAnimationClientRpc("stopHugging"); }
                    SwitchToBehaviourClientRpc((int)State.Following);
                    return;
                }
                if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= huggingRange)
                {
                    HealPlayer(targetPlayer);
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
                    if (hugging) { hugging = false; DoAnimationClientRpc("stopHugging"); }
                    return;
                }
                if (Vector3.Distance(transform.position, targetEnemy.transform.position) < huggingRange)
                {
                    HealEnemy(targetEnemy);
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
                        if (item.itemProperties.itemName == "SCP-559") { ChangeSizeClientRpc(0.5f); } // TODO: Test this
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
            DoAnimationClientRpc("startHyperDancing");
            dancing = true;
            StartSearch(transform.position);
            targetPlayer = null;
            targetEnemy = null;
        }

        public override void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            base.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            base.OnCollideWithPlayer(other);
            //logger.LogDebug("Collided with player");

            if (timeSinceHealing > 1f)
            {
                PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);

                HealPlayer(player);
            }

            return;
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

            return;
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
        {
            return;
        }

        public bool IsNearbyPlayerEmoting(float distance)
        {
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (Vector3.Distance(transform.position, player.transform.position) < distance && player.performingEmote) { return true; }
            }
            return false;
        }

        public bool HealPlayer(PlayerControllerB player)
        {
            if (player != null && player.health < 100 && timeSinceHealing > 1f)
            {
                logger.LogDebug("Healing player: " + player.playerUsername);

                int newHealthAmount;
                if (healingBuffTime > 0f) { newHealthAmount = player.health + (playerHealAmount * 2); }
                else { newHealthAmount = player.health + playerHealAmount; }

                if (newHealthAmount > 100) { newHealthAmount = 100; }

                HealPlayerClientRpc(player.actualClientId, newHealthAmount);
                timeSinceHealing = 0f;
                return true;
            }
            return false;
        }

        public bool HealEnemy(EnemyAI enemyToHeal)
        {
            SpawnableEnemyWithRarity spawnableEnemy = RoundManager.Instance.currentLevel.Enemies.Where(x => x.enemyType.enemyName == enemyToHeal.enemyType.enemyName).FirstOrDefault();
            if (spawnableEnemy == null) { logger.LogError("Enemy not found: " + enemyToHeal.enemyType.enemyName); return false; }

            int maxHealth = spawnableEnemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

            if (enemyToHeal.enemyHP < maxHealth && timeSinceHealing > 1f)
            {

                if (healingBuffTime > 0f) { enemyToHeal.enemyHP += enemyHealAmount * 2; }
                else { enemyToHeal.enemyHP += enemyHealAmount; }

                logger.LogDebug($"{enemyToHeal.enemyType.enemyName} HP: {enemyToHeal.enemyHP}/{maxHealth}");

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
        private void HealPlayerClientRpc(ulong clientId, int newHealthAmount)
        {
            PlayerControllerB player = StartOfRound.Instance.allPlayerScripts.Where(x => x.actualClientId == clientId).FirstOrDefault();
            if (player == null) { return; }

            player.JumpToFearLevel(0f, false); // TODO: Test this

            player.health = newHealthAmount;
            HUDManager.Instance.UpdateHealthUI(newHealthAmount, false);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayerTookDamageServerRpc(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                PlayerControllerB player = StartOfRound.Instance.allPlayerScripts.Where(x => x.actualClientId == clientId).FirstOrDefault();
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
        public void EnemyTookDamageServerRpc()
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                if (hyperTime > 0f) { return; }
                if (currentSearch != null) { StopSearch(currentSearch); }
                SwitchToBehaviourClientRpc((int)State.Healing);
            }
        }

        [ClientRpc]
        private void ChangeSizeClientRpc(float size) // TODO: Test this
        {
            transform.localScale = new Vector3(size, size, size);
        }

        [ServerRpc(RequireOwnership = false)]
        public void BlockTurretFireServerRpc()
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                Turret turretFiring;

                foreach (Turret turret in UnityEngine.Object.FindObjectsOfType<Turret>())
                {
                    if (turret.targetPlayerWithRotation == targetPlayer)
                    {
                        blockedTurret = turret;
                        SwitchToBehaviourClientRpc((int)State.Blocking);
                        return;
                    }
                }
            }
        }
    }
}