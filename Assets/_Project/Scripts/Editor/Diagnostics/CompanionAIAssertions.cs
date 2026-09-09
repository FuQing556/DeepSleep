using System;
using DeepSleep.Runtime.Players.Companion;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>不创建/修改场景的确定性算法回归；可在编辑状态重复运行。</summary>
    internal static class CompanionAIAssertions
    {
        [MenuItem("DeepSleep/验证/同伴AI风险数学")]
        private static void Run()
        {
            float incoming = CompanionThreatMath.Risk(new Vector2(2, 0), new Vector2(-5, 0), 0.5f, 0.5f);
            Check(incoming > 0, "迎面弹应产生预测风险");
            Check(CompanionThreatMath.Risk(new Vector2(2, 0), new Vector2(5, 0), 0.5f, 0.5f) == 0,
                "远离弹不应仅因距离近就产生预测碰撞");
            Check(CompanionThreatMath.Risk(new Vector2(2, 2), new Vector2(-5, 0), 0.5f, 0.5f) == 0,
                "错开弹道应安全");
            Check(CompanionThreatMath.Risk(Vector2.zero, Vector2.zero, 0.5f, 0.5f) == 1,
                "静止重叠应产生风险且不能NaN");
            Check(CompanionThreatMath.Risk(new Vector2(5, 0), new Vector2(-5, 0), 0.5f, 0.5f) == 0,
                "预测窗口之外不应当作立刻命中");
            Check(CompanionThreatMath.ClampPoint(new Vector2(20, -20), new Rect(-5,-3,10,6), new Vector2(1,1)) ==
                new Vector2(4,-2), "边界必须容纳实际碰撞体");
            Debug.Log("[CompanionAI] 6项确定性风险/边界断言通过。");
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
