using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class DeepSeekTargetAvailabilityChecks
    {
        private static void Set(object value, string field, object data) =>
            value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(value, data);

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var root = new GameObject("DS_target_availability_check") { hideFlags = HideFlags.HideAndDontSave };
            var rice = ScriptableObject.CreateInstance<DeepSeekRiceWeaponConfig>();
            var manual = ScriptableObject.CreateInstance<DeepSeekManualTargetingConfig>();
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            try
            {
                root.transform.position = new Vector3(10000, 10000, 0);
                root.layer = 31;
                var sphere = root.AddComponent<CircleCollider2D>(); sphere.isTrigger = true;
                var perception = root.AddComponent<CombatPerceptionBody2D>(); perception.Shape = sphere;
                var body = root.AddComponent<BossDamageBody2D>(); body.Sphere = sphere; body.Perception = perception;
                var hit = root.AddComponent<DamageHitbox2D>(); Set(hit, "_receiverComponent", body);
                typeof(DamageHitbox2D).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hit, null);
                Check(body.BeginAuthority(10000, .5f, null, null), "Fixture starts");
                Set(rice, "_targetSearchRadius", 20f); Set(rice, "_maximumTargetCandidates", 8);
                Set(rice, "_targetLayers", (LayerMask)(1 << 31)); Set(rice, "_minimumForwardDot", 0f);
                var finderType = typeof(DeepSeekRiceAutoShooter).Assembly.GetType("DeepSleep.Runtime.Combat.Targeting.NearestVisibleTargetFinder2D");
                var finder = Activator.CreateInstance(finderType, new object[] { rice });
                var find = finderType.GetMethod("TryFind");
                var origin = (Vector2)root.transform.position - Vector2.right * 5;
                Physics2D.SyncTransforms();
                bool Find() => (bool)find.Invoke(finder, new object[] { origin, Vector2.right, Vector2.zero, null });
                var controlRoot = new GameObject("DS_manual_check") { hideFlags = HideFlags.HideAndDontSave };
                controlRoot.transform.SetParent(root.transform); controlRoot.transform.position = origin;
                var controller = controlRoot.AddComponent<DeepSeekManualTargetController>();
                Set(manual, "_maximumLockDistance", 20f); Set(manual, "_targetLayers", (LayerMask)(1 << 31));
                Set(controller, "_targetingOrigin", controlRoot.transform); Set(controller, "_config", manual);
                Set(controller, "_isInitialized", true); Set(controller, "_lockedTarget", sphere);
                body.SetVulnerable(false);
                Check(!Find(), "Automatic targeting excludes protected entrance");
                Check(!controller.TryGetLockedTargetPosition(out _), "Stored manual target cannot bypass protection");
                Check(controller.HasLockedTarget, "Protection does not discard lock");
                body.SetVulnerable(true);
                Check(Find(), "Automatic targeting resumes when battle starts");
                Check(controller.TryGetLockedTargetPosition(out _), "Manual firing resumes without re-locking");
                body.SetVulnerable(false);
                Check(!Find(), "Phase protection also excludes target");
                body.SetVulnerable(true);
                Check(Find(), "Phase completion restores target");
                return checks + " DS target availability checks passed: real physics finder and stored manual lock.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(rice);
                UnityEngine.Object.DestroyImmediate(manual);
            }
        }
    }
}
