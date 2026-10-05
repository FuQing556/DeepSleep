using System;
using System.Linq;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.World.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class PlayerHitFeedbackInstaller
    {
        public static string InstallAll()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            var shader=Shader.Find("DeepSleep/PlayerHitOverlay");
            if(shader==null || ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Hit overlay shader missing/invalid");
            const string materialPath="Assets/_Project/Art/Shaders/MAT_PlayerHitOverlay.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}
            foreach(string path in new[]{"Assets/Scenes/Gameplay_Prototype.unity","Assets/Scenes/World01_EarlyInternet.unity"})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;
                if(!opened && scene.isDirty)throw new InvalidOperationException("Unsaved scene: "+path);
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try{Install(scene,material);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            var net=AssetDatabase.LoadAssetAtPath<NetworkTuningConfig>("Assets/_Project/Configs/Networking/CFG_Network.asset");
            DeepSleep.Editor.Networking.NetworkBuildRevision.Apply(net);EditorUtility.SetDirty(net);AssetDatabase.SaveAssets();
            return "Player hit feedback installed in Prototype and World01; camera/input refs, options, HUD and reliable channel 46.";
        }

        private static void Install(Scene scene,Material material)
        {
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var session=all.OfType<CoopSessionController>().Single();
            var assignment=all.OfType<PlayerControlAssignment>().Single();
            var camera=all.OfType<CameraHorizontalLookAhead2D>().Single();
            var channel=Get<NetworkPlayerHitFeedbackChannel>(session.gameObject);channel.Session=session;
            foreach(var replica in all.OfType<NetworkPlayerReplica>())
            {
                var actor=replica.Body.GetComponent<PlayerActor>();
                var presenter=Get<PlayerHitFeedbackPresenter2D>(actor.gameObject);
                presenter.Actor=actor;presenter.Receiver=actor.GetComponent<PlayerDamageReceiver2D>();
                presenter.Session=session;presenter.Assignment=assignment;presenter.CameraFeedback=camera;
                presenter.BodySprite=replica.Visual;
                var overlay=actor.transform.Find("HitFeedbackOverlay");
                if(overlay==null)overlay=Child(replica.Visual.transform.parent,"HitFeedbackOverlay");
                else overlay.SetParent(replica.Visual.transform.parent,false);
                var renderer=Get<SpriteRenderer>(overlay.gameObject);renderer.sharedMaterial=material;renderer.enabled=false;
                presenter.HitOverlay=renderer;
                var hud=all.OfType<PlayerCombatHudView>().Single(h=>h.GetComponent<PlayerCombatHudSource>().Role==actor.Definition.Role);
                presenter.Hud=hud;
                var flash=RectChild(hud.HealthFill.parent,"HitFlash");Stretch(flash);
                var image=Get<Image>(flash.gameObject);image.raycastTarget=false;image.enabled=false;
                presenter.HudFlash=image;
                if(!presenter.TryValidateConfiguration(out string reason))throw new InvalidOperationException(reason);
                if(actor.Definition.Role==PlayerRole.DeepSeek)channel.DeepSeek=presenter;else channel.Harness=presenter;
                EditorUtility.SetDirty(presenter);
            }
            if(!channel.TryValidateConfiguration(out string why))throw new InvalidOperationException(why);
            foreach(var source in all.Where(x=>x is DeepSleep.Runtime.Input.Touch.TouchCommandSource ||
                x is DeepSleep.Runtime.Input.Local.UnityInputCommandSource))
            {
                var so=new SerializedObject(source);so.FindProperty("_cameraPresentation").objectReferenceValue=camera;so.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(var panorama in all.OfType<FinitePanoramaLayer2D>())
            {var so=new SerializedObject(panorama);so.FindProperty("_cameraTravelBudget").floatValue=.51f;so.ApplyModifiedPropertiesWithoutUndo();}
            var gate=all.OfType<NetworkAuthorityGate>().Single();
            DeepSleep.Editor.Networking.NetworkAuthorityRules.Apply(gate);
            var menu=all.OfType<CoopSessionMenu>().Single();
            var options=Get<PlayerHitFeedbackOptions>(menu.PlayGroup);options.CameraFeedback=camera;
            options.ShakeButton=Option(menu,"HitShakeOption","受击震屏：轻微",new Vector2(-205,-124));
            options.FlashButton=Option(menu,"HitFlashOption","受击闪光：标准",new Vector2(205,-124));
            options.ShakeLabel=options.ShakeButton.GetComponentInChildren<Text>();
            options.FlashLabel=options.FlashButton.GetComponentInChildren<Text>();
            EditorUtility.SetDirty(options);EditorUtility.SetDirty(channel);EditorUtility.SetDirty(gate);
        }
        private static Button Option(CoopSessionMenu menu,string name,string label,Vector2 position)
        {
            var r=RectChild(menu.PlayGroup.transform,name);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=position;r.sizeDelta=new Vector2(370,54);
            var image=Get<Image>(r.gameObject);image.color=new Color(.1f,.22f,.32f,.95f);
            var button=Get<Button>(r.gameObject);button.targetGraphic=image;
            var text=Get<Text>(RectChild(r,"Label").gameObject);Stretch(text.rectTransform);
            text.text=label;text.font=menu.AiLabel.font;text.fontSize=23;text.alignment=TextAnchor.MiddleCenter;
            text.color=Color.white;text.raycastTarget=false;
            return button;
        }
        private static T Get<T>(GameObject go) where T:Component
        {var c=go.GetComponent<T>();if(c==null)c=go.AddComponent<T>();return c;}
        private static Transform Child(Transform parent,string name)
        {var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}return t;}
        private static RectTransform RectChild(Transform parent,string name)
        {var t=parent.Find(name) as RectTransform;if(t==null){t=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();t.SetParent(parent,false);}return t;}
        private static void Stretch(RectTransform r)
        {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
