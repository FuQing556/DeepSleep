using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>普通怪物真实池租还/扣血，豆包隔离 Prefab 扣血；不保存场景、不消费钱包/档案。</summary>
    public static class EnemyHitFlashLocalChecks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Run in an offline gameplay scene during Play.");
            var world = Object.FindFirstObjectByType<NetworkWorldSnapshotChannel>();
            Require(world != null && world.Session.Phase == SessionPhase.Offline && !world.Session.HasPeer,
                "Offline gameplay scene is required.");
            float previousScale = Time.timeScale;
            int checks = 0;
            try
            {
                Time.timeScale = 0f;
                foreach (EnemyActorPool2D pool in world.EnemyPools)
                    checks += CheckPool(pool);
                checks += CheckBossPrefab();
                return "PASS: " + checks + " enemy flash local checks; " + world.EnemyPools.Length +
                    " real pools plus isolated Doubao prefab. Actual HP loss, no healing/invalid/reset flash, base visuals unchanged, " +
                    "pool reuse, first-frame hold, fade, lethal/replica pose. No scene/profile save or real network.";
            }
            finally { Time.timeScale = previousScale; }
        }

        private static int CheckPool(EnemyActorPool2D pool)
        {
            int checks = 0;
            var variation = new EnemySpawnVariation2D(Vector2.left, 1f, 0f, 0f);
            EnemyActor2D actor = null;
            try
            {
                Require(pool.TryRent(new Vector2(1000f, 1000f), variation, out actor), "Pool could not rent temporary enemy: " + pool.name);
                var flash = actor.GetComponent<SpriteHitFlash2D>();
                var hitbox = actor.GetComponentInChildren<DamageHitbox2D>();
                Require(flash != null && flash.TryValidateConfiguration(out _) && hitbox != null && actor.Health.MaximumHealth >= 2f,
                    "Installed enemy feedback/health missing: " + pool.name); checks++;
                var body = flash.Sources[0];
                Color color = body.color;
                Vector3 scale = body.transform.localScale;
                uint sequence = flash.Sequence, plays = flash.PlayedCount;
                float hp = actor.Health.CurrentHealth;
                var damage = new DamagePacket(1f, actor.transform.position, Vector2.left, null, 0,
                    DamageInterceptionPolicy.Unspecified, true);
                Require(hitbox.TryReceiveDamage(damage) && actor.Health.CurrentHealth == hp - 1f &&
                    flash.Sequence == Next(sequence) && flash.PlayedCount == plays + 1,
                    "Actual HP loss must trigger one short flash."); checks++;
                Require(flash.IsPlaying && flash.Overlay.enabled && flash.Overlay.sprite == body.sprite &&
                    Mathf.Approximately(flash.Overlay.color.a, (PlayerHitFeedbackOptions.ReduceFlash ? .18f : .65f) * color.a),
                    "Flash does not match current player palette/intensity."); checks++;
                Require(body.color == color && body.transform.localScale == scale &&
                    !flash.Sources.Contains(flash.Overlay), "Flash changed base visual or entered network base layers."); checks++;
                Invoke(flash, "AdvanceFrame", Time.frameCount, 1f);
                Require(flash.NormalizedAge == 0f && flash.IsPlaying && flash.Overlay.enabled,
                    "A hit was aged away before its first renderable frame despite a same-frame delta exceeding its duration."); checks++;
                uint once = flash.Sequence;
                var invalid = new DamagePacket(0f, Vector2.zero, Vector2.zero, null);
                Require(!hitbox.TryReceiveDamage(invalid) && flash.Sequence == once, "Invalid damage caused a flash."); checks++;
                Require(actor.Health.TryRestore(1f) && flash.Sequence == once, "Healing caused another hit flash."); checks++;
                actor.Health.ResetToMaximum();
                Require(!flash.IsPlaying && !flash.Overlay.enabled && flash.Sequence == once, "Life reset retained or emitted feedback."); checks++;
                Require(hitbox.TryReceiveDamage(damage), "Second accepted hit failed.");
                Invoke(flash, "AdvanceFrame", Time.frameCount + 1, .08f);
                Require(flash.Overlay.enabled && Mathf.Approximately(flash.NormalizedAge, .5f), "Half-duration fade failed."); checks++;
                Invoke(flash, "AdvanceFrame", Time.frameCount + 2, .08f);
                Require(!flash.IsPlaying && !flash.Overlay.enabled, "Expired flash remained visible."); checks++;
                uint beforeReset = flash.Sequence;
                Require(actor.TryRequestDespawn(EnemyDespawnReason.RunReset) && !actor.gameObject.activeSelf && !flash.Overlay.enabled,
                    "RunReset must return and clear an enemy without a death reward."); checks++;
                var recycled = actor;
                actor = null;
                Require(pool.TryRent(new Vector2(1000f, 1000f), variation, out actor) && actor == recycled,
                    "Expected real LIFO pool reuse."); checks++;
                Require(!flash.IsPlaying && !flash.Overlay.enabled && flash.Sequence == beforeReset,
                    "Pool reuse retained visual state or reset the hit sequence."); checks++;
                uint before = flash.PlayedCount;
                Require(hitbox.TryReceiveDamage(damage) && flash.PlayedCount == before + 1,
                    "Reuse duplicated/missed damage subscription."); checks++;
                actor.Health.RestoreCheckpointHealth(actor.Health.MaximumHealth);
                Require(!flash.IsPlaying && !flash.Overlay.enabled, "Checkpoint restore retained old flash."); checks++;
            }
            finally { if (actor != null && actor.gameObject.activeSelf) actor.TryRequestDespawn(EnemyDespawnReason.RunReset); }
            return checks;
        }

        private static int CheckBossPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Combat/Enemies/PF_EN_Doubao.prefab");
            Require(prefab != null, "Missing Doubao prefab.");
            GameObject root = Object.Instantiate(prefab);
            root.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var boss = root.GetComponent<DoubaoBoss2D>();
                var flash = root.GetComponent<SpriteHitFlash2D>();
                Require(boss != null && flash != null && flash.TryValidateConfiguration(out _), "Boss feedback missing.");
                root.transform.rotation = Quaternion.Euler(0f, 0f, 37f);
                root.transform.localScale = Vector3.one * 1.7f;
                boss.Activate(new Vector2(1000f, 1000f), 3f);
                var damage = new DamagePacket(1f, Vector2.zero, Vector2.left, null);
                uint before = flash.PlayedCount;
                Require(boss.TryReceiveDamage(damage) && flash.PlayedCount == before + 1 && flash.IsPlaying,
                    "Boss accepted hit must flash.");
                Invoke(boss, "LateUpdate"); flash.RenderNow();
                Require(flash.Overlay.enabled && flash.Overlay.sprite == flash.Sources[0].sprite,
                    "Boss pose update overwrote its independent hit flash.");
                Transform body = flash.Sources[0].transform;
                Transform overlay = flash.Overlay.transform;
                Require(body == root.transform && overlay.parent == body, "Boss fixture must exercise the root-body overlay path.");
                Require(Vector3.Distance(overlay.position, body.position) < .0001f,
                    "Boss root-body overlay world position diverged.");
                Require(Quaternion.Angle(overlay.rotation, body.rotation) < .01f,
                    "Boss root-body overlay world rotation diverged.");
                Require(Vector3.Distance(overlay.lossyScale, body.lossyScale) < .0001f,
                    "Boss root-body overlay world scale diverged.");
                Require(boss.TryReceiveDamage(damage) && boss.TryReceiveDamage(damage) && boss.IsDeparting,
                    "Lethal boss hit must retain the existing departure lifecycle.");
                flash.RenderNow();
                Require(flash.IsPlaying && flash.Overlay.enabled && !boss.CanReceiveDamage,
                    "Lethal boss hit should flash its visible departure without reopening damage.");
                boss.ResetEncounter();
                Require(!root.activeSelf && !flash.Overlay.enabled, "Encounter reset left boss flash visible.");
                boss.Activate(new Vector2(1000f, 1000f), 3f);
                Require(!flash.IsPlaying && !flash.Overlay.enabled, "Encounter restart retained a flash.");
                boss.ApplyReplica(true, new Vector2(1000f, 1000f), 2f);
                Require(!flash.IsPlaying, "HP snapshot incorrectly inferred a hit.");
                flash.ApplyReplica(5, 0f);
                boss.ApplyReplica(true, new Vector2(1000f, 1000f), 2f, false, .5f);
                flash.RenderNow();
                Require(flash.IsPlaying && flash.Overlay.enabled && !root.GetComponent<Collider2D>().enabled,
                    "Replica pose must preserve feedback without enabling damage collider.");
                return 13;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static uint Next(uint value) { unchecked { value++; return value == 0 ? 1u : value; } }
        private static void Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, Hidden).Invoke(target, arguments);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
