using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Networking
{
    /// <summary>显式菜单装配；复用现有选角门和房间服务，无运行时补组件。</summary>
    public static class OpeningFrontEndSetup
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) return "Stop Play first";
            var session = Object.FindFirstObjectByType<CoopSessionController>();
            var menu = Object.FindFirstObjectByType<CoopSessionMenu>();
            var front = Object.FindFirstObjectByType<OpeningFrontEnd>();
            if (front != null) return "Front end already installed";
            var root = Rect("UI_FrontEnd", null, Vector2.zero, Vector2.zero);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            front = root.gameObject.AddComponent<OpeningFrontEnd>();
            front.Session = session; front.Selection = session.Selection; front.NetworkMenu = menu;
            front.SelectionCanvas = session.Selection.GetComponent<Canvas>();
            front.TouchInput = (TouchCommandSource)session.LocalInput;
            foreach (var hud in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (hud.name == "UI_CombatHUD") front.CombatHud = hud;
            var background = Rect("Background", root, Vector2.zero, Vector2.zero); Stretch(background);
            background.gameObject.AddComponent<Image>().color = new Color(.025f,.05f,.105f,1);
            front.Backdrop = background.gameObject;
            var safe = Rect("SafeArea", root, Vector2.zero, Vector2.zero); Stretch(safe);
            var fitter = safe.gameObject.AddComponent<SafeAreaRectFitter>();
            var so = new SerializedObject(fitter); so.FindProperty("_target").objectReferenceValue = safe; so.ApplyModifiedPropertiesWithoutUndo();
            var main = Rect("MainMenu",safe,Vector2.zero,new Vector2(900,560)); front.MainPanel=main.gameObject;
            Label("DeepSleep",main,new Vector2(0,170),new Vector2(800,100),64);
            Label("A journey for two",main,new Vector2(0,85),new Vector2(800,55),28);
            front.Solo = Button("Single Player",main,new Vector2(0,-20),new Vector2(540,90));
            front.Online = Button("Online Co-op",main,new Vector2(0,-140),new Vector2(540,90));
            front.Back = Button("Back",safe,new Vector2(-730,400),new Vector2(190,70));
            // Back 位于房间和选角面板外；两套交互不会互相穿透。
            menu.GetComponent<Canvas>().sortingOrder = 1100;
            var panel=(RectTransform)menu.Panel.transform; panel.sizeDelta=new Vector2(1080,760);
            Position(menu.StatusLabel.rectTransform,new Vector2(0,270),new Vector2(1000,170));
            Position((RectTransform)menu.HostDs.transform,new Vector2(-250,120),new Vector2(450,72));
            Position((RectTransform)menu.HostHs.transform,new Vector2(250,120),new Vector2(450,72));
            Position((RectTransform)menu.Address.transform,new Vector2(-130,-10),new Vector2(690,70));
            Position(menu.Address.textComponent.rectTransform,Vector2.zero,new Vector2(660,64));
            Position((RectTransform)menu.JoinButton.transform,new Vector2(380,-10),new Vector2(190,70));
            Position((RectTransform)menu.ReadyButton.transform,new Vector2(-250,-100),new Vector2(450,72));
            Position((RectTransform)menu.AiButton.transform,new Vector2(250,-100),new Vector2(450,72));
            Position((RectTransform)menu.LeaveButton.transform,new Vector2(-140,-195),new Vector2(670,70));
            Position((RectTransform)menu.TransportMode.transform,new Vector2(0,-280),new Vector2(950,65));
            Position((RectTransform)menu.RelayEndpoint.transform,new Vector2(0,-345),new Vector2(950,55));
            Position(menu.RelayEndpoint.textComponent.rectTransform,Vector2.zero,new Vector2(920,50));
            menu.RelayEndpoint.text="";
            menu.RelayEndpoint.placeholder = Label("Relay address: wss://...",menu.RelayEndpoint.transform,Vector2.zero,new Vector2(920,50),24);
            menu.AddressHint=Label("",panel,new Vector2(0,60),new Vector2(950,40),22);
            menu.CopyRoom=Button("Copy code",panel,new Vector2(365,-195),new Vector2(220,70));
            menu.ReadyText="Ready"; menu.CancelReadyText="Cancel ready";
            menu.AiText="Let AI play"; menu.ControlText="Take control";
            menu.LanText="Local network (LAN) - tap to switch";
            menu.RelayText="Internet co-op - tap to switch";
            menu.LanHint="Join: enter the host computer's Wi-Fi IPv4";
            menu.RoomHint="Join: paste your friend's room code. Host: leave blank to generate.";
            menu.TogglePanel.GetComponentInChildren<Text>().text="Session";
            EditorUtility.SetDirty(menu);
            UnityEditor.PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
            UnityEditor.PlayerSettings.allowedAutorotateToPortrait=false;
            UnityEditor.PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            UnityEditor.PlayerSettings.allowedAutorotateToLandscapeLeft=true;
            UnityEditor.PlayerSettings.allowedAutorotateToLandscapeRight=true;
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            EditorSceneManager.SaveScene(session.gameObject.scene); AssetDatabase.SaveAssets();
            return "Main menu, solo selection, room flow, landscape-only installed";
        }
        private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
            Position(r,position,size); return r;
        }
        private static void Position(RectTransform r,Vector2 pos,Vector2 size)
        { r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f); r.anchoredPosition=pos; r.sizeDelta=size; }
        private static void Stretch(RectTransform r)
        { r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero; }
        private static Text Label(string value,Transform parent,Vector2 pos,Vector2 size,int fontSize)
        {
            var r=Rect("Label",parent,pos,size);var t=r.gameObject.AddComponent<Text>();
            t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=fontSize;t.text=value;
            t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.raycastTarget=false;return t;
        }
        private static Button Button(string value,Transform parent,Vector2 pos,Vector2 size)
        {
            var r=Rect(value,parent,pos,size);var i=r.gameObject.AddComponent<Image>();i.color=new Color(.12f,.25f,.43f,1);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=i;Label(value,r,Vector2.zero,size,28);return b;
        }
    }
}
