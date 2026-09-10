using System;
using System.Collections.Generic;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Progression.Economy;
using DeepSleep.Runtime.Progression.Upgrades;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class TokenEconomySceneInstaller
    {
        private const string CatalogPath =
            "Assets/_Project/Configs/Progression/CFG_UpgradeCatalog_Default.asset";

        [MenuItem("DeepSleep/设置/装配 Token 掉落与商店")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式装配。");
            }

            RestNodePrototypeController2D node = One<RestNodePrototypeController2D>();
            RestNodeUpgradeController shop = One<RestNodeUpgradeController>();
            if (node.GetComponent<TokenWallet>() != null)
            {
                throw new InvalidOperationException("Token 经济已经装配，未重复创建。");
            }

            TokenWallet wallet = Undo.AddComponent<TokenWallet>(node.gameObject);
            EnemyTokenRewardController rewards =
                Undo.AddComponent<EnemyTokenRewardController>(node.gameObject);
            SetReference(shop, "_wallet", wallet);
            SetReference(rewards, "_wallet", wallet);
            SetReference(rewards, "_session", One<CoopSessionController>());
            SetArray(rewards, "_enemyPools", All<EnemyActorPool2D>().ToArray());

            Canvas canvas = FindNamed<Canvas>("UI_CombatHUD");
            RectTransform safe = FindChild(canvas.transform, "SafeArea")
                as RectTransform;
            if (safe == null)
            {
                throw new InvalidOperationException("UI_CombatHUD 下缺少 SafeArea。");
            }
            BuildWalletHud(safe, wallet);
            BuildRoleWalletHud(safe, wallet, "DS", PlayerRole.DeepSeek);
            BuildRoleWalletHud(safe, wallet, "HS", PlayerRole.Harness);
            UpdateCatalogPrices();

            EditorSceneManager.MarkSceneDirty(node.gameObject.scene);
            EditorSceneManager.SaveScene(node.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[TokenEconomy] 已装配共享钱包、击败奖励、联机余额同步、" +
                "卡片购买与额外刷新消费。掉落权重为 10/25/30/25/10。");
        }

        private static void BuildWalletHud(
            RectTransform parent,
            TokenWallet wallet)
        {
            RectTransform root = Rect("TokenWalletHUD", parent);
            root.anchorMin = new Vector2(0.36f, 0.885f);
            root.anchorMax = new Vector2(0.64f, 0.935f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            Image background = Undo.AddComponent<Image>(root.gameObject);
            background.color = new Color(0.02f, 0.035f, 0.07f, 0.78f);
            background.raycastTarget = false;

            RectTransform labelRect = Rect("Label", root);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text label = Undo.AddComponent<Text>(labelRect.gameObject);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.color = new Color(1f, 0.85f, 0.35f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;

            TokenWalletHudView view =
                Undo.AddComponent<TokenWalletHudView>(root.gameObject);
            SetReference(view, "_wallet", wallet);
            SetReference(view, "_label", label);
        }

        private static void BuildRoleWalletHud(
            RectTransform safe,
            TokenWallet wallet,
            string panelName,
            PlayerRole role)
        {
            RectTransform panel = FindChild(safe, panelName) as RectTransform;
            if (panel == null)
            {
                throw new InvalidOperationException($"状态栏缺少 {panelName} 面板。");
            }

            RectTransform row = Rect("Token", panel);
            row.anchorMin = new Vector2(0.58f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.offsetMin = new Vector2(0f, 2f);
            row.offsetMax = new Vector2(-16f, 30f);
            Text label = Undo.AddComponent<Text>(row.gameObject);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.color = new Color(1f, 0.85f, 0.35f);
            label.alignment = TextAnchor.MiddleRight;
            label.raycastTarget = false;

            RoleTokenWalletHudView view =
                Undo.AddComponent<RoleTokenWalletHudView>(row.gameObject);
            SetReference(view, "_wallet", wallet);
            SetReference(view, "_label", label);
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_role").enumValueIndex = (int)role;
            serialized.ApplyModifiedProperties();
        }

        private static void UpdateCatalogPrices()
        {
            UpgradeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<UpgradeCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException("缺少强化卡目录。");
            }

            Undo.RecordObject(catalog, "Configure token prices");
            var serialized = new SerializedObject(catalog);
            SerializedProperty definitions = serialized.FindProperty("_definitions");
            for (int index = 0; index < definitions.arraySize; index++)
            {
                SerializedProperty definition =
                    definitions.GetArrayElementAtIndex(index);
                int id = definition.FindPropertyRelative("_id").intValue;
                bool exclusive = id >= 10;
                definition.FindPropertyRelative("_baseTokenCost").intValue =
                    exclusive ? 15 : 12;
                definition.FindPropertyRelative("_additionalCostPerRank").intValue =
                    exclusive ? 4 : 3;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(gameObject, "Install Token economy");
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        private static T FindNamed<T>(string name) where T : Component
        {
            foreach (T item in All<T>())
            {
                if (item.name == name) return item;
            }
            throw new InvalidOperationException($"未找到 {name} ({typeof(T).Name})。");
        }

        private static T One<T>() where T : Component
        {
            List<T> found = All<T>();
            if (found.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{typeof(T).Name} 必须恰有一个，当前为 {found.Count} 个。");
            }
            return found[0];
        }

        private static List<T> All<T>() where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in
                     EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return result;
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetArray<T>(
            UnityEngine.Object target,
            string propertyName,
            T[] values) where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue =
                    values[index];
            }
            serialized.ApplyModifiedProperties();
        }
    }
}
