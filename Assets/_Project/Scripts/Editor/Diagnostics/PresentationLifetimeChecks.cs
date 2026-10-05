using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>表现出生帧、池还原与节点销毁次序回归。所有场景操作限定临时对象/借出的空闲表现。</summary>
    public static class PresentationLifetimeChecks
    {
        private const BindingFlags PrivateFields = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("DeepSleep/验证/表现寿命数学")]
        private static void RunMathMenu() => Debug.Log(RunMath());

        [MenuItem("DeepSleep/诊断/读取当前联机与命中表现状态")]
        private static void DescribeMenu() => Debug.Log(DescribeLoadedFeedback());

        public static string RunMath()
        {
            var lifetime = new PresentationLifetime();
            lifetime.Begin(100);
            Check(lifetime.Elapsed == 0f, "Begin did not reset elapsed.");
            Check(lifetime.Advance(100, 2f) == 0f, "Birth frame consumed a long deltaTime.");
            Check(lifetime.Advance(100, .02f) == 0f, "Birth frame consumed time on its second update.");
            Check(Mathf.Abs(lifetime.Advance(101, .02f) - .02f) < .000001f, "Following frame did not advance.");
            Check(Mathf.Abs(lifetime.Advance(101, 1f) - .02f) < .000001f, "Same frame counted twice.");
            Check(Mathf.Abs(lifetime.Advance(102, 0f) - .02f) < .000001f, "Pause advanced elapsed.");
            Check(Mathf.Abs(lifetime.Advance(103, -.5f) - .02f) < .000001f, "Negative deltaTime moved elapsed backwards.");
            Check(Mathf.Abs(lifetime.Advance(104, .03f) - .05f) < .000001f, "Resume failed to advance normally.");
            lifetime.Begin(104);
            Check(lifetime.Elapsed == 0f && lifetime.Advance(104, 10f) == 0f, "Replay in the same frame did not restart cleanly.");
            return "9 presentation lifetime assertions passed: birth frame, next frame, duplicate update, pause, negative dt, resume, replay.";
        }

        public static string RunPlay()
        {
            RequirePlay();
            int effects = 0, numbers = 0;
            foreach (var presenter in Find<HarnessLaserHitEffectPresenter2D>())
            {
                var available = Get<Stack<HarnessLaserHitEffect2D>>(presenter, "_available");
                Check(available.Count > 0, "No idle HS effect available; rerun after current effects finish.");
                var config = Get<HarnessTerminalLaserPresentationConfig>(presenter, "_config");
                int before = presenter.ActiveCount;
                uint requested = presenter.RequestedCount, played = presenter.PlayedCount;
                var effect = available.Peek();
                var state = new ViewSnapshot(effect);
                available.Pop();
                try
                {
                    Check(!effect.IsPlaying, "Available HS effect was already playing.");
                    // 如回归导致 Complete，不能让真实池回调提前 Push，随后 finally 又重复 Push。
                    Set(effect, "Finished", null);
                    effect.PlayAt(Vector2.zero, Vector2.right, .2f, config, 0f, 0f);
                    Invoke(effect, "LateUpdate");
                    Invoke(effect, "LateUpdate");
                    Check(effect.IsPlaying && effect.gameObject.activeSelf && Get<SpriteRenderer>(effect, "_renderer").enabled,
                        "HS hit effect disappeared on its birth frame.");
                    Check(Get<float>(effect, "_elapsedSeconds") == 0f, "HS effect aged during birth frame.");
                    effects++;
                }
                finally
                {
                    try { effect.PrepareForPool(); state.Restore(); }
                    finally { available.Push(effect); }
                }
                Check(presenter.ActiveCount == before && presenter.RequestedCount == requested && presenter.PlayedCount == played,
                    "HS pool/counters were not restored.");
            }
            foreach (var presenter in Find<CombatDamageNumberPresenter2D>())
            {
                var available = Get<Stack<DamageNumberEntryView>>(presenter, "_available");
                Check(available.Count > 0, "No idle damage number available; rerun after current numbers finish.");
                int before = presenter.ActiveCount;
                uint requested = presenter.RequestedCount, played = presenter.PlayedCount;
                var canvas = Get<Canvas>(presenter, "_canvas");
                var camera = Get<Camera>(presenter, "_worldCamera");
                var entry = available.Peek();
                var state = new ViewSnapshot(entry);
                available.Pop();
                try
                {
                    Check(!entry.IsPlaying, "Available damage number was already playing.");
                    Set(entry, "Finished", null);
                    entry.Play(camera.transform.position, 7f, Get<DamageNumberStyleConfig>(presenter, "_harnessStyle"),
                        Get<RectTransform>(presenter, "_canvasRect"), camera,
                        canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, .5f, .5f);
                    Invoke(entry, "LateUpdate");
                    Invoke(entry, "LateUpdate");
                    Check(entry.IsPlaying && entry.gameObject.activeSelf, "Damage number disappeared on its birth frame.");
                    Check(Get<float>(entry, "_elapsedSeconds") == 0f, "Damage number aged during birth frame.");
                    Check(entry.GetComponentsInChildren<Image>(true).Any(i => i.gameObject.activeSelf && i.enabled),
                        "Damage number has no visible glyph on its birth frame.");
                    numbers++;
                }
                finally
                {
                    try { entry.PrepareForPool(); state.Restore(); }
                    finally { available.Push(entry); }
                }
                Check(presenter.ActiveCount == before && presenter.RequestedCount == requested && presenter.PlayedCount == played,
                    "Damage number pool/counters were not restored.");
            }
            Check(effects > 0 && numbers > 0, "Gameplay presenters not loaded.");
            return RunMath() + " Play birth-frame checks: " + effects + " HS pools, " + numbers +
                " number pools passed; twice-immediate LateUpdate, idle state and pool order restored. Actual frame dt=" +
                Time.deltaTime.ToString("F4") + "; long-dt coverage comes from the injected pure lifetime test.";
        }

        public static string DescribeLoadedFeedback()
        {
            var report = new StringBuilder("Loaded feedback diagnostics (read-only)");
            report.AppendLine("\nframe=" + Time.frameCount + ", delta=" + Time.deltaTime.ToString("F4") + ", timeScale=" + Time.timeScale);
            foreach (var session in Find<CoopSessionController>())
            {
                report.AppendLine(session.gameObject.scene.name + "/" + session.name + ": phase=" + session.Phase +
                    ", authority=" + session.IsAuthority + ", localRole=" + session.LocalRole + ", localAI=" + session.LocalAi +
                    ", DS=" + session.GetRoleControl(PlayerRole.DeepSeek) + ", HS=" + session.GetRoleControl(PlayerRole.Harness));
                report.AppendLine(session.Diagnostics.Describe());
            }
            foreach (var channel in Find<NetworkCombatFeedbackChannel>()) report.AppendLine(channel.DescribeDiagnostics());
            foreach (var presenter in Find<HarnessLaserHitEffectPresenter2D>())
                report.AppendLine(presenter.name + " HS: enabled=" + presenter.enabled + ", active=" + presenter.gameObject.activeInHierarchy +
                    ", initialized=" + Get<bool>(presenter, "_isInitialized") + ", requested=" + presenter.RequestedCount +
                    ", played=" + presenter.PlayedCount + ", disabledDrop=" + presenter.DisabledDropCount + ", activeViews=" + presenter.ActiveCount +
                    ", pooled=" + Get<List<HarnessLaserHitEffect2D>>(presenter, "_all").Count + ", authorityGated=" + IsGated(presenter));
            foreach (var presenter in Find<CombatDamageNumberPresenter2D>())
                report.AppendLine(presenter.name + " numbers: enabled=" + presenter.enabled + ", active=" + presenter.gameObject.activeInHierarchy +
                    ", initialized=" + Get<bool>(presenter, "_isInitialized") + ", requested=" + presenter.RequestedCount +
                    ", played=" + presenter.PlayedCount + ", disabledDrop=" + presenter.DisabledDropCount +
                    ", capacityDrop=" + presenter.CapacityDropCount + ", invalidDrop=" + presenter.InvalidDropCount +
                    ", activeViews=" + presenter.ActiveCount + ", pooled=" + Get<List<DamageNumberEntryView>>(presenter, "_all").Count +
                    ", authorityGated=" + IsGated(presenter));
            foreach (var brain in Find<CompanionCommandSource2D>())
                report.AppendLine(brain.name + " AI: enabled=" + brain.enabled + ", active=" + brain.gameObject.activeInHierarchy +
                    ", plan=" + brain.Plan + ", blocked=" + brain.RouteBlocked + ", reason=" + brain.Reason);
            return report.ToString();
        }

        public static string RunRestNodeTeardownPlay()
        {
            RequirePlay();
            var original = Find<RestNodePrototypeController2D>().FirstOrDefault();
            var actor = Find<PlayerActor>().FirstOrDefault();
            Check(original != null && actor != null, "Gameplay node and actor required.");
            var root = new GameObject("RestNodeTeardownCheck_Temporary");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);
            try
            {
                var node = root.AddComponent<RestNodePrototypeController2D>();
                foreach (var field in typeof(RestNodePrototypeController2D).GetFields(PrivateFields))
                    if (Attribute.IsDefined(field, typeof(SerializeField))) field.SetValue(node, field.GetValue(original));
                // Awake 的可变表现全换成临时对象；借用的玩法引用仅用于非空检查，绝不推进 Update/ApplyState。
                Set(node, "_session", null);
                Set(node, "_cloudLayerRoot", root.transform);
                var templeObject = new GameObject("TemporaryTemple");
                templeObject.transform.SetParent(root.transform, false);
                var temple = templeObject.AddComponent<SpriteRenderer>();
                temple.sprite = Get<SpriteRenderer>(original, "_templeRenderer").sprite;
                Set(node, "_templeRenderer", temple);
                var prompt = TextObject("Prompt", root.transform);
                var button = ButtonObject(root.transform);
                var label = TextObject("Label", button.transform);
                Set(node, "_promptText", prompt); Set(node, "_actionButton", button); Set(node, "_actionButtonLabel", label);
                var hotspotObject = new GameObject("TemporaryMemoryHotspot");
                hotspotObject.transform.SetParent(root.transform, false);
                hotspotObject.AddComponent<BoxCollider2D>().isTrigger = true;
                var hotspot = hotspotObject.AddComponent<RestNodeHotspot2D>();
                hotspot.Configure(node, RestNodeHotspotKind.MemoryFragment, "Diagnostic", false, PlayerRole.DeepSeek);
                Set(node, "_hotspots", new[] { hotspot });
                node.enabled = false;
                root.SetActive(true);
                Check(Get<bool>(node, "_isInitialized"), "Temporary node did not initialize.");
                node.enabled = true;
                Set(node, "<State>k__BackingField", RestNodeState.Open);
                var overlaps = Get<Dictionary<RestNodeHotspot2D, Dictionary<PlayerActor, int>>>(node, "_overlaps");
                SeedOverlap(overlaps, hotspot, actor);
                node.enabled = false;
                Invoke(node, "NotifyExited", hotspot, actor);
                Check(overlaps.Count == 0, "Disabled node retained overlaps.");
                node.enabled = true;
                Set(node, "<State>k__BackingField", RestNodeState.Open);
                SeedOverlap(overlaps, hotspot, actor);
                Object.DestroyImmediate(prompt.gameObject);
                Invoke(node, "NotifyExited", hotspot, actor);
                Check(overlaps.Count == 0, "Destroyed prompt prevented overlap cleanup.");
                prompt = TextObject("ReplacementPrompt", root.transform); Set(node, "_promptText", prompt);
                SeedOverlap(overlaps, hotspot, actor);
                Object.DestroyImmediate(label.gameObject);
                Invoke(node, "NotifyExited", hotspot, actor);
                Check(overlaps.Count == 0, "Destroyed label prevented overlap cleanup.");
                label = TextObject("ReplacementLabel", button.transform); Set(node, "_actionButtonLabel", label);
                SeedOverlap(overlaps, hotspot, actor);
                Object.DestroyImmediate(button.gameObject);
                Invoke(node, "NotifyExited", hotspot, actor);
                Check(overlaps.Count == 0, "Destroyed button prevented overlap cleanup.");
                node.enabled = false;
                Invoke(node, "NotifyExited", hotspot, actor);
                return "5 temporary-node teardown cases passed: disabled exit, destroyed prompt/label/button, disable after UI destruction. " +
                    "No real node/UI/hotspot was disabled or destroyed; temporary objects removed.";
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void SeedOverlap(Dictionary<RestNodeHotspot2D, Dictionary<PlayerActor, int>> overlaps,
            RestNodeHotspot2D hotspot, PlayerActor actor)
        { overlaps.Clear(); overlaps.Add(hotspot, new Dictionary<PlayerActor, int> { [actor] = 1 }); }
        private static Text TextObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Text>();
        }
        private static Button ButtonObject(Transform parent)
        {
            var go = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }
        private static bool IsGated(Behaviour component) => Find<NetworkAuthorityGate>().Any(g =>
            g.AuthorityOnly != null && Array.IndexOf(g.AuthorityOnly, component) >= 0);
        private static T[] Find<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, PrivateFields).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, PrivateFields).SetValue(owner, value);
        private static void Invoke(object owner, string method, params object[] arguments) =>
            owner.GetType().GetMethod(method, PrivateFields).Invoke(owner, arguments);
        private static void RequirePlay() { if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required."); }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        private sealed class ViewSnapshot
        {
            private readonly MonoBehaviour _view;
            private readonly FieldInfo[] _fields;
            private readonly object[] _values;
            private readonly TransformState[] _transforms;
            private readonly SpriteState[] _sprites;
            private readonly ImageState[] _images;
            public ViewSnapshot(MonoBehaviour view)
            {
                _view = view;
                _fields = view.GetType().GetFields(PrivateFields | BindingFlags.DeclaredOnly);
                _values = _fields.Select(f => f.GetValue(view)).ToArray();
                _transforms = view.GetComponentsInChildren<Transform>(true).Select(t => new TransformState(t)).ToArray();
                _sprites = view.GetComponentsInChildren<SpriteRenderer>(true).Select(s => new SpriteState(s)).ToArray();
                _images = view.GetComponentsInChildren<Image>(true).Select(i => new ImageState(i)).ToArray();
            }
            public void Restore()
            {
                for (int i = 0; i < _fields.Length; i++) _fields[i].SetValue(_view, _values[i]);
                foreach (var transform in _transforms) transform.Restore();
                foreach (var sprite in _sprites) sprite.Restore();
                foreach (var image in _images) image.Restore();
            }
        }
        private sealed class TransformState
        {
            private readonly Transform _target;
            private readonly Vector3 _position, _scale;
            private readonly Quaternion _rotation;
            private readonly bool _active;
            private readonly Vector2 _size, _anchored;
            public TransformState(Transform target)
            {
                _target = target; _position = target.localPosition; _rotation = target.localRotation;
                _scale = target.localScale; _active = target.gameObject.activeSelf;
                if (target is RectTransform rect) { _size = rect.sizeDelta; _anchored = rect.anchoredPosition; }
            }
            public void Restore()
            {
                _target.localPosition = _position; _target.localRotation = _rotation; _target.localScale = _scale;
                if (_target is RectTransform rect) { rect.sizeDelta = _size; rect.anchoredPosition = _anchored; }
                _target.gameObject.SetActive(_active);
            }
        }
        private sealed class SpriteState
        {
            private readonly SpriteRenderer _target;
            private readonly Color _color;
            private readonly bool _enabled;
            public SpriteState(SpriteRenderer target) { _target = target; _color = target.color; _enabled = target.enabled; }
            public void Restore() { _target.color = _color; _target.enabled = _enabled; }
        }
        private sealed class ImageState
        {
            private readonly Image _target;
            private readonly Color _color;
            private readonly Sprite _sprite;
            private readonly bool _enabled;
            public ImageState(Image target) { _target = target; _color = target.color; _sprite = target.sprite; _enabled = target.enabled; }
            public void Restore() { _target.color = _color; _target.sprite = _sprite; _target.enabled = _enabled; }
        }
    }
}
