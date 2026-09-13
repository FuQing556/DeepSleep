using System;
using System.Linq;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.UI.Common;
using DeepSleep.Runtime.Input.Touch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>显式重建临时房间UI，保留玩法场景对同一个菜单组件的引用。</summary>
    public static class SessionUiRevisionInstaller
    {
        private static readonly Color Background = new(.035f, .065f, .12f, .98f);
        private static readonly Color ButtonColor = new(.09f, .22f, .34f, 1f);
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Gameplay_Prototype.unity");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay_Prototype.unity", OpenSceneMode.Additive);
            var menu = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CoopSessionMenu>(true)).Single();
            var session = menu.Session;
            if (session.GetComponent<LanRoomDiscovery>() == null) session.gameObject.AddComponent<LanRoomDiscovery>();
            menu.Discovery = session.GetComponent<LanRoomDiscovery>(); menu.Discovery.Session = session;
            string endpoint = menu.RelayEndpoint != null ? menu.RelayEndpoint.text : "ws://127.0.0.1:8765";
            var safe = menu.Panel.transform.parent;
            // 整个SafeArea仅属于此房间菜单，清理旧按钮及悬空英文提示。
            for (int i = safe.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(safe.GetChild(i).gameObject);
            var canvas = menu.GetComponent<Canvas>(); canvas.sortingOrder = 2000;
            var scaler = menu.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1f;
            menu.TogglePanel = Button("菜单", safe, new Vector2(-62,-28), new Vector2(112,42), new Vector2(.5f,1));
            menu.QuickAi = Button("AI 托管", safe, new Vector2(62,-28), new Vector2(112,42), new Vector2(.5f,1));
            menu.QuickAiLabel = menu.QuickAi.GetComponentInChildren<Text>();
            var overlay = Rect("SessionOverlay",safe); Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0,0,.02f,.76f);
            menu.Panel = overlay.gameObject;
            var card = Rect("MenuCard",overlay); card.anchorMin = new Vector2(.06f,.05f); card.anchorMax = new Vector2(.94f,.95f);
            card.offsetMin = card.offsetMax = Vector2.zero;
            card.gameObject.AddComponent<Image>().color = Background;
            var line = Rect("Accent",card); line.anchorMin=new Vector2(0,1);line.anchorMax=Vector2.one;line.pivot=new Vector2(.5f,1);
            line.sizeDelta=new Vector2(0,3);line.gameObject.AddComponent<Image>().color=new Color(.2f,.8f,1);
            menu.TitleLabel = Label("联机大厅", card, 29); Top(menu.TitleLabel.rectTransform, 20, 42, 24,24);
            menu.TitleLabel.alignment = TextAnchor.MiddleLeft;
            menu.StatusLabel = Label("",card,20); Top(menu.StatusLabel.rectTransform,65,85,24,24);menu.StatusLabel.alignment=TextAnchor.UpperLeft;
            var body = Rect("Body",card);Stretch(body);body.offsetMin=new Vector2(24,84);body.offsetMax=new Vector2(-24,-156);
            var connect=Rect("Connection",body);Stretch(connect);menu.ConnectGroup=connect.gameObject;
            var left=Rect("CreateAndManual",connect);Stretch(left);left.anchorMax=new Vector2(.37f,1);left.offsetMax=new Vector2(-14,0);
            menu.HostDs=Button("创建房间 · 我选 DS",left,Vector2.zero,Vector2.one);Top((RectTransform)menu.HostDs.transform,0,48);
            menu.HostHs=Button("创建房间 · 我选 HS",left,Vector2.zero,Vector2.one);Top((RectTransform)menu.HostHs.transform,58,48);
            menu.TransportMode=Button("局域网",left,Vector2.zero,Vector2.one);Top((RectTransform)menu.TransportMode.transform,120,48);
            menu.TransportLabel=menu.TransportMode.GetComponentInChildren<Text>();menu.TransportLabel.fontSize=17;
            menu.ManualButton=Button("手动地址 / 备用连接",left,Vector2.zero,Vector2.one);Top((RectTransform)menu.ManualButton.transform,180,42);
            var manual=Rect("ManualAddress",left);Stretch(manual);manual.offsetMax=new Vector2(0,-228);menu.ManualGroup=manual.gameObject;
            menu.AddressHint=Label("",manual,15);Top(menu.AddressHint.rectTransform,0,38);
            menu.Address=Input("房主 IP / 公网房间码",manual);Top((RectTransform)menu.Address.transform,40,40,0,82);menu.Address.text="";
            menu.JoinButton=Button("加入",manual,new Vector2(-38,-60),new Vector2(76,40),Vector2.one);
            menu.RelayEndpoint=Input("wss:// 中继地址",manual);Top((RectTransform)menu.RelayEndpoint.transform,86,40);menu.RelayEndpoint.text=endpoint;
            var right=Rect("NearbyRooms",connect);Stretch(right);right.anchorMin=new Vector2(.4f,0);
            menu.DiscoveryLabel=Label("寻找局域网房间…",right,18);Top(menu.DiscoveryLabel.rectTransform,0,72,0,112);menu.DiscoveryLabel.alignment=TextAnchor.UpperLeft;
            menu.ScanButton=Button("刷新房间",right,new Vector2(-53,-22),new Vector2(106,44),Vector2.one);
            var scroll=Rect("RoomList",right);Stretch(scroll);scroll.offsetMax=new Vector2(0,-82);
            var sr=scroll.gameObject.AddComponent<ScrollRect>();sr.horizontal=false;sr.movementType=ScrollRect.MovementType.Clamped;
            var viewport=Rect("Viewport",scroll);Stretch(viewport);viewport.gameObject.AddComponent<Image>().color=new Color(.04f,.1f,.17f,.5f);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Rooms",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=8;layout.padding=new RectOffset(6,6,6,6);layout.childControlHeight=true;layout.childControlWidth=true;layout.childForceExpandHeight=false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport=viewport;sr.content=content;
            menu.RoomButtons=new Button[32];menu.RoomLabels=new Text[32];
            for(int i=0;i<32;i++)
            {
                var button=Button("房间",content,Vector2.zero,new Vector2(400,52));button.name="Room_"+i;
                button.gameObject.AddComponent<LayoutElement>().preferredHeight=52;
                menu.RoomButtons[i]=button;menu.RoomLabels[i]=button.GetComponentInChildren<Text>();menu.RoomLabels[i].fontSize=18;
                button.gameObject.SetActive(false);
            }
            var lobby=Rect("Lobby",body);Stretch(lobby);menu.LobbyGroup=lobby.gameObject;
            Label("选好角色后，双方点击准备。房间内不消耗战斗时间。",lobby,22).rectTransform.sizeDelta=new Vector2(700,80);
            menu.ReadyButton=Button("准备开始",lobby,new Vector2(0,-88),new Vector2(360,58));menu.ReadyLabel=menu.ReadyButton.GetComponentInChildren<Text>();
            menu.CopyRoom=Button("复制房间码",lobby,new Vector2(0,-156),new Vector2(360,48));
            var play=Rect("InGame",body);Stretch(play);menu.PlayGroup=play.gameObject;
            menu.PlayHint=Label("",play,22);Top(menu.PlayHint.rectTransform,24,120);
            menu.AiButton=Button("开启 AI 托管",play,new Vector2(0,-42),new Vector2(390,62));menu.AiLabel=menu.AiButton.GetComponentInChildren<Text>();
            menu.LeaveButton=Button("退出 / 返回主菜单",card,new Vector2(222,40),new Vector2(396,48),Vector2.zero);
            menu.LeaveButton.GetComponent<Image>().color=new Color(.32f,.13f,.18f);
            menu.CloseButton=Button("继续游戏",card,new Vector2(-222,40),new Vector2(396,48),Vector2.right);
            var confirm=Rect("ExitConfirmation",card);Stretch(confirm);confirm.gameObject.AddComponent<Image>().color=Background;menu.ConfirmGroup=confirm.gameObject;
            var warning=Label("确定离开本局？\n本局尚未结算的进度会丢失。\n联机房主退出后，队友也会断开。",confirm,25);warning.rectTransform.sizeDelta=new Vector2(760,150);warning.rectTransform.anchoredPosition=new Vector2(0,65);
            menu.ConfirmLeave=Button("确认离开",confirm,new Vector2(-175,-95),new Vector2(300,58));
            menu.CancelLeave=Button("留在游戏",confirm,new Vector2(175,-95),new Vector2(300,58));
            confirm.gameObject.SetActive(false);overlay.gameObject.SetActive(false);menu.TogglePanel.gameObject.SetActive(false);menu.QuickAi.gameObject.SetActive(false);
            foreach(var hud in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<PlayerCombatHudView>(true)))
            {
                hud.LocalSuffix=" / 你";hud.CompanionSuffix=" / 队友";hud.HpFormat="生命 {0:0}/{1:0}";
                hud.AliveText="正常";hud.DownedText="倒地 · 等待救援";hud.RevivingFormat="救援中 {0:0}%";
                hud.ProtectionFormat="复活保护 {0:0.0}s";hud.HitProtectionFormat="受击保护 {0:0.0}s";
                hud.ReadyText="就绪";hud.CooldownFormat="冷却 {0:0.0}s";hud.ActiveFormat="持续 {0:0.0}s";
                hud.ChargesFormat=" · 剩余 {0} 碗";hud.CalibrationFormat="瞄准 {0:0.0}s";hud.MeleeText="近战形态";
                bool ds=hud.CharacterName.Contains("DS");hud.SkillName=ds?"米饭护航 · ":"光剑爆发 · ";hud.WeaponName=ds?"饭团 · ":"激光 · ";
                EditorUtility.SetDirty(hud);
            }
            foreach(var pad in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<TouchCommandPad>(true)))
            {
                var label=pad.GetComponentInChildren<Text>(true);if(label==null)continue;
                label.text=pad.Kind==TouchCommandPad.PadKind.Skill?"技能":pad.Kind==TouchCommandPad.PadKind.Secondary?"副技能":pad.Kind==TouchCommandPad.PadKind.Cancel?"取消":"";
                EditorUtility.SetDirty(label);
            }
            EditorUtility.SetDirty(menu);EditorUtility.SetDirty(menu.Discovery);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(opened)EditorSceneManager.CloseScene(scene,true);
            return "中文跨端局内菜单、托管快捷键、局域网房间列表和HUD文案已装配。";
        }
        private static RectTransform Rect(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void Top(RectTransform r,float top,float height,float left=0,float right=0)
        {r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(-left-right,height);r.anchoredPosition=new Vector2((left-right)*.5f,-top);}
        private static Text Label(string value,Transform parent,int size)
        {var r=Rect("Label",parent);var t=r.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;t.color=new Color(.91f,.95f,1);t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.supportRichText=false;return t;}
        private static Button Button(string text,Transform parent,Vector2 position,Vector2 size,Vector2? anchor=null)
        {
            var r=Rect(text,parent);r.sizeDelta=size;r.anchoredPosition=position;if(anchor.HasValue)r.anchorMin=r.anchorMax=anchor.Value;
            var image=r.gameObject.AddComponent<Image>();image.color=ButtonColor;
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;var c=b.colors;c.highlightedColor=new Color(.7f,.9f,1);c.pressedColor=new Color(.4f,.7f,1);c.disabledColor=new Color(.5f,.5f,.5f,.45f);b.colors=c;
            var label=Label(text,r,21);Stretch(label.rectTransform);label.rectTransform.offsetMin=new Vector2(8,2);label.rectTransform.offsetMax=new Vector2(-8,-2);return b;
        }
        private static InputField Input(string hint,Transform parent)
        {
            var r=Rect(hint,parent);r.gameObject.AddComponent<Image>().color=new Color(.12f,.16f,.23f);
            var input=r.gameObject.AddComponent<InputField>();var text=Label("",r,18);Stretch(text.rectTransform);text.rectTransform.offsetMin=new Vector2(10,0);text.rectTransform.offsetMax=new Vector2(-10,0);text.alignment=TextAnchor.MiddleLeft;input.textComponent=text;
            var placeholder=Label(hint,r,17);Stretch(placeholder.rectTransform);placeholder.color=new Color(.6f,.68f,.75f);input.placeholder=placeholder;input.characterLimit=256;return input;
        }
    }
}
