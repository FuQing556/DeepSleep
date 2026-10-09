using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Players.LifeCycle;
using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI.Combat
{
    /// <summary>只读权限书状态；两端共用，不把倒地或其他行动门禁误报成书本封禁。</summary>
    public sealed class ClaudePermissionStatusView : MonoBehaviour
    {
        public ClaudePermissionModule2D Permissions;
        public Camera WorldCamera;
        public RectTransform CanvasRect;
        public RectTransform[] Rows;
        public CanvasGroup[] Groups;
        // DS三项在前，HS三项在后，按移动/攻击/技能固定排列。
        public Image[] Markers;
        public Text[] Labels;
        public Image[] Frames;
        public Image[] Decorations;
        public float PageReferenceSize = 120;
        [Min(.1f)] public float TriangleRadius = 1.15f;
        [Min(.1f)] public float CircleDiameter = .46f;

        private void LateUpdate() => Refresh(Time.unscaledDeltaTime);

        public void Refresh(float realDeltaSeconds)
        {
            for (int role = 0; role < 2; role++)
            {
                int mask = 0;
                if (Permissions.Lives[role].State == PlayerLifeState.Alive)
                    foreach (var book in Permissions.Books)
                        if (book.State == ClaudeBookState.Sealed && (int)book.Role == role)
                            mask |= 1 << (int)book.Permission;
                Vector3 center = Permissions.Lives[role].transform.position;
                Vector3 screen = WorldCamera.WorldToScreenPoint(center);
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, screen, null, out local);
                Rows[role].anchoredPosition = local;
                Rows[role].localScale = Vector3.one;
                Vector2 edge;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect,
                    WorldCamera.WorldToScreenPoint(center + WorldCamera.transform.right * CircleDiameter), null, out edge);
                float diameter = Vector2.Distance(local, edge);
                for (int item = 0; item < 3; item++)
                {
                    Image marker = Markers[role * 3 + item];
                    bool shown = (mask & (1 << item)) != 0;
                    marker.gameObject.SetActive(shown);
                    Vector2 offset = TriangleOffset(item, TriangleRadius);
                    Vector3 point = center + WorldCamera.transform.right * offset.x + WorldCamera.transform.up * offset.y;
                    Vector2 markerLocal;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, WorldCamera.WorldToScreenPoint(point), null, out markerLocal);
                    marker.rectTransform.anchoredPosition = markerLocal - local;
                    marker.rectTransform.sizeDelta = Vector2.one * diameter;
                    var decoration = Decorations[role * 3 + item];
                    decoration.gameObject.SetActive(shown);
                    decoration.rectTransform.anchoredPosition = marker.rectTransform.anchoredPosition;
                    decoration.rectTransform.sizeDelta = marker.rectTransform.sizeDelta * 1.15f;
                    decoration.rectTransform.localScale = marker.rectTransform.localScale;
                    Labels[role * 3 + item].rectTransform.localScale = Vector3.one * (diameter / PageReferenceSize / marker.rectTransform.localScale.x);
                    Frames[role * 3 + item].fillAmount = 1;
                    foreach (var book in Permissions.Books)
                        if (book.State == ClaudeBookState.Sealed && (int)book.Role == role && (int)book.Permission == item)
                        {
                            Frames[role * 3 + item].fillAmount = book.Progress.fillAmount;
                            break;
                        }
                }
                Groups[role].alpha = mask != 0 && screen.z > 0 ? 1 : 0;
                Groups[role].interactable = Groups[role].blocksRaycasts = false;
            }
        }

        private void OnDisable()
        {
            for (int role = 0; role < 2; role++)
            {
                if (Groups != null && role < Groups.Length && Groups[role] != null) Groups[role].alpha = 0;
            }
        }

        public static Vector2 TriangleOffset(int permission, float radius) => permission == 0
            ? new Vector2(0, radius)
            : new Vector2((permission == 1 ? 1 : -1) * Mathf.Sqrt(3) * .5f * radius, -.5f * radius);
    }
}
