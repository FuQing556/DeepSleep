using System;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class HarnessMeleeGeometryChecks
    {
        [MenuItem("DeepSleep/验证/HS近战几何与变速")]
        public static void Run()
        {
            var config = AssetDatabase.LoadAssetAtPath<HarnessMeleeConfig>(
                "Assets/_Project/Configs/Combat/Harness/Melee/CFG_HA_Melee_Default.asset");
            Require(config != null && config.IsValid, "完整配置");
            int assertions = 1;
            foreach (HarnessMeleeAttackConfig attack in config.Attacks)
            {
                Require(Mathf.Approximately(attack.Travel.Evaluate(0), 0), "轨迹起点");
                Require(Mathf.Approximately(attack.Travel.Evaluate(1), 1), "轨迹终点");
                Require(attack.Travel.Evaluate(.5f) < .5f, "前半程比线性更慢");
                float last = -1;
                for (int i = 0; i <= 100; i++)
                {
                    float t = i / 100f;
                    float travel = attack.Travel.Evaluate(t);
                    Require(travel >= last - .00001f, "轨迹不得倒退");
                    last = travel;
                    bool plateau = t >= attack.GrowFraction && t <= 1f - attack.ShrinkFraction;
                    if (plateau)
                        Require(Mathf.Approximately(MeleeSwordGeometry2D.SizeAt(attack, t), 1), "配置的大剑平台");
                    MeleeSwordGeometry2D.Evaluate(attack, Vector2.zero, 0, t, out var rightHilt, out var rightTip);
                    MeleeSwordGeometry2D.Evaluate(attack, Vector2.zero, 180, t, out var leftHilt, out var leftTip);
                    Require(Vector2.Distance(leftHilt, new Vector2(-rightHilt.x, rightHilt.y)) < .001f,
                        "向左时剑柄镜像，上下不倒置");
                    Require(Vector2.Distance(leftTip, new Vector2(-rightTip.x, rightTip.y)) < .001f,
                        "向左时剑尖镜像，上下不倒置");
                    assertions += plateau ? 4 : 3;
                }
                assertions += 3;
            }
            Debug.Log($"[HS近战] 几何与变速检查通过：{assertions} 项断言。允许调整幅度；曲线必须单调且先慢后快。");
        }

        private static void Require(bool condition, string rule)
        {
            if (!condition) throw new InvalidOperationException("[HS近战检查失败] " + rule);
        }
    }
}
