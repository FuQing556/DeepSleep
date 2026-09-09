using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Companion;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Revive;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Combat;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>用户明确触发的一次性HUD装配；不改角色碰撞体，不在Runtime补组件。</summary>
    public static class CombatHudSceneInstaller
    {
        private const string IconPath = "Assets/_Project/Art/UI/Status/ICO_SH_ReviveProtection_v01.png";
        [MenuItem("DeepSleep/设置/装配双端战斗状态栏")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请在编辑模式装配。");
            var assignment = One<PlayerControlAssignment>();
            var selection = One<OpeningCharacterSelectionController>();
            var data = new SerializedObject(assignment);
            var ds = (PlayerActor)data.FindProperty("_deepSeekActor").objectReferenceValue;
            var hs = (PlayerActor)data.FindProperty("_harnessActor").objectReferenceValue;
            if (ds == null || hs == null) throw new InvalidOperationException("角色引用不完整。");
            var icon = ImportIcon();
            foreach (var existing in All<PlayerCombatHudView>())
                throw new InvalidOperationException("HUD已存在，未重复装配：" + existing.name);
            var root = new GameObject("UI_CombatHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(root, "Install combat HUD");
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 900;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var safe = Rect("SafeArea", root.transform);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one; safe.sizeDelta = Vector2.zero;
            var fitter = Undo.AddComponent<SafeAreaRectFitter>(safe.gameObject);
            var serialized = new SerializedObject(fitter);
            serialized.FindProperty("_target").objectReferenceValue = safe;
            serialized.ApplyModifiedProperties();
            BuildPanel(safe, ds, assignment, selection, icon, false);
            BuildPanel(safe, hs, assignment, selection, icon, true);
            var tactics = AssetDatabase.LoadAssetAtPath<CompanionTacticsConfig>(
                "Assets/_Project/Configs/Players/CFG_CompanionTactics_Default.asset");
            Undo.RecordObject(tactics, "HS aim rest");
            tactics.HarnessAimRestSeconds = 0.25f;
            EditorUtility.SetDirty(tactics);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[CombatHUD] 双端只读状态栏已装配；HS人机冷却后休息0.25秒。");
        }

        private static void BuildPanel(RectTransform parent, PlayerActor actor, PlayerControlAssignment assignment,
            OpeningCharacterSelectionController selection, Sprite icon, bool right)
        {
            var panel = Rect(right ? "HS" : "DS", parent);
            panel.anchorMin = new Vector2(right ? 0.66f : 0, 1);
            panel.anchorMax = new Vector2(right ? 1 : 0.34f, 1);
            panel.pivot = new Vector2(0.5f, 1);
            panel.offsetMin = new Vector2(20, -224); panel.offsetMax = new Vector2(-20, -20);
            var bg = Undo.AddComponent<Image>(panel.gameObject);
            bg.color = new Color(0.02f, 0.035f, 0.07f, 0.78f); bg.raycastTarget = false;
            var group = Undo.AddComponent<CanvasGroup>(panel.gameObject);
            group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            var source = Undo.AddComponent<PlayerCombatHudSource>(panel.gameObject);
            source.Role = actor.Definition.Role; source.Assignment = assignment;
            source.Health = actor.GetComponent<HealthComponent>();
            source.Life = actor.GetComponent<PlayerLifeStateController2D>();
            source.DamageReceiver = actor.GetComponent<PlayerDamageReceiver2D>();
            source.Revive = actor.GetComponent<PlayerReviveCoordinator2D>();
            source.Guard = actor.GetComponent<DeepSeekRiceGuardController>();
            source.Melee = actor.GetComponent<HarnessMeleeController>();
            source.Laser = actor.GetComponent<HarnessTerminalLaserController>();
            if (!source.IsValid) throw new InvalidOperationException("HUD数据源装配缺失：" + actor.name);
            var view = Undo.AddComponent<PlayerCombatHudView>(panel.gameObject);
            view.SourceComponent = source; view.Selection = selection; view.Panel = group;
            view.Header = Label(panel, "Header", 32, 12, 38);
            view.HealthLabel = Label(panel, "Health", 25, 51, 30);
            var bar = Rect("HealthBar", panel);
            TopRow(bar, 87, 13, 20, 20);
            var track = Undo.AddComponent<Image>(bar.gameObject);
            track.color = new Color(0.12f, 0.17f, 0.24f); track.raycastTarget = false;
            var fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            var fillImage = Undo.AddComponent<Image>(fill.gameObject);
            fillImage.color = right ? new Color(1f, 0.3f, 0.4f) : new Color(0.25f, 0.8f, 1f);
            fillImage.raycastTarget = false; view.HealthFill = fill;
            view.StatusLabel = Label(panel, "Status", 25, 106, 30, 62);
            var iconRect = Rect("ReviveProtection", panel);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0, 1);
            iconRect.pivot = new Vector2(0, 1); iconRect.anchoredPosition = new Vector2(14, -102);
            iconRect.sizeDelta = new Vector2(42, 42);
            view.ProtectionIcon = Undo.AddComponent<Image>(iconRect.gameObject);
            view.ProtectionIcon.sprite = icon; view.ProtectionIcon.preserveAspect = true;
            view.ProtectionIcon.raycastTarget = false; view.ProtectionIcon.enabled = false;
            view.SkillLabel = Label(panel, "Skill", 24, 140, 28);
            view.WeaponLabel = Label(panel, "Weapon", 24, 170, 28);
            view.CharacterName = right ? "HS" : "DS";
            view.LocalSuffix = "  /  YOU"; view.CompanionSuffix = "  /  COMPANION";
            view.HpFormat = "HP  {0:0}/{1:0}"; view.AliveText = "ACTIVE"; view.DownedText = "DOWNED";
            view.RevivingFormat = "REVIVING  {0:0}%"; view.ProtectionFormat = "REVIVE SHIELD  {0:0.0}s";
            view.HitProtectionFormat = "HIT SHIELD  {0:0.0}s";
            view.SkillName = right ? "MELEE  " : "RICE GUARD  "; view.WeaponName = right ? "LASER  " : "RICE  ";
            view.ReadyText = "READY"; view.CooldownFormat = "CD  {0:0.0}s";
            view.ActiveFormat = "ACTIVE  {0:0.0}s"; view.ChargesFormat = "  x{0}";
            view.CalibrationFormat = "AIM  {0:0.0}s"; view.MeleeText = "MELEE MODE";
        }

        private static Sprite ImportIcon()
        {
            AssetDatabase.ImportAsset(IconPath);
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("缺少已抠图的复活保护图标。");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 512; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        private static void TopRow(RectTransform rect, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, -top-height); rect.offsetMax = new Vector2(-right, -top);
        }
        private static Text Label(RectTransform parent, string name, int size, float top, float height, float left = 20)
        {
            var rect = Rect(name, parent); TopRow(rect, top, height, left, 16);
            var text = Undo.AddComponent<Text>(rect.gameObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
            text.color = Color.white; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.raycastTarget = false;
            return text;
        }
        private static List<T> All<T>() where T : Component
        {
            var result = new List<T>();
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }
        private static T One<T>() where T : Component
        {
            var found = All<T>();
            if (found.Count != 1) throw new InvalidOperationException(typeof(T).Name + " 必须恰有一个。");
            return found[0];
        }
    }
}
