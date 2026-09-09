using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Enemies.DataCrawlerSnake;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Revive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户明确触发的装配器；只新增AI适配与引用，保留全部角色/敌人的美术和碰撞调参。</summary>
    internal static class CompanionAISceneInstaller
    {
        private const string ConfigPath = "Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset";

        [MenuItem("DeepSleep/设置/装配双角色同伴AI")]
        private static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请先停止播放。");
            var assignment = One<PlayerControlAssignment>();
            var assignmentData = new SerializedObject(assignment);
            var ds = (PlayerActor)assignmentData.FindProperty("_deepSeekActor").objectReferenceValue;
            var hs = (PlayerActor)assignmentData.FindProperty("_harnessActor").objectReferenceValue;
            var guard = ds.GetComponent<DeepSeekRiceGuardController>();
            if (guard == null) throw new InvalidOperationException("DS护航组件缺失，未装配AI。");
            var registry = Ensure<CombatPerceptionRegistry2D>(assignment.gameObject);
            int masks = 0;
            foreach (var pool in SceneComponents<EnemyActorPool2D>())
            {
                var prefab = new SerializedObject(pool).FindProperty("_enemyPrefab").objectReferenceValue;
                masks |= PreparePrefab(AssetDatabase.GetAssetPath(prefab));
                Set(pool, "_perceptionRegistry", registry);
            }
            foreach (var pool in SceneComponents<EnemyProjectilePool2D>())
            {
                var prefab = new SerializedObject(pool).FindProperty("_projectilePrefab").objectReferenceValue;
                masks |= PreparePrefab(AssetDatabase.GetAssetPath(prefab));
                Set(pool, "_perceptionRegistry", registry);
            }
            var config = AssetDatabase.LoadAssetAtPath<CompanionTacticsConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<CompanionTacticsConfig>();
                ConfigureDefaults(config, masks);
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var router = Ensure<CompanionCommandRouter>(assignment.gameObject);
            router.Assignment = assignment;
            router.DeepSeek = PrepareBrain(assignment.transform, ds, hs, guard, registry, config);
            router.Harness = PrepareBrain(assignment.transform, hs, ds, guard, registry, config);
            Set(assignment, "_companionCommandSourceComponent", router);
            foreach (var label in SceneComponents<Text>())
            {
                if (label.text == "The other character will be controlled by Companion AI later.")
                {
                    label.text = "Your companion fights alongside you and can rescue you.";
                    EditorUtility.SetDirty(label);
                }
            }
            EditorUtility.SetDirty(router);
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CompanionAI] 双角色同伴已装配。GameSimulation下AI_DeepSeek/AI_Harness可查看计划；未修改碰撞参数。");
        }

        private static CompanionCommandSource2D PrepareBrain(Transform parent, PlayerActor actor, PlayerActor ally,
            DeepSeekRiceGuardController guard, CombatPerceptionRegistry2D registry, CompanionTacticsConfig config)
        {
            string name = "AI_" + actor.Definition.Role;
            Transform existing = parent.Find(name);
            GameObject root = existing != null ? existing.gameObject : new GameObject(name);
            if (existing == null) { Undo.RegisterCreatedObjectUndo(root, "装配同伴AI"); root.transform.SetParent(parent, false); }
            var sensor = Ensure<CompanionBattleSensor2D>(root);
            sensor.Registry = registry;
            sensor.Config = config;
            var combat = Ensure<CompanionCombatPolicy2D>(root);
            combat.Role = actor.Definition.Role;
            combat.Config = config;
            combat.Guard = guard;
            combat.DeepSeekTarget = actor.GetComponent<DeepSeekManualTargetController>();
            combat.Laser = actor.GetComponent<HarnessTerminalLaserController>();
            combat.Melee = actor.GetComponent<HarnessMeleeController>();
            var brain = Ensure<CompanionCommandSource2D>(root);
            brain.Config = config;
            brain.Sensor = sensor;
            brain.Combat = combat;
            brain.Owner = actor.transform;
            brain.Body = actor.GetComponent<Rigidbody2D>();
            var motor = new SerializedObject(actor.MovementMotor);
            brain.Shape = (Collider2D)motor.FindProperty("bodyCollider").objectReferenceValue;
            brain.MotorConfig = (PlayerMotorConfig)motor.FindProperty("config").objectReferenceValue;
            brain.Health = actor.GetComponent<HealthComponent>();
            brain.Life = actor.GetComponent<PlayerLifeStateController2D>();
            brain.AllyLife = ally.GetComponent<PlayerLifeStateController2D>();
            brain.Revive = actor.GetComponent<PlayerReviveActionChannel>();
            brain.TeamGuard = guard;
            EditorUtility.SetDirty(sensor);
            EditorUtility.SetDirty(combat);
            EditorUtility.SetDirty(brain);
            return brain;
        }

        private static int PreparePrefab(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("对象池未引用已保存的预制体。");
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var adapter = Ensure<CombatPerceptionBody2D>(root);
                adapter.Enemy = root.GetComponent<EnemyActor2D>();
                adapter.Projectile = root.GetComponent<EnemyProjectile2D>();
                adapter.SnakeAttack = root.GetComponent<DataCrawlerSnakeAttackController2D>();
                adapter.Body = root.GetComponent<Rigidbody2D>();
                adapter.Hitbox = root.GetComponentInChildren<DamageHitbox2D>(true);
                adapter.Shape = adapter.Enemy != null ? adapter.Hitbox.GetComponent<Collider2D>() :
                    (Collider2D)new SerializedObject(adapter.Projectile).FindProperty("_bodyCollider").objectReferenceValue;
                adapter.TargetValue = adapter.SnakeAttack != null ? 3f : 1f;
                if (adapter.Shape == null || adapter.Body == null) throw new InvalidOperationException(path + " 感知引用不完整。");
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return 1 << adapter.Shape.gameObject.layer;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureDefaults(CompanionTacticsConfig c, int masks)
        {
            // 本轮可验收的初始战术参数，不覆盖角色技能参数；之后只在配置资产调节。
            c.PerceptionLayers = masks; c.QueryCapacity = 256;
            c.DecisionInterval = 0.125f; c.PerceptionRadius = 20f; c.PredictionSeconds = 0.45f;
            c.SafetyPadding = 0.15f; c.ChargingBonus = 3f; c.NearbyThreatWeight = 5f;
            c.WoundedBonus = 1f; c.DistanceCost = 0.15f; c.TargetStickiness = 1.5f;
            c.NearbyRadius = 3f; c.AttackRange = 18f; c.FormationOffset = new Vector2(-1.4f, 1.4f);
            c.ArrivalRadius = 0.3f; c.DangerCost = 12f; c.DirectionChangeCost = 0.08f;
            c.RescueDangerLimit = 0.35f; c.EmergencyDanger = 0.65f; c.LowHealthFraction = 0.35f;
            c.GuardDangerThreshold = 0.35f; c.MeleeEnterRange = 3f; c.MeleeAttackRange = 4f;
            c.MeleeClusterCount = 2; c.MeleeIdleExitSeconds = 1.5f; c.SkillRetrySeconds = 0.5f;
        }

        private static T Ensure<T>(GameObject go) where T : Component =>
            go.TryGetComponent<T>(out var component) ? component : Undo.AddComponent<T>(go);
        private static void Set(UnityEngine.Object owner, string property, UnityEngine.Object value)
        {
            var data = new SerializedObject(owner);
            data.FindProperty(property).objectReferenceValue = value;
            data.ApplyModifiedProperties();
            if (PrefabUtility.IsPartOfPrefabInstance(owner)) PrefabUtility.RecordPrefabInstancePropertyModifications(owner);
        }
        private static List<T> SceneComponents<T>() where T : Component
        {
            var result = new List<T>();
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }
        private static T One<T>() where T : Component
        {
            var all = SceneComponents<T>();
            if (all.Count != 1) throw new InvalidOperationException(typeof(T).Name + " 应恰有一个。");
            return all[0];
        }
    }
}
