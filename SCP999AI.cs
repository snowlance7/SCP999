using BepInEx.Logging;
using GameNetcodeStuff;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using static SCP999.Plugin;

namespace SCP999
{
    public class SCP999AI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public Transform turnCompass = null!;
        public ScanNodeProperties ScanNode = null!;
        public List<AudioClip> hitSFXList = null!;
        public List<AudioClip> hugSFXList = null!;
        public List<AudioClip> hurtSFXList = null!;
        public List<AudioClip> roamSFXList = null!;
        public InteractTrigger jarTrigger = null!;
        public ulong lizzieSteamId;
        public ulong snowySteamId;
#pragma warning restore 0649

        readonly static int hugAnim = Animator.StringToHash("hugging");
        readonly static int stretchAnim = Animator.StringToHash("stretching");
        readonly static int hyperAnim = Animator.StringToHash("hyperDancing");
        readonly static int walkAnim = Animator.StringToHash("walking");
        readonly static int danceAnim = Animator.StringToHash("dancing");


        EnemyAI? targetEnemy;
        Turret? blockedTurret;

        float timeSinceHealing;
        float healingBuffTime;
        float hyperTime;
        float timeSinceBlockSFX;
        float timeSinceHugSFX;
        float hugTime;

        int playerHealAmount;
        int enemyHealAmount;

        float playerDetectionRange;
        float enemyDetectionRange;
        float rangeMultiplier = 1f;
        float followingRange;
        float huggingRange;

        bool walking;
        bool hugging;

        int maxCandy;
        int candyEaten;
        bool gettingInJar;

        bool followPlayer = true;
        bool followEnemy = true;

        bool tamed = false;
        PlayerControllerB tamedByPlayer = null!;

        float insanityDecreaseRate;
        private bool dancing;

        public enum State
        {
            Roaming,
            Following,
            Blocking,
            Healing,
            Hyper
        }

        public void SwitchToBehaviourStateCustom(State state)
        {

            switch (state)
            {
                case State.Roaming:
                    SetTargetNull();
                    StartSearch(transform.position);

                    break;
                case State.Following:
                    StopSearch(currentSearch);

                    break;
                case State.Blocking:
                    StopSearch(currentSearch);
                    DoAnimationClientRpc(stretchAnim, true);

                    break;
                case State.Healing:
                    StopSearch(currentSearch);

                    break;
                case State.Hyper:
                    tamed = false;
                    tamedByPlayer = null!;
                    SetTargetNull();
                    StartSearch(transform.position);
                    DoAnimationClientRpc(walkAnim, false);
                    DoAnimationClientRpc(hyperAnim, true);

                    break;
                default:
                    break;
            }

            SwitchToBehaviourClientRpc((int)state);
        }

        public override void Start()
        {
            base.Start();
            logIfDebug("SCP-999 Spawned");

            playerHealAmount = configPlayerHealAmount.Value;
            enemyHealAmount = configEnemyHealAmount.Value;
            playerDetectionRange = configPlayerDetectionRange.Value;
            enemyDetectionRange = configEnemyDetectionRange.Value;
            followingRange = configFollowRange.Value;
            huggingRange = configHuggingRange.Value;
            maxCandy = configMaxCandy.Value;
            insanityDecreaseRate = configInsanityDecreaseRate.Value;

            currentBehaviourStateIndex = (int)State.Roaming;

            if (!RoundManager.Instance.SpawnedEnemies.Contains(this))
            {
                RoundManager.Instance.SpawnedEnemies.Add(this);
            }

            if (IsServerOrHost)
            {
                if (transform.localScale.y != config999Size.Value)
                {
                    ChangeSizeClientRpc(config999Size.Value);
                }

                SetOutsideOrInside();
                StartSearch(transform.position);
            }
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
            }

            if (hyperTime > 0f)
            {
                hyperTime -= Time.deltaTime;
            }

            bool _hugging = hugTime > 0f;
            if (_hugging)
            {
                hugTime -= Time.deltaTime;
            }
            if (hugging != _hugging)
            {
                creatureAnimator.SetBool(hugAnim, _hugging);
                hugging = _hugging;
            }

