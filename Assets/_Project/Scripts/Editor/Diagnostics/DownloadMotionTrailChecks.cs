using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Poses;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class DownloadMotionTrailChecks
    {
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in edit mode.");
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Combat/Enemies/Internet/PF_Enemy_Download.prefab");
            var visual = enemy.GetComponent<DownloadChargeVisual2D>();
            var view = AssetDatabase.LoadAssetAtPath<NetworkEntityView>("Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab");
            int checks = 0;
            Action<bool, string> check = (pass, label) => { if (!pass) throw new Exception(label); checks++; };
            check(visual.MotionTrailPrefab != null && visual.SortingGroup != null, "Authority assembly");
            check(view.MotionTrailPrefab == visual.MotionTrailPrefab && view.MotionTrailSprite == visual.Dash, "Replica assembly");
            var trail = UnityEngine.Object.Instantiate(visual.MotionTrailPrefab);
            var sourceObject = new GameObject("MotionTrailCheckSource");
            try
            {
                typeof(SpriteMotionTrail2D).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(trail, null);
                var source = sourceObject.AddComponent<SpriteRenderer>(); source.sprite = visual.Dash;
                source.color = new Color(.8f, .9f, 1f, .75f); source.flipY = true;
                source.transform.position = new Vector3(2, 3, 0); source.transform.rotation = Quaternion.Euler(0, 0, 25);
                source.transform.localScale = new Vector3(.7f, .7f, 1);
                trail.Sample(source, null, false);
                check(!trail.Renderers[0].enabled, "No charging/recovery trail");
                trail.Sample(source, null, true);
                var first = trail.Renderers[0]; var initial = first.transform.position;
                check(first.enabled && first.sprite == visual.Dash && first.flipY, "Sprite/flip copied");
                check(Mathf.Abs(first.color.a - .75f * trail.Alpha) < .0001f, "Alpha copied");
                check(Quaternion.Angle(first.transform.rotation, source.transform.rotation) < .001f &&
                    Vector3.Distance(first.transform.lossyScale, source.transform.lossyScale) < .001f, "World rotation/scale");
                source.transform.position += Vector3.right * 3;
                trail.Sample(source, null, true);
                check(!trail.Renderers[1].enabled, "Emission interval");
                trail.Advance(trail.IntervalSeconds); trail.Sample(source, null, true);
                check(trail.Renderers[1].enabled && first.transform.position == initial, "Stationary world ghost");
                trail.Advance(0f);
                check(first.enabled, "Pause preserves age");
                sourceObject.SetActive(false); trail.Sample(source, null, false);
                check(first.enabled, "Return does not erase tail");
                trail.Advance(trail.FadeSeconds);
                foreach (var r in trail.Renderers) check(!r.enabled, "Tail fades after return");
                sourceObject.SetActive(true);
                for (int i = 0; i < 12; i++) { trail.Advance(trail.IntervalSeconds); trail.Sample(source, null, true); }
                check(trail.Renderers.Length == 4 && trail.GetComponentsInChildren<Collider2D>().Length == 0, "Bounded/no collision");
                trail.Clear(); foreach (var r in trail.Renderers) check(!r.enabled, "Reset clears tail");
                return "Download motion trail: " + checks + " checks passed";
            }
            finally { UnityEngine.Object.DestroyImmediate(trail.gameObject); UnityEngine.Object.DestroyImmediate(sourceObject); }
        }
    }
}
