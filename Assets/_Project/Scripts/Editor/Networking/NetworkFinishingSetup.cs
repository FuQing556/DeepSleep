using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Movement;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Input.Touch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Networking
{
    public static class NetworkFinishingSetup
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) return "Stop Play first";
            var session = Object.FindFirstObjectByType<CoopSessionController>();
            InstallRelay(session);
            var capture = session.GetComponent<DeepSleep.Adapters.Networking.NetworkRenderCapture>();
            if (capture == null) capture = session.gameObject.AddComponent<DeepSleep.Adapters.Networking.NetworkRenderCapture>();
            capture.Camera = Camera.main;
            session.GetComponent<CoopNetworkDevelopmentProbe>().CaptureComponent = capture;
            var channel = session.GetComponent<NetworkPlayerSnapshotChannel>();
            const string replicaPath = "Assets/_Project/Prefabs/Networking/PF_NetworkEntityView.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(replicaPath);
            var replicaView = prefab.GetComponent<NetworkEntityView>();
            if (replicaView.Group == null) replicaView.Group = prefab.AddComponent<UnityEngine.Rendering.SortingGroup>();
            replicaView.Group.enabled = false;
            PrefabUtility.SaveAsPrefabAsset(prefab,replicaPath); PrefabUtility.UnloadPrefabContents(prefab);
            foreach (var replica in new[] { channel.DeepSeek, channel.Harness })
            {
                var motor = new SerializedObject(replica.GetComponent<PlayerMovementMotor2D>());
                var prediction = replica.GetComponent<NetworkMovementPrediction>();
                if (prediction == null) prediction = replica.gameObject.AddComponent<NetworkMovementPrediction>();
                prediction.Replica = replica;
                prediction.Motor = (PlayerMotorConfig)motor.FindProperty("config").objectReferenceValue;
                prediction.BodyCollider = (Collider2D)motor.FindProperty("bodyCollider").objectReferenceValue;
                replica.Prediction = prediction; replica.Actions = replica.GetComponent<PlayerActionGate>();
            }
            if (!(session.LocalInput is TouchCommandSource))
            {
                var input = session.LocalInput.gameObject.AddComponent<TouchCommandSource>();
                input.DesktopInput = session.LocalInput; input.AimCamera = Camera.main;
                input.Assignment = session.Assignment; input.DeepSeek = session.DeepSeek.transform; input.Harness = session.Harness.transform;
                var root = new GameObject("UI_TouchControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                input.TouchCanvas = root;
                var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>(); safe.SetParent(root.transform, false);
                safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.offsetMin = safe.offsetMax = Vector2.zero;
                var fitter = safe.gameObject.AddComponent<DeepSleep.Runtime.UI.Common.SafeAreaRectFitter>();
                var fit = new SerializedObject(fitter); fit.FindProperty("_target").objectReferenceValue = safe; fit.ApplyModifiedPropertiesWithoutUndo();
                var aim = Pad("Tap enemy / Hold to slash", safe, input, TouchCommandPad.PadKind.WorldAim, Vector2.zero, Vector2.zero, Vector2.zero);
                aim.Area.anchorMin = Vector2.zero; aim.Area.anchorMax = Vector2.one; aim.Area.offsetMin = aim.Area.offsetMax = Vector2.zero;
                aim.GetComponent<Image>().color = Color.clear;
                var move = Pad("Move", safe, input, TouchCommandPad.PadKind.Movement, new Vector2(180,180), new Vector2(240,240), Vector2.zero);
                var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); knob.SetParent(move.transform, false);
                knob.sizeDelta = new Vector2(70,70); knob.GetComponent<Image>().raycastTarget = false; move.Knob = knob;
                Pad("Skill", safe,input,TouchCommandPad.PadKind.Skill,new Vector2(-160,180),new Vector2(150,150),Vector2.right);
                Pad("Secondary", safe,input,TouchCommandPad.PadKind.Secondary,new Vector2(-350,120),new Vector2(140,100),Vector2.right);
                Pad("Cancel", safe,input,TouchCommandPad.PadKind.Cancel,new Vector2(-160,360),new Vector2(140,90),Vector2.right);
                session.LocalInput = input;
                var assignment = new SerializedObject(session.Assignment);
                assignment.FindProperty("_localCommandSourceComponent").objectReferenceValue = input; assignment.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene); EditorSceneManager.SaveScene(session.gameObject.scene);
            return "Prediction + touch command source wired";
        }
        private static void InstallRelay(CoopSessionController session)
        {
            if (session.TransportComponent is DeepSleep.Adapters.Networking.SelectableTransportAdapter) return;
            var lan = (DeepSleep.Adapters.Networking.NgoTransportAdapter)session.TransportComponent;
            var relay = session.gameObject.AddComponent<DeepSleep.Adapters.Networking.WebSocketRelayAdapter>(); relay.Config = session.Config;
            var selector = session.gameObject.AddComponent<DeepSleep.Adapters.Networking.SelectableTransportAdapter>(); selector.Lan = lan; selector.Relay = relay;
            session.TransportComponent = selector;
            var menu = Object.FindFirstObjectByType<DeepSleep.Runtime.UI.CharacterSelection.CoopSessionMenu>();
            var panel = (RectTransform)menu.Panel.transform; panel.sizeDelta = new Vector2(700,740);
            foreach (var label in panel.GetComponentsInChildren<Text>(true))
                if (label.text.Contains("cloud relay not connected")) Object.DestroyImmediate(label.gameObject);
            var button = Object.Instantiate(menu.ReadyButton, panel); button.name = "TransportMode";
            ((RectTransform)button.transform).anchoredPosition = new Vector2(0,-235);
            ((RectTransform)button.transform).sizeDelta = new Vector2(630,60);
            menu.TransportMode = button; menu.TransportLabel = button.GetComponentInChildren<Text>();
            var field = Object.Instantiate(menu.Address,panel); field.name = "Relay WSS endpoint";
            ((RectTransform)field.transform).anchoredPosition = new Vector2(0,-310);
            ((RectTransform)field.transform).sizeDelta = new Vector2(630,60);
            field.text = "ws://127.0.0.1:8765"; menu.RelayEndpoint = field;
        }
        private static TouchCommandPad Pad(string name, Transform parent, TouchCommandSource source,
            TouchCommandPad.PadKind kind, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image),typeof(TouchCommandPad));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent,false); rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = pos; rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.1f,.18f,.3f,.6f);
            var pad = go.GetComponent<TouchCommandPad>(); pad.Source = source; pad.Kind = kind; pad.Area = rect;
            if (kind != TouchCommandPad.PadKind.WorldAim)
            {
                var label = new GameObject("Label",typeof(RectTransform),typeof(Text)); label.transform.SetParent(rect,false);
                var lr = label.GetComponent<RectTransform>(); lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
                var text=label.GetComponent<Text>();text.text=name;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=24;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
            }
            return pad;
        }
    }
}
