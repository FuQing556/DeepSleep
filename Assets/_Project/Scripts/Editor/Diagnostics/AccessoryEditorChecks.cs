using System;
using System.Reflection;
using DeepSleep.Runtime.Presentation.Accessories;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DeepSleep.Editor.Diagnostics
{
    public static class AccessoryEditorChecks
    {
        public static string Run()
        {
            var source = AssetDatabase.LoadAssetAtPath<HeadwearDefinition>(Setup.HeadwearInstaller.DefinitionPath);
            var item = Object.Instantiate(source);
            var root = new GameObject("AccessoryIsolatedChecks");
            var character = new GameObject("Character", typeof(SpriteRenderer));
            character.transform.SetParent(root.transform);
            var ornament = new GameObject("Ornament", typeof(SpriteRenderer));
            ornament.transform.SetParent(character.transform);
            var view = character.AddComponent<HeadwearSpriteView>();
            view.Subject = character.GetComponent<SpriteRenderer>();
            view.Ornament = ornament.GetComponent<SpriteRenderer>();
            view.SetEquipped(item);
            var uiRoot = new GameObject("UI", typeof(RectTransform));
            uiRoot.transform.SetParent(root.transform);
            var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portrait.transform.SetParent(uiRoot.transform, false);
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badge.transform.SetParent(portrait.transform, false);
            var imageView = portrait.AddComponent<HeadwearImageView>();
            imageView.Subject = portrait.GetComponent<Image>(); imageView.Ornament = badge.GetComponent<Image>();
            imageView.Catalog = new[] { item };
            // 只需非空 profile；会话覆盖直接使用 ID，不读取或写入正式存档。
            var profile = root.AddComponent<DeepSleep.Runtime.Progression.Meta.LocalPlayerProfileStore>();
            imageView.Bind(profile);
            imageView.SetSessionEquipment((ushort)item.NetworkId, (ushort)item.NetworkId);
            int assertions = 0;
            try
            {
                foreach (var pose in item.Poses)
                foreach (AccessoryLayer layer in Enum.GetValues(typeof(AccessoryLayer)))
                {
                    item.Layer = layer; view.Subject.sprite = pose.Pose;
                    foreach (bool flip in new[] { false, true })
                    {
                        view.Subject.flipX = flip; Tick(view);
                        Check(view.Ornament.sortingOrder == view.Subject.sortingOrder + (layer == AccessoryLayer.Back ? -1 : 1), ref assertions);
                        Vector3 localBase = item.Sprite.bounds.min + Vector3.Scale(item.Sprite.bounds.size, item.BaseAnchor);
                        Vector3 actual = character.transform.InverseTransformPoint(ornament.transform.TransformPoint(localBase));
                        Vector2 expected = (Vector2)pose.Pose.bounds.min + Vector2.Scale(pose.Pose.bounds.size, pose.Anchor);
                        if (flip) expected.x = -expected.x;
                        Check(Vector2.Distance(actual, expected) < .0001f, ref assertions);
                    }
                    imageView.Subject.sprite = pose.Pose; imageView.Subject.preserveAspect = true;
                    imageView.Subject.rectTransform.sizeDelta = new Vector2(240, 320);
                    imageView.Subject.rectTransform.pivot = new Vector2(.2f, .7f);
                    portrait.transform.localRotation = Quaternion.Euler(0,0,17);
                    portrait.transform.localScale = new Vector3(.7f,.7f,1);
                    Tick(imageView);
                    Check(badge.transform.parent == portrait.transform.parent, ref assertions);
                    Check(badge.transform.GetSiblingIndex() == portrait.transform.GetSiblingIndex() + (layer == AccessoryLayer.Back ? -1 : 1), ref assertions);
                    Vector2 fitted = pose.Pose.rect.size * Mathf.Min(240 / pose.Pose.rect.width, 320 / pose.Pose.rect.height);
                    Vector2 target = imageView.Subject.rectTransform.rect.center + Vector2.Scale(fitted, pose.Anchor - Vector2.one * .5f);
                    Check(Vector3.Distance(badge.transform.position, portrait.transform.TransformPoint(target)) < .001f, ref assertions);
                    imageView.Subject.enabled = false; Tick(imageView);
                    Check(!imageView.Ornament.enabled, ref assertions); imageView.Subject.enabled = true;
                }
                string json = EditorJsonUtility.ToJson(item);
                var loaded = ScriptableObject.CreateInstance<HeadwearDefinition>();
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(json, loaded);
                    Check(loaded.Layer == item.Layer && loaded.Poses.Length == 9, ref assertions);
                    for (int i = 0; i < 9; i++) Check(loaded.Poses[i].Anchor == item.Poses[i].Anchor && loaded.Poses[i].WidthRatio == item.Poses[i].WidthRatio, ref assertions);
                }
                finally { Object.DestroyImmediate(loaded); }
                return "PASS " + assertions + " assertions: 9 poses/front-back/flip/UI hierarchy and anchors/serialization. No production config or save writes.";
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(item); }
        }

        private static void Tick(object target) => target.GetType().GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        private static void Check(bool condition, ref int count) { if (!condition) throw new Exception("Accessory check failed at " + count); count++; }
    }
}
