using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Setup
{
    public static class MenuVerticalScrollbarsInstaller
    {
        public static string Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            var active=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/MainMenu.unity");
            bool loaded=scene.IsValid()&&scene.isLoaded;
            if(!loaded) scene=EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity",OpenSceneMode.Additive);
            try
            {
                var scrolls=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ScrollRect>(true)).ToArray();
                var template=scrolls.Single(s=>s.name=="LevelCardScroll").horizontalScrollbar;
                foreach(var scroll in scrolls.Where(s=>s.name=="ListScroll"))
                {
                    var name=scroll.transform.parent.name;
                    if(name!="WhaleVoucherShop"&&name!="Inventory"&&name!="Achievements") continue;
                    var bar=scroll.verticalScrollbar;
                    if(bar==null) bar=UnityEngine.Object.Instantiate(template,scroll.transform.parent);
                    bar.name=name+"VerticalScrollbar";
                    var rect=(RectTransform)bar.transform;
                    var list=(RectTransform)scroll.transform;
                    rect.anchorMin=rect.anchorMax=Vector2.one*.5f;
                    rect.anchoredPosition=list.anchoredPosition+new Vector2(list.sizeDelta.x*.5f+28,0);
                    // 整体旋转现有横条，保留美术长轴与拖拽局部坐标的一致性。
                    rect.sizeDelta=new Vector2(list.sizeDelta.y,44);
                    rect.localRotation=Quaternion.Euler(0,0,90);
                    bar.direction=Scrollbar.Direction.LeftToRight;
                    bar.onValueChanged=new Scrollbar.ScrollEvent();
                    scroll.verticalScrollbar=bar;
                    scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
                    scroll.vertical=true; scroll.horizontal=false;
                    bar.gameObject.SetActive(true);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                return "Shop, inventory and achievements have themed vertical scrollbars.";
            }
            finally { if(!loaded) EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(active); }
        }
    }
}
