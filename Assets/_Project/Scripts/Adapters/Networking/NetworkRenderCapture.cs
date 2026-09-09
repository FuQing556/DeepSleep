using DeepSleep.Runtime.Networking;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeepSleep.Adapters.Networking
{
    public sealed class NetworkRenderCapture : MonoBehaviour, INetworkTestCapture
    {
        public Camera Camera;
        public void Capture(string path)
        {
            if (!Debug.isDebugBuild || Camera == null) return;
            var rt = RenderTexture.GetTemporary(960,540,24,RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(Camera,new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt;
                image = new Texture2D(960,540,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,960,540),0,0); image.Apply();
                System.IO.File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); if (image != null) Destroy(image); }
        }
    }
}
