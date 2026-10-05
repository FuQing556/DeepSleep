using System;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.World.Nodes;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Run
{
    /// <summary>
    /// 执行章节决定的停止生成、自然清场和强制回收；不保存第二套阶段状态或计时器。
    /// 普通敌人依赖来自关卡绑定，特殊内容通过显式停止契约登记，节点不再管理全关对象池。
    /// </summary>
    public sealed class ChapterCombatWorld2D : MonoBehaviour
    {
        public LevelSceneBindings Bindings;
        public RestNodeCombatGate Gate;
        public RiceProjectilePool Rice;
        public MonoBehaviour[] ParticipantComponents = Array.Empty<MonoBehaviour>();
        private IChapterCombatLifecycle[] _participants;

        private bool CanAuthor => Bindings.Session.Phase == SessionPhase.Offline || Bindings.Session.IsAuthority;

        public bool IsCleared
        {
            get
            {
                for (int index = 0; index < Bindings.Enemies.Count; index++)
                    if (Bindings.Enemies[index].Pool.ActiveCount > 0) return false;
                return true;
            }
        }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError("[ChapterCombatWorld2D] " + reason, this);
                enabled = false;
                return;
            }
            _participants = new IChapterCombatLifecycle[ParticipantComponents.Length];
            for (int index = 0; index < _participants.Length; index++)
                _participants[index] = (IChapterCombatLifecycle)ParticipantComponents[index];
        }

        /// <summary>只停止新生成；残敌、攻击、救援和击败奖励继续使用原链路。</summary>
        public void StopSpawning()
        {
            if (!CanAuthor) return;
            for (int index = 0; index < Bindings.Enemies.Count; index++)
                Bindings.Enemies[index].Director.Stop();
        }

        /// <summary>章节完成本段调参、目标重建及检查点恢复后，最后开放攻击。</summary>
        public void ResumeCombat() => Gate.SetCombatAllowed(true);

        /// <summary>副本只跟随章节/节点的表现门，不执行本地奖励、恢复或遭遇裁决。</summary>
        public void ApplyReplicaCombatAllowed(bool allowed) => Gate.SetCombatAllowed(allowed);

        /// <summary>同步取消待发攻击并无奖励回收。可重复调用，不注销池实例的感知与奖励订阅。</summary>
        public void StopCombat(ChapterCombatStopReason reason)
        {
            Gate.SetCombatAllowed(false);
            if (!CanAuthor || _participants == null) return;
            StopSpawning();
            for (int index = 0; index < _participants.Length; index++)
                _participants[index].StopCombat(reason);
            for (int index = 0; index < Bindings.Enemies.Count; index++)
            {
                LevelEnemySceneBinding entry = Bindings.Enemies[index];
                entry.Pool.DespawnAll(EnemyDespawnReason.RunReset);
                for (int projectile = 0; projectile < entry.ProjectilePools.Count; projectile++)
                {
                    EnemyProjectilePool2D pool = entry.ProjectilePools[projectile];
                    // 合法共享弹池只回收一次；条目数小且只在生命周期边界执行，不为此分配集合。
                    bool seen = false;
                    for (int previous = 0; previous < index && !seen; previous++)
                        for (int p = 0; p < Bindings.Enemies[previous].ProjectilePools.Count; p++)
                            if (Bindings.Enemies[previous].ProjectilePools[p] == pool) { seen = true; break; }
                    if (!seen) pool.ReturnAllActive();
                }
            }
            Rice.ReturnAllActive();
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Bindings == null || Bindings.gameObject.scene != gameObject.scene ||
                Bindings.Session == null || Gate == null || Gate.gameObject.scene != gameObject.scene ||
                Rice == null || Rice.gameObject.scene != gameObject.scene ||
                Bindings.WorldSnapshot == null || Bindings.WorldSnapshot.Rice != Rice)
            { reason = "Bindings、Gate、Rice 必须显式属于本关，饭团池须与世界复制引用一致。"; return false; }
            if (!Gate.TryValidateConfiguration(out reason)) return false;
            if (ParticipantComponents == null)
            { reason = "ParticipantComponents 不能为 null；没有特殊模块时使用空数组。"; return false; }
            for (int index = 0; index < ParticipantComponents.Length; index++)
            {
                MonoBehaviour component = ParticipantComponents[index];
                if (component == null || component is not IChapterCombatLifecycle ||
                    component.gameObject.scene != gameObject.scene || Array.IndexOf(ParticipantComponents, component) != index)
                { reason = "生命周期模块 " + index + " 为空、重复、跨场景或未实现停止契约。"; return false; }
            }
            reason = string.Empty;
            return true;
        }
    }
}
