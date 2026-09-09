using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.UI.CharacterSelection;
using DeepSleep.Runtime.UI.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    /// <summary>为当前玩法场景装配不依赖美术素材的开局选角灰盒。</summary>
    internal static class OpeningCharacterSelectionSceneInstaller
    {
        private const string MENU_PATH = "DeepSleep/设置/装配开局角色选择";
        private const string ROOT_NAME = "UI_OpeningCharacterSelection";
        private const string EVENT_SYSTEM_NAME = "UI_EventSystem";

        [MenuItem(MENU_PATH)]
        private static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[开局选角] 当前没有可编辑的已加载场景。");
                return;
            }

            if (!TryFindControlAssignment(scene, out PlayerControlAssignment assignment))
            {
                Debug.LogError("[开局选角] 当前场景没有 PlayerControlAssignment。");
                return;
            }

            if (TryFindRoot(scene, ROOT_NAME, out GameObject existingRoot))
            {
                Debug.Log($"[开局选角] 场景中已存在 {existingRoot.name}，未重复创建。", existingRoot);
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                Debug.LogError("[开局选角] 无法取得 Unity 内置字体。");
                return;
            }

            EnsureEventSystem(scene);
            GameObject root = CreateCanvasRoot(scene);
            CreateFullScreenImage(root.transform, "Dimmer", new Color(0.015f, 0.025f, 0.06f, 0.82f));

            GameObject safeArea = CreateRectObject("SafeArea", root.transform);
            StretchFull(safeArea.GetComponent<RectTransform>());
            SafeAreaRectFitter safeAreaFitter = Undo.AddComponent<SafeAreaRectFitter>(safeArea);
            SetObjectReference(safeAreaFitter, "_target", safeArea.GetComponent<RectTransform>());

            GameObject panel = CreateRectObject("CharacterSelectionPanel", safeArea.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1180f, 430f);
            Image panelImage = Undo.AddComponent<Image>(panel);
            panelImage.color = new Color(0.035f, 0.065f, 0.13f, 0.96f);

            CreateText(panel.transform, "Title", "CHOOSE YOUR CHARACTER", 46,
                new Vector2(0f, 125f), new Vector2(900f, 70f), font);
            CreateText(panel.transform, "Hint", "Your companion fights alongside you and can rescue you.", 23,
                new Vector2(0f, 65f), new Vector2(1000f, 46f), font,
                new Color(0.72f, 0.79f, 0.9f, 1f));

            Button deepSeekButton = CreateButton(panel.transform, "SelectDeepSeek", "DeepSeek",
                new Vector2(-260f, -55f), new Color(0.08f, 0.38f, 0.85f, 1f), font);
            Button harnessButton = CreateButton(panel.transform, "SelectHarness", "Harness (HS)",
                new Vector2(260f, -55f), new Color(0.48f, 0.055f, 0.095f, 1f), font);

            OpeningCharacterSelectionController controller =
                Undo.AddComponent<OpeningCharacterSelectionController>(root);
            SetObjectReference(controller, "_controlAssignment", assignment);
            SetObjectReference(controller, "_menuRoot", root);
            SetObjectReference(controller, "_deepSeekButton", deepSeekButton);
            SetObjectReference(controller, "_harnessButton", harnessButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log(
                "[开局选角] 已装配灰盒UI：选择期间暂停，点击角色后通过 " +
                "PlayerControlAssignment 交接本地输入并继续游戏。",
                root);
        }

        private static GameObject CreateCanvasRoot(Scene scene)
        {
            GameObject root = new GameObject(
                ROOT_NAME,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(root, "Create opening character selection UI");
            SceneManager.MoveGameObjectToScene(root, scene);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].GetComponent<EventSystem>() != null)
                {
                    return;
                }
            }

            GameObject eventSystemObject = new GameObject(
                EVENT_SYSTEM_NAME,
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create UI EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchoredPosition,
            Color color,
            Font font)
        {
            GameObject buttonObject = CreateRectObject(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(430f, 125f);

            Image image = Undo.AddComponent<Image>(buttonObject);
            image.color = color;
            Button button = Undo.AddComponent<Button>(buttonObject);
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            CreateText(buttonObject.transform, "Label", label, 34,
                Vector2.zero, rect.sizeDelta, font);
            return button;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            Vector2 anchoredPosition,
            Vector2 size,
            Font font,
            Color? color = null)
        {
            GameObject textObject = CreateRectObject(name, parent);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text text = Undo.AddComponent<Text>(textObject);
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color ?? Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateFullScreenImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = CreateRectObject(name, parent);
            StretchFull(imageObject.GetComponent<RectTransform>());
            Image image = Undo.AddComponent<Image>(imageObject);
            image.color = color;
            return imageObject;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(result, $"Create {name}");
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(fieldName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryFindControlAssignment(
            Scene scene,
            out PlayerControlAssignment assignment)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                assignment = roots[index].GetComponent<PlayerControlAssignment>();
                if (assignment != null)
                {
                    return true;
                }
            }

            assignment = null;
            return false;
        }

        private static bool TryFindRoot(Scene scene, string name, out GameObject result)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == name)
                {
                    result = roots[index];
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
