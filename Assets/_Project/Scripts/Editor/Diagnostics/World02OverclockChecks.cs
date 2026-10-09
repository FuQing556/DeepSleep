using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>旧诊断入口转到新场景状态检查，保留已有截图工具调用。</summary>
    public static class World02OverclockChecks
    {
        public static string RunAssets()=>World02SceneEffectsChecks.RunAssets();
        public static string RunPlay()=>World02SceneEffectsChecks.RunPlay();

        public static string CaptureGameView(string path)
        {
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var window=EditorWindow.GetWindow(type);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            Vector2 size=(Vector2)type.GetProperty("targetSize",flags).GetValue(window);
            Canvas.ForceUpdateCanvases();
            var rendered=(RenderTexture)type.GetMethod("RenderView",flags).Invoke(window,new object[]{size,false});
            var flipped=new RenderTexture(rendered.width,rendered.height,0);
            var previous=RenderTexture.active;
            var texture=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(rendered,flipped,new Vector2(1,-1),new Vector2(0,1));RenderTexture.active=flipped;
                texture.ReadPixels(new Rect(0,0,flipped.width,flipped.height),0,0);texture.Apply();
                System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;flipped.Release();Object.DestroyImmediate(flipped);Object.DestroyImmediate(texture);}
            return $"GameView Overlay capture {rendered.width}x{rendered.height}: {path}";
        }
    }
}
