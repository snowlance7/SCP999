using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx.Logging;
using GameNetcodeStuff;
using LethalLib;
using SCP956;
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

namespace SCP999
{
    class SCP999AI : EnemyAI
    {
        private static ManualLogSource logger = LoggerInstance;

#pragma warning disable 0649
        public Transform turnCompass = null!;
#pragma warning restore 0649
        float timeSinceNewRandPos;

        float timeSinceHittingPlayer;

        enum State
        {
            Roamin,
            Following,
            Healing
        }

        public override void Start()
        {
            base.Start();
            logger.LogDebug("SCP-956 Spawned");

            timeSinceHittingPlayer = 0f;
            timeSinceNewRandPos = 0f;

            currentBehaviourStateIndex = (int)State.Roamin;
            RoundManager.Instance.SpawnedEnemies.Add(this);
        }

        public override void Update()
        {
            base.Update();

            timeSinceNewRandPos += Time.deltaTime;
            timeSinceHittingPlayer += Time.deltaTime;

            var state = currentBehaviourStateIndex;

            if (targetPlayer != null)
            {
                turnCompass.LookAt(targetPlayer.gameplayCamera.transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(0f, turnCompass.eulerAngles.y, 0f)), 10f * Time.deltaTime);
            }
        }

        public override void DoAIInterval()
        {
            logger.LogDebug("Do AI Interval");
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead)
            {
                return;
            }

