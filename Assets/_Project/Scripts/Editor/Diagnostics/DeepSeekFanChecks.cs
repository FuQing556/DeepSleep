using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Orientation;
using DeepSleep.Runtime.Progression.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>实际武器/弹池隔离验证；不运行章节、档案或存档服务。</summary>
    public static class DeepSeekFanChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static object Get(object obj, string field) => obj.GetType().GetField(field, Private).GetValue(obj);
        private static void Call(object obj, string method) => obj.GetType().GetMethod(method, Private).Invoke(obj, null);

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Gameplay_Prototype.unity");
            var physics = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            RiceProjectilePool pool = null;
            HealthConfig healthConfig = null;
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
            try
            {
                var shooter = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DeepSeekRiceAutoShooter>(true)).Single();
                var manual = (DeepSeekManualTargetController)Get(shooter, "_manualTargetController");
                var facing = (PlayerFacingController2D)Get(shooter, "_facingController");
                var upgrades = (PlayerUpgradeRuntimeState)Get(shooter, "_upgradeState");
                pool = (RiceProjectilePool)Get(shooter, "_projectilePool");
                Call(facing, "Awake"); Call(manual, "Awake"); Call(upgrades, "Awake");
                Call(pool, "Awake"); Call(shooter, "Awake");
                Vector2 origin = ((Transform)Get(shooter, "_muzzle")).position;
                healthConfig = ScriptableObject.CreateInstance<HealthConfig>(); Set(healthConfig, "_maximumHealth", 100f);
                Collider2D Target(string name, float degrees, float distance, HealthComponent shared = null)
                {
                    var root = new GameObject(name); root.SetActive(false); SceneManager.MoveGameObjectToScene(root, physics);
                    root.layer = 8;
                    root.transform.position = origin + (Vector2)(Quaternion.Euler(0, 0, degrees) * Vector2.right) * distance;
                    var shape = root.AddComponent<CircleCollider2D>(); shape.radius = .2f; shape.isTrigger = true;
                    var receiver = shared;
                    if (receiver == null) { receiver = root.AddComponent<HealthComponent>(); Set(receiver, "_config", healthConfig); Call(receiver, "Awake"); }
                    var hit = root.AddComponent<DamageHitbox2D>(); Set(hit, "_receiverComponent", receiver); Call(hit, "Awake");
                    root.SetActive(true); return shape;
                }
                var primary = Target("Primary", 0, 10);
                Physics2D.SyncTransforms(); Check(manual.TryLockTarget(primary), "Manual primary lock");
                float[] Shoot()
                {
                    pool.ReturnAllActive(); var command = default(PlayerCommand); shooter.ConsumeCommand(in command, 10);
                    return pool.Instances.Where(p => p.IsRented).Select(p => Vector2.SignedAngle(Vector2.right,
                        p.GetComponent<Rigidbody2D>().linearVelocity)).OrderBy(a => a).ToArray();
                }
                void Angles(float[] expected, string label)
                {
                    var actual = Shoot(); var sorted = expected.OrderBy(a => a).ToArray();
                    Check(actual.Length == sorted.Length, label + " count");
                    for (int i = 0; i < sorted.Length; i++) Check(Mathf.Abs(actual[i] - sorted[i]) < .01f,
                        label + " angle: " + string.Join(",", actual));
                }
                var patterns = new[] { new[] { 0f }, new[] { 0f, 15f }, new[] { 0f, 15f, -15f },
                    new[] { 0f, 15f, -15f, 30f }, new[] { 0f, 15f, -15f, 30f, -30f } };
                for (int rank = 0; rank < 5; rank++)
                {
                    upgrades.SetRankFromAuthority(PlayerRole.DeepSeek, UpgradeCardId.RiceFan, rank);
                    upgrades.SetRankFromAuthority(PlayerRole.DeepSeek, UpgradeCardId.RiceGuidance, 0);
                    Call(shooter, "OnDisable");
                    var expected = patterns[rank];
                    Angles(expected, "Rank " + rank + " first volley");
                    Angles((rank + 1) % 2 == 0 ? expected.Select(a => -a).ToArray() : expected, "Rank " + rank + " next volley");
                    Check(expected.Count(a => a == 0) == 1, "Exactly one primary lane");
                }
                upgrades.SetRankFromAuthority(PlayerRole.DeepSeek, UpgradeCardId.RiceFan, 4);
                upgrades.SetRankFromAuthority(PlayerRole.DeepSeek, UpgradeCardId.RiceGuidance, 1);
                Call(shooter, "OnDisable");
                Angles(new[] { 0f, 15f, -15f, 30f, -30f }, "No other targets: guidance preserves fan");
                var nearMain = Target("NearMain", 2, 4);
                var side = Target("Side", 18, 8);
                var outer = Target("Outer", 28, 8);
                var sideAlias = Target("SideAlias", 24, 7, side.GetComponent<HealthComponent>());
                // 第二个受击体与主目标共用接收者，验证不会重复把侧弹分给主目标。
                var alias = Target("PrimaryAlias", 15, 3, primary.GetComponent<HealthComponent>());
                Physics2D.SyncTransforms();
                Angles(new[] { 0f, 18f, -15f, 28f, -30f }, "Guidance corrects only side lanes, excludes primary receiver");
                nearMain.gameObject.SetActive(false); side.gameObject.SetActive(false);
                outer.gameObject.SetActive(false); alias.gameObject.SetActive(false);
                sideAlias.gameObject.SetActive(false);
                manual.ClearTarget(); facing.SetDirection(FacingDirection.Right); Physics2D.SyncTransforms();
                Angles(new[] { 0f, 15f, -15f, 30f, -30f }, "Automatic primary also preserves center");
                primary.gameObject.SetActive(false); Physics2D.SyncTransforms();
                Check(Shoot().Length == 0, "No targets: no volley");
                upgrades.SetRankFromAuthority(PlayerRole.DeepSeek, UpgradeCardId.RiceFan, 1); Call(shooter, "OnDisable");
                Check(Shoot().Length == 0 && !(bool)Get(shooter, "_mirrorFan"), "Empty attempt does not alternate sides");
                primary.gameObject.SetActive(true); Physics2D.SyncTransforms();
                Check(manual.TryLockTarget(primary), "Re-lock after empty attempt");
                Angles(new[] { 0f, 15f }, "First successful even volley after empty attempt");
                Check(upgrades.Catalog.TryGet(UpgradeCardId.RiceFan, out var fan) && fan.MaximumRank == 4 && fan.Description.Contains("主弹道"), "Fan catalog updated");
                Check(upgrades.Catalog.TryGet(UpgradeCardId.RiceGuidance, out var guidance) && guidance.MaximumRank == 1 && guidance.Description.Contains("仅侧弹道"), "Guidance catalog updated");
                return "DeepSeek fan: " + checks + " checks passed (actual weapon/pool, isolated Editor; not device acceptance).";
            }
            finally
            {
                if (pool != null) pool.ReturnAllActive();
                EditorSceneManager.ClosePreviewScene(scene); EditorSceneManager.CloseScene(physics, true);
                if (healthConfig != null) UnityEngine.Object.DestroyImmediate(healthConfig);
                SceneManager.SetActiveScene(original);
            }
        }
    }
}
