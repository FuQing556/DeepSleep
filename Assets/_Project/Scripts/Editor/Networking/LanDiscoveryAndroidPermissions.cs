#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace DeepSleep.Editor.Networking
{
    /// <summary>向Unity生成的清单追加局域网发现权限，保留引擎Activity/入口配置。</summary>
    public sealed class LanDiscoveryAndroidPermissions : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = new XmlDocument(); document.Load(manifest);
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (string permission in new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE",
                "android.permission.ACCESS_WIFI_STATE", "android.permission.CHANGE_WIFI_MULTICAST_STATE" })
            {
                bool found = false;
                foreach (XmlNode node in document.GetElementsByTagName("uses-permission"))
                    if (node.Attributes?["name", android]?.Value == permission) found = true;
                if (found) continue;
                var element = document.CreateElement("uses-permission");
                element.SetAttribute("name", android, permission);
                document.DocumentElement.AppendChild(element);
            }
            document.Save(manifest);
        }
    }
}
#endif