            switch (currentBehaviourStateIndex)
            {
                case (int)State.Roamin:
                    agent.speed = 0f;
                    if (TargetFrozenPlayerInRange(config956ActivationRadius.Value))
                    {
                        logger.LogDebug("Start Killing Player");
                        SwitchToBehaviourClientRpc((int)State.MovingTowardsPlayer);
                        return;
                    }
                    if (configSecretLab.Value && timeSinceRandTeleport > config956TeleportTime.Value) // TODO: Test this more
                    {
                        logger.LogDebug("Teleporting");
                        Vector3 pos = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(transform.position, config956TeleportRange.Value, RoundManager.Instance.navHit, RoundManager.Instance.AnomalyRandom);
                        Teleport(pos);
                        timeSinceRandTeleport = 0;
                    }
                    break;

                case (int)State.Following:
                    agent.speed = 0.5f;
                    timeSinceRandTeleport = 0;
                    if (!TargetFrozenPlayerInRange(config956ActivationRadius.Value))
                    {
                        logger.LogDebug("Stop Killing Players");
                        SwitchToBehaviourClientRpc((int)State.Dormant);
                        return;
                    }
                    MoveToPlayer();
                    break;

                case (int)State.Healing:

                    break;
                default:
                    logger.LogWarning("Unhandled State");
                    break;
            }
        }

        public IEnumerator HeadbuttAttack()
        {
            SwitchToBehaviourClientRpc((int)State.HeadButtAttackInProgress);
            PlayerControllerB player = targetPlayer;
            Vector3 playerPos = player.transform.position;

            yield return new WaitForSeconds(3f);
            logger.LogDebug("Headbutting");
            DoAnimationClientRpc("headButt");

            yield return new WaitForSeconds(0.5f);
            logger.LogDebug($"Damaging player: {targetPlayer.playerUsername}");
            DamageTargetPlayerClientRpc(player.actualClientId);
            creatureSFX.PlayOneShot(BoneCracksfx);

            yield return new WaitForSeconds(0.5f);

            if (player.isPlayerDead)
            {
                creatureVoice.PlayOneShot(PlayerDeathsfx);

                logger.LogDebug("Player died, spawning candy");
                int candiesCount = UnityEngine.Random.Range(configCandyMinSpawn.Value, configCandyMaxSpawn.Value);

                for (int i = 0; i < candiesCount; i++)
                {
                    Vector3 pos = RoundManager.Instance.GetRandomNavMeshPositionInRadius(playerPos, 1.5f, RoundManager.Instance.navHit);
                    NetworkHandler.Instance.SpawnItemServerRpc(0, CandyNames[UnityEngine.Random.Range(0, CandyNames.Count)], 0, pos, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 361f), 0f), false, true);
                }

                //NetworkHandler.Instance.FrozenPlayers.Remove(player.actualClientId);
                targetPlayer = null;
            }
            if (currentBehaviourStateIndex != (int)State.HeadButtAttackInProgress)
            {
                yield break;
            }
            SwitchToBehaviourClientRpc((int)State.MovingTowardsPlayer);
        }

        bool TargetFrozenPlayerInRange(float range)
        {
            //SwitchToBehaviourServerRpc((int)State.HeadButtAttackInProgress); // TODO: Use this
            targetPlayer = null;


            /*if (NetworkHandler.Instance.FrozenPlayers == null) { return false; } // TODO: Check chatgpt and learn more about making freezing players more modular and decoupleable
            if (NetworkHandler.Instance.FrozenPlayers.Count > 0)
            {
                foreach (ulong id in NetworkHandler.Instance.FrozenPlayers)
                {
                    PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[StartOfRound.Instance.ClientPlayerList[id]];
                    if (player == null || player.disconnectedMidGame || player.isPlayerDead || !player.isPlayerControlled) { NetworkHandler.Instance.FrozenPlayers.Remove(id); continue; }
                    if (Vector3.Distance(transform.position, player.transform.position) < range && PlayerIsTargetable(player))
                    {
                        targetPlayer = player;
                    }
                }
            }
            if (PlayerControllerBPatch.playerFrozen)
            {
                targetPlayer = localPlayer;
            }*/


            return targetPlayer != null;
        }

        void MoveToPlayer()
        {
            if (targetPlayer == null)
            {
                return;
            }
            if (Vector3.Distance(transform.position, targetPlayer.transform.position) <= 3f)
            {
                logger.LogDebug("Headbutt Attack");
                StartCoroutine(HeadbuttAttack());
                return;
            }

            if (timeSinceNewRandPos > 1.5f)
            {
                timeSinceNewRandPos = 0;
                Vector3 positionInFrontPlayer = (targetPlayer.transform.forward * 2.9f) + targetPlayer.transform.position;
                SetDestinationToPosition(positionInFrontPlayer, checkForPath: false);
            }
        }

        public override void OnCollideWithPlayer(Collider other)
        {
            if (!(timeSinceHittingPlayer < 0.5f))
            {
                PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
                if (playerControllerB != null)
                {
                    timeSinceHittingPlayer = 0f;
                    playerControllerB.DamagePlayer(10, hasDamageSFX: true, callRPC: true, CauseOfDeath.Stabbing);
                    //HitPlayerServerRpc();
                }
            }

            return;
        }

        public override void HitFromExplosion(float distance)
        {
            base.HitFromExplosion(distance);
            KillEnemy(true);
        }

        public override void HitEnemy(int force = 0, PlayerControllerB playerWhoHit = null, bool playHitSFX = true, int hitID = -1)
        {
            base.HitEnemy(0, playerWhoHit, playHitSFX, hitID);
        }

        public void Teleport(Vector3 teleportPos)
        {
            serverPosition = teleportPos;
            transform.position = teleportPos;
            agent.Warp(teleportPos);
            SyncPositionToClients();
        }

        public bool IsAnyPlayerLookingAtMe()
        {
            foreach (PlayerControllerB player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player.isPlayerControlled && player.HasLineOfSightToPosition(transform.position, 45f, 60, config956SpawnRadius.Value))
                {
                    return true;
                }
            }
            return false;
        }

        // RPC's

        [ServerRpc(RequireOwnership = false)]
        private void TargetPlayerServerRpc(ulong clientId)
        {
            /*if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                
            }*/
        }

        [ClientRpc]
        private void DoAnimationClientRpc(string animationName)
        {
            logger.LogDebug("Animation: " + animationName);
            creatureAnimator.SetTrigger(animationName);
        }

        [ClientRpc]
        private void DamageTargetPlayerClientRpc(ulong clientId)
        {
            PlayerControllerB player = StartOfRound.Instance.localPlayerController;
            if (player.actualClientId == clientId)
            {
                player.DamagePlayer(configHeadbuttDamage.Value);

                if (player.isPlayerDead) { PlayerControllerBPatch.playerFrozen = false; }
            }
        }
    }
}
// TODO: Death animation