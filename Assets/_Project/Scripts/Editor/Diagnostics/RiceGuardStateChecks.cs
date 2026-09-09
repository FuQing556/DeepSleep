using System;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>纯规则检查；只创建瞬态测试配置，不修改场景、正式资产或玩家状态。</summary>
    public static class RiceGuardStateChecks
    {
        [MenuItem("DeepSleep/验证/DS米饭护航规则核心")]
        public static void Run()
        {
            var config = ScriptableObject.CreateInstance<DeepSeekRiceGuardConfig>();
            try
            {
                config.Charges = 6; config.DurationSeconds = 8;
                config.CooldownSeconds = 16; config.WarningSeconds = 2;
                config.Radius = 1.725f;
                var state = new RiceGuardState(config);
                Require(state.TryActivate() && state.RemainingCharges == 6, "六次护航展开");
                Require(!state.TryActivate(), "展开中不能重复刷新");
                Require(!state.TryBlock(0) && state.RemainingCharges == 6, "未编号攻击不得扣次数");
                Require(state.TryBlock(1) && state.RemainingCharges == 5, "首次挡伤扣一次");
                Require(state.TryBlock(1) && state.RemainingCharges == 5, "重复攻击不重复扣除");
                for (ulong id = 2; id <= 6; id++) Require(state.TryBlock(id), "不同攻击各自扣除");
                Require(!state.IsActive && state.CooldownRemaining == 16, "六次耗尽立即冷却");
                Require(state.TryBlock(6) && !state.TryBlock(7), "末次攻击重入已挡，新攻击穿过");
                state.Simulate(0); Require(state.CooldownRemaining == 16, "暂停不偷跑");
                state.Simulate(15); Require(!state.TryActivate(), "冷却未完不展开");
                state.Simulate(1); Require(state.TryActivate(), "冷却结束重新展开");
                Require(state.TryBlock(1) && state.RemainingCharges == 5, "新一轮清除旧账本");
                state.Simulate(6); Require(state.IsWarning && state.RemainingSeconds == 2, "最后两秒预警");
                state.Simulate(2); Require(!state.IsActive && state.CooldownRemaining == 16, "到期立即失效");
                state.Simulate(16); state.TryActivate(); state.Simulate(10);
                Require(state.CooldownRemaining == 14, "跨到期大步长正确结算");
                state.Simulate(14); state.TryActivate(); state.Cancel();
                Require(!state.IsActive && state.CooldownRemaining == 16, "中止进入冷却");
                state.Simulate(1); state.Cancel();
                Require(state.CooldownRemaining == 15, "重复中止不刷新冷却");
                config.Charges = 100; config.DurationSeconds = 100;
                state.Simulate(15); state.TryActivate();
                Require(state.RemainingCharges == 6 && state.RemainingSeconds == 8, "运行规则不随资产改变");
                Debug.Log("[DS护航] 规则核心检查通过；尚不代表玩家伤害接入或美术装配完成。");
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        private static void Require(bool condition, string rule)
        {
            if (!condition) throw new InvalidOperationException("[DS护航] " + rule);
        }
    }
}
