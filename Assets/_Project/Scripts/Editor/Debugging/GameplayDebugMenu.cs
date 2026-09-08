using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Debugging
{
    /// <summary>
    /// 仅在编辑器播放模式使用的玩法调试入口，不进入玩家构建。
    /// </summary>
    internal static class GameplayDebugMenu
    {
        private const string StopSpawningPath =
            "DeepSleep/调试/刷怪/停止全部刷怪";
        private const string StartSpawningPath =
            "DeepSleep/调试/刷怪/开始全部刷怪";
        private const string ClearEnemiesPath =
            "DeepSleep/调试/刷怪/清除敌人与敌方子弹";
        private const string DownDeepSeekPath =
            "DeepSleep/调试/玩家/击倒 DeepSeek";
        private const string DownHarnessPath =
            "DeepSleep/调试/玩家/击倒 Harness";

        [MenuItem(StopSpawningPath, priority = 0)]
        private static void StopAllSpawning()
        {
            EnemySpawnDirector2D[] directors =
                Object.FindObjectsByType<EnemySpawnDirector2D>(
                    FindObjectsInactive.Include);

            for (int index = 0; index < directors.Length; index++)
            {
                directors[index].Stop();
            }

            Debug.Log($"[DeepSleep 调试] 已停止 {directors.Length} 个刷怪器。");
        }

        [MenuItem(StartSpawningPath, priority = 1)]
        private static void StartAllSpawning()
        {
            EnemySpawnDirector2D[] directors =
                Object.FindObjectsByType<EnemySpawnDirector2D>(
                    FindObjectsInactive.Include);

            for (int index = 0; index < directors.Length; index++)
            {
                directors[index].Begin();
            }

            Debug.Log($"[DeepSleep 调试] 已启动 {directors.Length} 个刷怪器。");
        }

        [MenuItem(ClearEnemiesPath, priority = 2)]
        private static void ClearEnemiesAndProjectiles()
        {
            EnemyActorPool2D[] enemyPools =
                Object.FindObjectsByType<EnemyActorPool2D>(
                    FindObjectsInactive.Include);
            EnemyProjectilePool2D[] projectilePools =
                Object.FindObjectsByType<EnemyProjectilePool2D>(
                    FindObjectsInactive.Include);
            int enemyCount = 0;
            int projectileCount = 0;

            for (int index = 0; index < enemyPools.Length; index++)
            {
                enemyCount += enemyPools[index].DespawnAll(
                    EnemyDespawnReason.ExitedPlayfield);
            }

            for (int index = 0; index < projectilePools.Length; index++)
            {
                projectileCount +=
                    projectilePools[index].ReturnAllActive();
            }

            Debug.Log(
                $"[DeepSleep 调试] 已回收 {enemyCount} 个敌人、" +
                $"{projectileCount} 枚敌方子弹。");
        }

        [MenuItem(DownDeepSeekPath, priority = 20)]
        private static void DownDeepSeek()
        {
            DownPlayer(PlayerRole.DeepSeek);
        }

        [MenuItem(DownHarnessPath, priority = 21)]
        private static void DownHarness()
        {
            DownPlayer(PlayerRole.Harness);
        }

        [MenuItem(StopSpawningPath, true)]
        [MenuItem(StartSpawningPath, true)]
        [MenuItem(ClearEnemiesPath, true)]
        [MenuItem(DownDeepSeekPath, true)]
        [MenuItem(DownHarnessPath, true)]
        private static bool ValidatePlayModeCommand()
        {
            return EditorApplication.isPlaying;
        }

        private static void DownPlayer(PlayerRole role)
        {
            PlayerActor[] players = Object.FindObjectsByType<PlayerActor>(
                FindObjectsInactive.Exclude);

            for (int index = 0; index < players.Length; index++)
            {
                PlayerActor player = players[index];

                if (player.Definition == null ||
                    player.Definition.Role != role)
                {
                    continue;
                }

                PlayerLifeStateController2D lifeState =
                    player.GetComponent<PlayerLifeStateController2D>();
                HealthComponent health =
                    player.GetComponent<HealthComponent>();

                if (lifeState == null || health == null ||
                    lifeState.State == PlayerLifeState.Downed ||
                    health.CurrentHealth <= 0f)
                {
                    Debug.LogWarning(
                        $"[DeepSleep 调试] {role} 已经宕机或生命装配无效。",
                        player);
                    return;
                }

                DamagePacket lethalDamage = new DamagePacket(
                    health.CurrentHealth,
                    player.transform.position,
                    Vector2.zero,
                    null);
                bool succeeded = health.TryReceiveDamage(in lethalDamage);

                Debug.Log(
                    $"[DeepSleep 调试] 击倒 {role}：{succeeded}。",
                    player);
                return;
            }

            Debug.LogWarning(
                $"[DeepSleep 调试] 当前播放场景没有找到 {role}。");
        }
    }
}
