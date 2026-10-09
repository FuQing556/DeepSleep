using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using UnityEngine;

namespace DeepSleep.Runtime.Presentation.Audio
{
    /// <summary>特殊敌人池的材质/动作音，实例创建时订阅，不逐帧扫描或重复通用死亡音。</summary>
    public sealed class EnemyContentAudio2D : MonoBehaviour
    {
        public EnemyActorPool2D Pool;
        public CombatAudioPresenter Audio;
        public bool Download;
        public bool Recursive, RecursiveSplits, QuickApp;
        private readonly List<Action> _unsubscribe = new();
        private void OnEnable()
        {
            if (Pool == null || Audio == null) { Debug.LogError("[EnemyContentAudio] Missing explicit pool/audio", this); return; }
            Pool.ActorCreated += Bind;
            Pool.ActorDespawned += Despawn;
            Pool.ActorContactImpacted += Despawn;
            foreach (var actor in Pool.Instances) Bind(actor);
        }
        private void Bind(EnemyActor2D actor)
        {
            if (Recursive)
            {
                actor.Health.DamageAccepted += RecursiveHit;
                _unsubscribe.Add(() => { if (actor != null) actor.Health.DamageAccepted -= RecursiveHit; });
            }
            else if (QuickApp) { }
            else if (Download)
            {
                var motor = actor.GetComponent<DownloadChargeMotor2D>();
                if (motor == null) return;
                motor.StateEntered += State;
                _unsubscribe.Add(() => { if (motor != null) motor.StateEntered -= State; });
            }
            else
            {
                var shield = actor.GetComponentInChildren<PlayerAttackBlocker2D>(true);
                if (shield == null) return;
                shield.Blocked += Block;
                _unsubscribe.Add(() => { if (shield != null) shield.Blocked -= Block; });
            }
        }
        private void State(DownloadChargeState state, Vector2 point)
        {
            if (state == DownloadChargeState.Charging) Audio.PublishContentCue(AudioCue.DownloadCharge, point, .12f);
            else if (state == DownloadChargeState.Dashing) Audio.PublishContentCue(AudioCue.DownloadDash, point, .1f);
        }
        private void Block(Vector2 point) => Audio.PublishContentCue(AudioCue.GuardBlockMetal, point, .12f);
        private void RecursiveHit(DamagePacket packet) => Audio.PublishContentCue(AudioCue.RecursiveHit, packet.HitPoint, .16f);
        private void Despawn(EnemyDespawnRequest2D request)
        {
            if (request.Reason == EnemyDespawnReason.Defeated)
                Audio.PublishContentCue(Recursive ? (RecursiveSplits ? AudioCue.RecursiveSplit : AudioCue.RecursiveDefeat) :
                    QuickApp ? AudioCue.QuickAppDefeat : Download ? AudioCue.DownloadDefeat : AudioCue.GuardDefeat, request.EffectPosition, .12f);
            else if (request.Reason == EnemyDespawnReason.ContactImpact)
                Audio.PublishContentCue(Recursive ? AudioCue.RecursiveImpact : QuickApp ? AudioCue.QuickAppImpact :
                    Download ? AudioCue.DownloadImpact : AudioCue.GuardImpact, request.EffectPosition, .12f);
        }
        private void OnDisable()
        {
            if (Pool != null) { Pool.ActorCreated -= Bind; Pool.ActorDespawned -= Despawn; Pool.ActorContactImpacted -= Despawn; }
            foreach (var action in _unsubscribe) action();
            _unsubscribe.Clear();
        }
    }
}