            if (targetPlayer != null && currentBehaviourStateIndex == (int)State.Blocking)
            {
                turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }

            if (localPlayer.currentlyHeldObjectServer != null
                && localPlayer.currentlyHeldObjectServer.itemProperties.name == "ContainmentJarItem"
                && localPlayer.currentlyHeldObjectServer.GetComponent<ContainmentJarBehavior>().JarContents == ContainmentJarBehavior.Contents.Empty)
            {
                jarTrigger.interactable = true;
            }
            else
            {
                jarTrigger.interactable = false;
            }

            if (currentBehaviourStateIndex == (int)State.Following || currentBehaviourStateIndex == (int)State.Roaming)
            {
                bool _walking = agent.velocity.sqrMagnitude >= 0.01f; // TODO: Check if this causes errors on clients

                if (walking != _walking)
                {
                    creatureAnimator.SetBool(walkAnim, _walking);
                    walking = _walking;
                }
            }
        }

        public override void DoAIInterval()
        {
            if (moveTowardsDestination)
            {
                agent.SetDestination(destination);
            }

            if (isEnemyDead || StartOfRound.Instance.allPlayersDead || gettingInJar)
            {
                return;
            };

            if (BabyIsCryingNearby()) { return; }
            if (tamed && tamedByPlayer != null) { targetPlayer = tamedByPlayer; }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roaming:
                    agent.speed = 5f;
                    agent.stoppingDistance = 0f;
                    agent.acceleration = 8f;
                    if ((TargetClosestPlayer(1.5f, true) && followPlayer) || (TargetClosestEnemy(1.5f, true) && followEnemy))
                    {
                        logIfDebug("Start Targeting");
                        SwitchToBehaviourStateCustom(State.Following);
                        break;
                    }
                    break;

                case (int)State.Following:
                    if (healingBuffTime > 0f) { agent.speed = 10f; } else { agent.speed = 5f; }
                    agent.stoppingDistance = followingRange;
                    agent.acceleration = 10f;

                    if (!tamed)
                    {
                        if (!TargetClosestEntity())
                        {
                            logIfDebug("Stop Targeting");
                            SwitchToBehaviourStateCustom(State.Roaming);
                            return;
                        }
                    }

                    if (tamed && tamedByPlayer != null && isOutside == tamedByPlayer.isInsideFactory)
                    {
                        Vector3 pos = RoundManager.Instance.GetNavMeshPosition(tamedByPlayer.transform.position);
                        agent.Warp(pos);
                        SetEnemyOutsideClientRpc(!tamedByPlayer.isInsideFactory);
                    }
                    
                    if (MoveToSweetsIfDroppedByPlayer())
                    {
                        EatSweetsIfClose();
                        return;
                    }

                    bool _dancing = IsNearbyPlayerEmoting(followingRange);
                    if (dancing != _dancing)
                    {
                        DoAnimationClientRpc(danceAnim, _dancing);
                        dancing = _dancing;
                    }

                    FollowTarget();

                    break;

                case (int)State.Blocking:
                    agent.speed = 20f;
                    agent.stoppingDistance = 0f;
                    agent.acceleration = 15f;
                    if (!MoveInFrontOfTurret())
                    {
                        logIfDebug("Stop Blocking");
                        SwitchToBehaviourStateCustom(State.Following);
                        blockedTurret = null;
                        DoAnimationClientRpc(stretchAnim, false);
                        return;
                    }

                    break;

                case (int)State.Healing:
                    agent.speed = 10f;
                    agent.stoppingDistance = huggingRange;
                    agent.acceleration = 50f;

                    MoveToHealTarget();
                    break;

                case (int)State.Hyper:
                    agent.speed = 20f;
                    agent.stoppingDistance = 0f;
                    agent.acceleration = 25f;
                    
                    if (hyperTime <= 0f)
                    {
                        candyEaten = 0;
                        SwitchToBehaviourStateCustom(State.Roaming);
                        DoAnimationClientRpc(hyperAnim, false);
                        return;
                    }
                    break;

                default:
                    logger.LogWarning("Invalid state: " + currentBehaviourStateIndex);
                    break;
            }
        }

        private bool BabyIsCryingNearby()
        {
            CaveDwellerAI baby = RoundManager.Instance.SpawnedEnemies.OfType<CaveDwellerAI>().Where(x => x.babyCrying && Vector3.Distance(transform.position, x.transform.position) < x.babyCryingAudio.maxDistance).FirstOrDefault();

            if (baby != null)
            {
                agent.speed = 10f;
                agent.stoppingDistance = 0f;
                agent.acceleration = 50f;
                targetEnemy = baby;
                return SetDestinationToPosition(baby.transform.position, true);
            }
            return false;
        }

        public void SetTamed(PlayerControllerB playerTamedTo)
        {
            tamed = true;
            tamedByPlayer = playerTamedTo;
            targetPlayer = playerTamedTo;

            if (playerTamedTo.playerSteamId == snowySteamId)
            {
                SetScanNodeClientRpc("Following my creator");
            }
            else if (playerTamedTo.playerSteamId == lizzieSteamId)
            {
                SetScanNodeClientRpc("Following my best friend lizzie <3");
            }
            else
            {
                SetScanNodeClientRpc($"Following {playerTamedTo.playerUsername}");
            }

            SwitchToBehaviourStateCustom(State.Following);
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

            GameObject[] nodes = inside ? Utils.GetInsideAINodes() : Utils.GetOutsideAINodes();

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

        public void GetInJar()
        {
            logIfDebug("GetInJar");
            GetInJarServerRpc(localPlayer.actualClientId);
        }

        public bool TargetClosestEntity()
        {
            //logIfDebug("Targetting closest entitiy");
            if (targetPlayer != null && followPlayer && TargetClosestPlayer() && (Vector3.Distance(transform.position, targetPlayer.transform.position) < playerDetectionRange * 2 || CheckLineOfSightForPosition(targetPlayer.transform.position))) { return true; }
            else if (targetEnemy != null && followEnemy && TargetClosestEnemy(5f) && (Vector3.Distance(transform.position, targetEnemy.transform.position) < enemyDetectionRange * 2 || CheckLineOfSightForPosition(targetEnemy.transform.position))) { return true; }
            else { return false; }
        }

        public bool TargetClosestEnemy(float bufferDistance = 1.5f, bool requireLineOfSight = false, float viewWidth = 70f)
        {
            mostOptimalDistance = 2000f;
            EnemyAI? previousTarget = targetEnemy;
            targetEnemy = null;
            foreach (EnemyAI enemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (enemy.isEnemyDead) { continue; }
                if (enemy.enemyType.name == "SCP999Enemy") { continue; }
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

            if (tamed)
            {
                pos = tamedByPlayer.transform.position;
            }
            else if (targetPlayer != null)
            {
                pos = targetPlayer.transform.position;
            }
            else if (targetEnemy != null)
            {
                pos = targetEnemy.transform.position;
            }
            else { return; }

            SetDestinationToPosition(pos);
        }

        void SetTarget(PlayerControllerB player)
        {
            targetPlayer = player;
            targetEnemy = null;
        }

        void SetTarget(EnemyAI enemy)
        {
            targetPlayer = null;
            targetEnemy = enemy;
        }

        void SetTargetNull()
        {
            targetPlayer = null;
            targetEnemy = null;
        }

        void MoveToHealTarget()
        {
            if (targetPlayer != null)
            {
                //logIfDebug("Player hp: " + targetPlayer.health);
                if (targetPlayer.health >= 100 || targetPlayer.isPlayerDead)
                {
                    SwitchToBehaviourStateCustom(State.Following);
                    return;
                }

                SetDestinationToPosition(targetPlayer.transform.position, false);
            }
            else if (targetEnemy != null)
            {
                int maxHealth = targetEnemy.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;
                //logIfDebug("Enemy hp: " + targetEnemy.enemyHP + "/" + maxHealth);

                if (targetEnemy.enemyHP >= maxHealth || targetEnemy.isEnemyDead)
                {
                    SwitchToBehaviourStateCustom(State.Following);
                    return;
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
                            logIfDebug("Playing hitSFX");
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
            try
            {
                foreach (GameObject obj in HoarderBugAI.grabbableObjectsInMap.ToList())
                {
                    if (obj == null)
                    {
                        HoarderBugAI.grabbableObjectsInMap.Remove(obj);
                        continue;
                    }
                    GrabbableObject item = obj.GetComponentInChildren<GrabbableObject>();
                    if (item == null) { continue; }
                    if (Vector3.Distance(transform.position, item.transform.position) <= followingRange)
                    {
                        if (Sweets.Contains(item.itemProperties.itemName) && item.hasBeenHeld && !item.heldByPlayerOnServer)
                        {
                            logIfDebug("Moving to item: " + item.itemProperties.itemName);
                            agent.stoppingDistance = 0f;
                            SetDestinationToPosition(item.transform.position, false);
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void EatSweetsIfClose()
        {
            try
            {
                foreach (GameObject obj in HoarderBugAI.grabbableObjectsInMap.ToList())
                {
                    if (obj == null)
                    {
                        HoarderBugAI.grabbableObjectsInMap.Remove(obj);
                        continue;
                    }
                    GrabbableObject item = obj.GetComponentInChildren<GrabbableObject>();
                    if (item == null) { continue; }
                    if (Vector3.Distance(transform.position, item.transform.position) < 1f)
                    {
                        if (Sweets.Contains(item.itemProperties.itemName))
                        {
                            logIfDebug("Eating item: " + item.itemProperties.itemName);
                            if (item.itemProperties.itemName == "SCP-559") { ChangeSizeClientRpc(transform.localScale.y / 2); }
                            if (item.itemProperties.itemName == "Black Candy") { MakeHyper(60f); }
                            if (item.itemProperties.itemName == "Cake") { healingBuffTime += 10f; }

                            item.NetworkObject.Despawn(true);

                            candyEaten += 1;
                            logIfDebug("Candy eaten: " + candyEaten);
                            healingBuffTime += 20f;

                            if (candyEaten >= maxCandy) { MakeHyper(30f); }
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }

        public void MakeHyper(float duration)
        {
            hyperTime += duration;
            SwitchToBehaviourStateCustom(State.Hyper);
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

            PlayerControllerB player = MeetsStandardPlayerCollisionConditions(other);

            if (player != null)
            {
                HealPlayer(player);
                Hug();
            }
        }
        public new PlayerControllerB MeetsStandardPlayerCollisionConditions(Collider other, bool inKillAnimation = false, bool overrideIsInsideFactoryCheck = false)
        {
            if (isEnemyDead)
            {
                return null;
            }
            if (!ventAnimationFinished)
            {
                return null;
            }
            if (inKillAnimation)
            {
                return null;
            }
            if (stunNormalizedTimer >= 0f)
            {
                return null;
            }
            PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
            if (component == null)
            {
                return null;
            }
            if (!PlayerIsTargetable(component, cannotBeInShip: false, overrideIsInsideFactoryCheck))
            {
                Debug.Log("Player is not targetable");
                return null;
            }
            return component;
        }

        public override void OnCollideWithEnemy(Collider other, EnemyAI? collidedEnemy = null)
        {
            base.OnCollideWithEnemy(other, collidedEnemy);

            if (collidedEnemy != null && collidedEnemy.enemyType.name != "SCP999Enemy")
            {
                HealEnemy(collidedEnemy);
                Hug();
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

        public void Hug()
        {
            if (currentBehaviourStateIndex == (int)State.Following || currentBehaviourStateIndex == (int)State.Healing)
            {
                hugTime = 1f;
                if (timeSinceHugSFX > 5f)
                {
                    int randomIndex = Random.Range(0, hugSFXList.Count - 1);
                    creatureVoice.PlayOneShot(hugSFXList[randomIndex], 1f);
                    timeSinceHugSFX = 0f;
                }
            }
        }

        public void HealPlayer(PlayerControllerB player)
        {
            if (player != null && timeSinceHealing > 1f)
            {
                timeSinceHealing = 0f;
                if (player.health < 100)
                {
                    //logIfDebug("Healing player: " + player.playerUsername);

                    int newHealthAmount;
                    if (healingBuffTime > 0f) { newHealthAmount = player.health + (playerHealAmount * 2); }
                    else { newHealthAmount = player.health + playerHealAmount; }

                    if (newHealthAmount > 100) { newHealthAmount = 100; }

                    player.health = newHealthAmount;
                    HUDManager.Instance.UpdateHealthUI(player.health, false);
                }

                player.insanityLevel -= insanityDecreaseRate;
                player.JumpToFearLevel(0f, false);
            }
        }

        public void HealEnemy(EnemyAI enemyToHeal)
        {
            if (timeSinceHealing > 1f)
            {
                timeSinceHealing = 0f;
                int maxHealth = enemyToHeal.enemyType.enemyPrefab.GetComponent<EnemyAI>().enemyHP;

                //logIfDebug($"{enemyToHeal.enemyType.enemyName} HP: {enemyToHeal.enemyHP}/{maxHealth}");

                if (enemyToHeal.enemyHP < maxHealth)
                {

                    if (healingBuffTime > 0f) { enemyToHeal.enemyHP += enemyHealAmount * 2; }
                    else { enemyToHeal.enemyHP += enemyHealAmount; }
                }
            }

            if (enemyToHeal is CaveDwellerAI)
            {
                CaveDwellerAI baby = (CaveDwellerAI)enemyToHeal;
                if (baby.babyCrying)
                {
                    baby.rockBabyTimer += Time.deltaTime * 0.4f;

                    if (baby.rockBabyTimer > 1f)
                    {
                        baby.SetCryingLocalClient(false);
                        baby.SetBabyCryingServerRpc(false);
                    }
                }
            }
        }

        public void EnemyTookDamage(EnemyAI enemy)
        {
            if (hyperTime > 0f) { return; }
            if (tamed) { return; }

            targetEnemy = enemy;
            SwitchToBehaviourStateCustom(State.Healing);
        }

        // RPC's

        [ClientRpc]
        public void SetScanNodeClientRpc(string subText)
        {
            ScanNode.subText = subText;
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetTamedPlayerServerRpc(ulong clientId)
        {
            if (IsServerOrHost)
            {
                SetTamed(PlayerFromId(clientId));
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void DoAnimationServerRpc(int id, bool value)
        {
            if (IsServerOrHost)
            {
                DoAnimationClientRpc(id, value);
            }
        }

        [ClientRpc]
        private void DoAnimationClientRpc(int id, bool value)
        {
            //logIfDebug($"Setting {id} to {value}");
            creatureAnimator.SetBool(id, value);
        }

        [ServerRpc(RequireOwnership = false)]
        private void GetInJarServerRpc(ulong clientId)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                PlayerControllerB player = PlayerFromId(clientId);
                player.currentlyHeldObjectServer.GetComponent<ContainmentJarBehavior>().ChangeJarContentsClientRpc(ContainmentJarBehavior.Contents.SCP999);
                RoundManager.Instance.DespawnEnemyOnServer(this.NetworkObject);
            }
        }

        [ClientRpc]
        private void SetEnemyOutsideClientRpc(bool value)
        {
            SetEnemyOutside(value);
        }

        [ServerRpc(RequireOwnership = false)]
        public void PlayerTookDamageServerRpc(ulong clientId)
        {
            if (IsServerOrHost)
            {
                PlayerControllerB player = PlayerFromId(clientId);
                if (player == null) { return; }
                if (tamed && player != targetPlayer) { return; }

                float multiplier = 2 - (player.health / 100f);
                float range = configPlayerDetectionRange.Value * multiplier;

                if (Vector3.Distance(transform.position, player.transform.position) <= range)
                {
                    SetTarget(player);

                    if (hyperTime > 0f) { return; }
                    SwitchToBehaviourStateCustom(State.Healing);
                }
            }
        }

        [ClientRpc]
        private void ChangeSizeClientRpc(float size) // TODO: Test this
        {
            logIfDebug("Changing size to " + size);
            transform.localScale = new Vector3(size, size, size);
        }

        [ServerRpc(RequireOwnership = false)]
        public void BlockTurretFireServerRpc(NetworkObjectReference netObjRef)
        {
            if (IsServerOrHost)
            {
                if (netObjRef.TryGet(out NetworkObject networkObject))
                {
                    blockedTurret = networkObject.GetComponentInChildren<Turret>();
                    SwitchToBehaviourStateCustom(State.Blocking);
                }
            }
        }
    }
}