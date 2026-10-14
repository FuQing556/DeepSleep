using System;
using System.Linq;
using DeepSleep.Runtime.Input.Touch;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户本轮授权的显式装配；保留已有碰撞体、角色与关卡配置。</summary>
    public static class PlaytestTouchControlsInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Existing scene edits must be saved first.");
            var active = SceneManager.GetActiveScene();
            foreach (string level in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
            {
                string path = "Assets/Scenes/" + level + ".unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try { Install(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
            }
            SceneManager.SetActiveScene(active);
            return "Three combat scenes: HS directional melee pad, exit, shared skill countdown installed.";
        }

        private static void Install(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var input = roots.SelectMany(r => r.GetComponentsInChildren<TouchCommandSource>(true)).Single();
            var pads = input.TouchCanvas.GetComponentsInChildren<TouchCommandPad>(true);
            var move = pads.Single(p => p.Kind == TouchCommandPad.PadKind.Movement);
            var skill = pads.Single(p => p.Kind == TouchCommandPad.PadKind.Skill && p.name == "Skill");
            var view = input.TouchCanvas.GetComponent<TouchCombatControlsView>();
            if (view == null) view = input.TouchCanvas.AddComponent<TouchCombatControlsView>();
            view.Input = input;
            var sources = roots.SelectMany(r => r.GetComponentsInChildren<PlayerCombatHudSource>(true)).ToArray();
            view.DeepSeek = sources.Single(s => s.Role == PlayerRole.DeepSeek);
            view.Harness = sources.Single(s => s.Role == PlayerRole.Harness);
            view.Permissions = roots.SelectMany(r => r.GetComponentsInChildren<DeepSleep.Runtime.Combat.Encounters.Claude.ClaudePermissionModule2D>(true)).SingleOrDefault();
            view.Skill = skill;
            view.ReadyText = "技能"; view.ActiveText = "护航中";
            view.SkillLabel = skill.GetComponentInChildren<Text>(true);
            view.SkillLabel.fontSize = 30;
            if (view.Melee == null)
            {
                var copy = UnityEngine.Object.Instantiate(move.gameObject, skill.transform.parent);
                copy.name = "MeleeDirection";
                view.Melee = copy.GetComponent<TouchCommandPad>();
                view.Melee.Kind = TouchCommandPad.PadKind.MeleeAim;
                BindArtwork(copy, roots);
            }
            var meleeLayout = view.Melee.GetComponent<MobileScreenRect>();
            meleeLayout.ScreenPosition = new Vector2(.86f, .22f);
            meleeLayout.SizeInScreenHeights = Vector2.one * .23f;
            if (view.Exit == null)
            {
                var copy = UnityEngine.Object.Instantiate(skill.gameObject, skill.transform.parent);
                copy.name = "ExitMelee";
                view.Exit = copy.GetComponent<TouchCommandPad>();
                view.Exit.GetComponentInChildren<Text>(true).text = "收刀";
                BindArtwork(copy, roots);
            }
            var exitLayout = view.Exit.GetComponent<MobileScreenRect>();
            exitLayout.ScreenPosition = new Vector2(.9f, .67f);
            exitLayout.SizeInScreenHeights = Vector2.one * .115f;
            var skillLayout = skill.GetComponent<MobileScreenRect>();
            skillLayout.ScreenPosition = new Vector2(.86f, .22f);
            skillLayout.SizeInScreenHeights = Vector2.one * .19f;
            if (view.CooldownMask == null)
            {
                var go = new GameObject("CooldownMask", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(skill.transform, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = go.GetComponent<Image>();
                image.sprite = skill.GetComponentsInChildren<Image>(true).First(i => i.name == "Base").sprite;
                image.color = new Color(0, 0, 0, .72f); image.raycastTarget = false;
                image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top; image.fillClockwise = true;
                image.preserveAspect = true; image.enabled = false;
                view.CooldownMask = image;
                view.SkillLabel.transform.SetAsLastSibling();
            }
            view.Melee.gameObject.SetActive(false); view.Exit.gameObject.SetActive(false);
            view.MoveSeal = Seal(move, view.MoveSeal);
            view.SkillSeal = Seal(skill, view.SkillSeal);
            view.MeleeSeal = Seal(view.Melee, view.MeleeSeal);
            EditorUtility.SetDirty(view);
        }

        private static Image Seal(TouchCommandPad pad, Image existing)
        {
            const string path = "Assets/_Project/Art/VFX/Claude/VFX_CL_ControlSeal_v01.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.maxTextureSize = 512; importer.SaveAndReimport();
            }
            if (existing == null)
            {
                var go = new GameObject("PermissionSeal", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform; rect.SetParent(pad.transform, false);
                rect.anchorMin = Vector2.one * .08f; rect.anchorMax = Vector2.one * .92f;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                existing = go.GetComponent<Image>();
            }
            existing.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            existing.preserveAspect = true; existing.raycastTarget = false; existing.enabled = false;
            return existing;
        }

        private static void BindArtwork(GameObject copy, GameObject[] roots)
        {
            var original = roots.SelectMany(r => r.GetComponentsInChildren<UiThemeView>(true))
                .First(t => t.DeepSeek != null && t.Harness != null);
            var theme = copy.AddComponent<UiThemeView>();
            theme.DeepSeek = original.DeepSeek; theme.Harness = original.Harness;
            theme.Artwork = copy.GetComponentsInChildren<Image>(true)
                .Where(i => i.name == "Base" || i.name == "Frame")
                .Select(i => new UiThemeView.ArtworkBinding { Target = i,
                    Kind = i.name == "Base" ? UiArtworkKind.CircleBase : UiArtworkKind.CircleFrame }).ToArray();
        }
    }
}
