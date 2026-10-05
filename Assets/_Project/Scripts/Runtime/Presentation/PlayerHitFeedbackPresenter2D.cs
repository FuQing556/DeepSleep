using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.World.Cameras;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.Presentation
{
    /// <summary>仅由实际扣血事实触发；独立覆盖精灵，不修改物理根、动作或网络姿态。</summary>
    [DefaultExecutionOrder(300)]
    public sealed class PlayerHitFeedbackPresenter2D : MonoBehaviour
    {
        public PlayerActor Actor;
        public PlayerDamageReceiver2D Receiver;
        public CoopSessionController Session;
        public PlayerControlAssignment Assignment;
        public CameraHorizontalLookAhead2D CameraFeedback;
        public SpriteRenderer BodySprite, HitOverlay;
        public PlayerCombatHudView Hud;
        public Image HudFlash;
        private float _elapsed = 100f, _protection;
        private bool _subscribed;
        public uint PlayedCount { get; private set; }
        public bool IsPlaying => _elapsed < .2f || _protection > 0f;
        public float RemainingProtection => _protection;

        public bool TryValidateConfiguration(out string reason)
        {
            reason = Actor == null || Receiver == null || Session == null || Assignment == null ||
                CameraFeedback == null || BodySprite == null || HitOverlay == null || Hud == null || HudFlash == null
                ? "角色受击表现引用不完整。" : string.Empty;
            return reason.Length == 0;
        }

        private void OnEnable()
        {
            if (!TryValidateConfiguration(out string reason))
            { Debug.LogError("[PlayerHitFeedback] " + reason, this); enabled=false; return; }
            Receiver.DamageAccepted += OnAccepted;
            Receiver.FeedbackReset += ResetFeedback;
            _subscribed=true;
            ResetFeedback();
        }

        private void OnDisable()
        {
            if (_subscribed)
            {
                Receiver.DamageAccepted -= OnAccepted;
                Receiver.FeedbackReset -= ResetFeedback;
                _subscribed=false;
            }
            ResetFeedback();
        }

        private void OnAccepted(PlayerDamageReceiver2D receiver, DamagePacket damage)
        {
            if (Session.Phase == SessionPhase.Playing && !Session.IsAuthority) return;
            PlayReplica(damage.Direction, receiver.RemainingInvulnerabilitySeconds);
        }

        public void PlayReplica(Vector2 direction, float protectionSeconds)
        {
            if (!isActiveAndEnabled) return;
            PlayedCount++;
            _elapsed=0f;
            _protection=Mathf.Clamp(protectionSeconds,0f,10f);
            bool local=Session.Phase==SessionPhase.Playing
                ? Session.LocalRole==Actor.Definition.Role : Assignment.CurrentLocalPlayerRole==Actor.Definition.Role;
            if (local)
            {
                CameraFeedback.ShakeStrength=PlayerHitFeedbackOptions.ShakeStrength;
                CameraFeedback.PlayHit(direction);
            }
            RenderNow();
        }

        public void ResetFeedback()
        {
            _elapsed=100f;_protection=0f;
            if(HitOverlay!=null)HitOverlay.enabled=false;
            if(HudFlash!=null)HudFlash.enabled=false;
            if(CameraFeedback!=null && Actor!=null && Actor.Definition!=null && Assignment!=null &&
                (Session!=null && Session.Phase==SessionPhase.Playing
                    ? Session.LocalRole==Actor.Definition.Role : Assignment.CurrentLocalPlayerRole==Actor.Definition.Role))
                CameraFeedback.ResetHit();
        }

        private void LateUpdate()
        {
            // 复活/检查点恢复不沿用上一条普通受击闪光。客人读取权威 HUD 状态。
            if(Hud.SourceComponent is IPlayerCombatHudSource source && source.TryRead(out var state) && state.ReviveProtection)
            { ResetFeedback(); return; }
            _elapsed+=Time.deltaTime;
            _protection=Mathf.Max(0,_protection-Time.deltaTime);
            RenderNow();
        }

        public void RenderNow()
        {
            bool reduced=PlayerHitFeedbackOptions.ReduceFlash;
            float flash=Mathf.Clamp01(1f-_elapsed/.16f);
            // 无敌提示采用持续低强度色层，不反复开关角色造成频闪/隐身。
            float alpha=Mathf.Max(flash*(reduced?.18f:.65f),_protection>0f?(reduced?.035f:.075f):0f);
            HitOverlay.enabled=alpha>0f && BodySprite.enabled && BodySprite.gameObject.activeInHierarchy;
            if(HitOverlay.enabled)
            {
                HitOverlay.sprite=BodySprite.sprite;
                HitOverlay.flipX=BodySprite.flipX;HitOverlay.flipY=BodySprite.flipY;
                // Installer 保证同父节点，继承同一 SortingGroup/动作层，不写原视觉 Transform。
                HitOverlay.transform.localPosition=BodySprite.transform.localPosition;
                HitOverlay.transform.localRotation=BodySprite.transform.localRotation;
                HitOverlay.transform.localScale=BodySprite.transform.localScale;
                HitOverlay.sortingLayerID=BodySprite.sortingLayerID;
                HitOverlay.sortingOrder=BodySprite.sortingOrder+2;
                HitOverlay.color=new Color(1f,.72f,.65f,alpha*BodySprite.color.a);
            }
            HudFlash.enabled=_elapsed<.22f;
            HudFlash.color=new Color(1f,.45f,.3f,Mathf.Clamp01(1f-_elapsed/.22f)*(reduced?.16f:.42f));
        }
    }
}
