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
            foreach (var attack in config.Attacks)
            {
                foreach (var key in attack.MotionKeys)
                {
                    Require((MeleeSwordMotion2D.Evaluate(attack.MotionKeys, key.Time) - key.Channels).sqrMagnitude < .00001f,
                        "必须经过批准的关键姿势");
                    assertions++;
                }
                for (int i = 0; i <= 200; i++)
                {
                    float t = i / 200f;
                    MeleeSwordGeometry2D.Evaluate(attack, Vector2.zero, 0, t, out var rh, out var rt);
                    MeleeSwordGeometry2D.Evaluate(attack, Vector2.zero, 180, t, out var lh, out var lt);
                    Require(Vector2.Distance(lh, new Vector2(-rh.x, rh.y)) < .001f &&
                        Vector2.Distance(lt, new Vector2(-rt.x, rt.y)) < .001f, "朝左只镜像，不颠倒上下");
                    Require(Mathf.Abs(Vector2.Distance(rh, rt) - attack.SwordLength *
                        MeleeSwordGeometry2D.SizeAt(attack, t)) < .001f, "可见刀刃与查询长度一致");
                    Vector4 pose = MeleeSwordMotion2D.Evaluate(attack.MotionKeys, t);
                    int k = 0;
                    while (k < attack.MotionKeys.Length - 2 && attack.MotionKeys[k + 1].Time < t) k++;
                    for (int c = 0; c < 4; c++)
                    {
                        float a = attack.MotionKeys[k].Channels[c], b = attack.MotionKeys[k + 1].Channels[c];
                        Require(pose[c] >= Mathf.Min(a, b) - .0001f && pose[c] <= Mathf.Max(a, b) + .0001f,
                            "通道保形，不越过关键帧");
                    }
                    assertions += 6;
                }
                // 累计细分路径长度，而非仅检查两个端点：覆盖一帧跨过多个关键姿势的情况。
                for (int span = 1; span <= 10; span++)
                for (int i = 0; i + span <= 10; i++)
                {
                    float from = i / 10f, to = (i + span) / 10f, hiltPath = 0, tipPath = 0;
                    MeleeSwordGeometry2D.Evaluate(attack, Vector2.zero, 35, from, out var h0, out var t0);
                    for (int j = 1; j <= 100; j++)
                    {
                        MeleeSwordGeometry2D.Evaluate(attack, Vector2.zero, 35,
                            Mathf.Lerp(from, to, j / 100f), out var h1, out var t1);
                        hiltPath += Vector2.Distance(h0, h1); tipPath += Vector2.Distance(t0, t1);
                        h0 = h1; t0 = t1;
                    }
                    float bound = MeleeSwordGeometry2D.SweepDistanceBound(attack, from, to) + .0001f;
                    Require(hiltPath <= bound && tipPath <= bound, "全区间刀刃路程不超出扫掠上界");
                    assertions++;
                }
                // 用瞬态副本检验解耦，绝不修改真实配置资产。
                var copy = UnityEngine.Object.Instantiate(attack);
                try
                {
                    Vector2 wave = MeleeSwordGeometry2D.WavePosition(copy, Vector2.one, 35);
                    copy.MotionKeys = new[] { new MeleeSwordPoseKey(0, Vector2.one * 100, -300, .1f),
                        new MeleeSwordPoseKey(1, Vector2.one * -100, 600, 3) };
                    Require(wave == MeleeSwordGeometry2D.WavePosition(copy, Vector2.one, 35),
                        "剑气中心不受剑运动牵引");
                    MeleeSwordGeometry2D.Evaluate(copy, Vector2.one, 35, .5f, out var h0, out var t0);
                    copy.WaveOffset += Vector2.one * 50;
                    copy.WaveRotation += 90;
                    MeleeSwordGeometry2D.Evaluate(copy, Vector2.one, 35, .5f, out var h1, out var t1);
                    Require(h0 == h1 && t0 == t1, "修改剑气不牵动剑柄与剑尖");
                    assertions += 2;
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            Debug.Log($"[HS近战] 自由挥剑几何检查通过：{assertions} 项断言。");
        }

        private static void Require(bool condition, string rule)
        {
            if (!condition) throw new InvalidOperationException("[HS近战检查失败] " + rule);
        }
    }
}
