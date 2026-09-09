using System;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>本机重连票据，不是平台账号或付费身份。只经 WSS/可信局域网发送，禁止写日志。</summary>
    public static class NetworkClientIdentity
    {
        private const string Key = "DeepSleep.Network.ReconnectTicket.v1";
        private static string _processTicket;
        private static bool _warnedAboutPersistence;

        public static string LoadOrCreate()
        {
            if (Guid.TryParseExact(_processTicket, "N", out _)) return _processTicket;

            try
            {
                string saved = PlayerPrefs.GetString(Key, "");
                if (Guid.TryParseExact(saved, "N", out _)) return _processTicket = saved;
            }
            catch (Exception exception)
            {
                WarnOnce(exception);
            }

            _processTicket = Guid.NewGuid().ToString("N");
            try
            {
                PlayerPrefs.SetString(Key, _processTicket);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                // 存储不可用时只失去“重启应用后重连”；当前进程的断线重连仍保留同一票据。
                WarnOnce(exception);
            }
            return _processTicket;
        }

        private static void WarnOnce(Exception exception)
        {
            if (_warnedAboutPersistence) return;
            _warnedAboutPersistence = true;
            Debug.LogWarning("[NetworkIdentity] 无法持久化重连身份；本次运行仍可重连。" +
                " 存储恢复前，退出应用后不能保留原房间席位。原因类型：" + exception.GetType().Name);
        }
    }
}
