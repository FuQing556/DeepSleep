using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.World.Nodes;
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
        private const string ActivateRiceGuardPath =
            "DeepSleep/调试/玩家/展开 DS 米饭护航";
        private const string ConsumeRiceGuardPath =
            "DeepSleep/调试/玩家/让 DS 护航承受一次攻击";
        private const string EnterRestNodePath =
            "DeepSleep/调试/节点/进入休息节点";
        private const string ReturnToCombatPath =
            "DeepSleep/调试/节点/返回战斗";
        private const string GrantTokenPath =
            "DeepSleep/调试/经济/DS 与 HS 各增加 50 Token";
        private const string CompleteSegmentPath =
            "DeepSleep/调试/章节/完成任务并结束倒计时";
        private const string FailSegmentPath =
            "DeepSleep/调试/章节/任务不足并结束倒计时";
        private const string CompletePrototypePath =
            "DeepSleep/调试/章节/直接完成三段原型";

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

        [MenuItem(ActivateRiceGuardPath, priority = 22)]
        private static void ActivateRiceGuard()
        {
            DeepSeekRiceGuardController controller =
                Object.FindAnyObjectByType<DeepSeekRiceGuardController>(
                    FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogWarning("[DeepSleep 调试] 当前场景没有DS米饭护航控制器。");
                return;
            }

            Debug.Log(
                $"[DeepSleep 调试] 展开DS米饭护航：{controller.TryActivate()}。",
                controller);
        }

        [MenuItem(ConsumeRiceGuardPath, priority = 23)]
        private static void ConsumeRiceGuard()
        {
            DeepSeekRiceGuardController controller =
                Object.FindAnyObjectByType<DeepSeekRiceGuardController>(
                    FindObjectsInactive.Exclude);
            PlayerDamageReceiver2D receiver = controller != null
                ? controller.GetComponent<PlayerDamageReceiver2D>()
                : null;
            if (controller == null || receiver == null || !controller.IsActive)
            {
                Debug.LogWarning("[DeepSleep 调试] DS护航未展开或受伤入口未装配。");
                return;
            }

            DamagePacket damage = new DamagePacket(
                1f,
                receiver.transform.position,
                Vector2.right,
                null,
                DamageAttackIdAllocator.Next(),
                DamageInterceptionPolicy.Blockable);
            bool accepted = receiver.TryReceiveDamage(in damage);
            Debug.Log(
                $"[DeepSleep 调试] 护航承受攻击：{accepted}，" +
                $"剩余 {controller.RemainingCharges} 碗。",
                controller);
        }

        [MenuItem(EnterRestNodePath, priority = 40)]
        private static void EnterRestNode()
        {
            RestNodePrototypeController2D controller =
                Object.FindAnyObjectByType<RestNodePrototypeController2D>(
                    FindObjectsInactive.Include);
            bool accepted = controller != null &&
                controller.BeginNodeTransition();
            Debug.Log(
                $"[DeepSleep 调试] 请求进入休息节点：{accepted}。",
                controller);
        }

        [MenuItem(ReturnToCombatPath, priority = 41)]
        private static void ReturnToCombat()
        {
            RestNodePrototypeController2D controller =
                Object.FindAnyObjectByType<RestNodePrototypeController2D>(
                    FindObjectsInactive.Include);
            bool accepted = controller != null &&
                controller.ReturnToCombat();
            Debug.Log(
                $"[DeepSleep 调试] 请求返回战斗：{accepted}。",
                controller);
        }

        [MenuItem(GrantTokenPath, priority = 50)]
        private static void GrantToken()
        {
            TokenWallet wallet = Object.FindAnyObjectByType<TokenWallet>(
                FindObjectsInactive.Include);
            if (wallet == null)
            {
                Debug.LogWarning("[DeepSleep 调试] 当前场景没有 Token 钱包。");
                return;
            }
            wallet.CreditRole(PlayerRole.DeepSeek, 50);
            wallet.CreditRole(PlayerRole.Harness, 50);
            Debug.Log(
                $"[DeepSleep 调试] DS Token：{wallet.DeepSeekBalance}，" +
                $"HS Token：{wallet.HarnessBalance}。",
                wallet);
        }

        [MenuItem(CompleteSegmentPath, priority = 60)]
        private static void CompleteSegment()
        {
            ChapterRunController controller =
                Object.FindAnyObjectByType<ChapterRunController>(
                    FindObjectsInactive.Include);
            controller?.CompleteObjectiveAndExpireForDevelopment();
        }

        [MenuItem(FailSegmentPath, priority = 61)]
        private static void FailSegment()
        {
            ChapterRunController controller =
                Object.FindAnyObjectByType<ChapterRunController>(
                    FindObjectsInactive.Include);
            controller?.FailObjectiveForDevelopment();
        }

        [MenuItem(CompletePrototypePath, priority = 62)]
        private static void CompletePrototype()
        {
            ChapterRunController controller =
                Object.FindAnyObjectByType<ChapterRunController>(
                    FindObjectsInactive.Include);
            controller?.CompletePrototypeForDevelopment();
        }

        [MenuItem(StopSpawningPath, true)]
        [MenuItem(StartSpawningPath, true)]
        [MenuItem(ClearEnemiesPath, true)]
        [MenuItem(DownDeepSeekPath, true)]
        [MenuItem(DownHarnessPath, true)]
        [MenuItem(ActivateRiceGuardPath, true)]
        [MenuItem(ConsumeRiceGuardPath, true)]
        [MenuItem(EnterRestNodePath, true)]
        [MenuItem(ReturnToCombatPath, true)]
        [MenuItem(GrantTokenPath, true)]
        [MenuItem(CompleteSegmentPath, true)]
        [MenuItem(FailSegmentPath, true)]
        [MenuItem(CompletePrototypePath, true)]
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
