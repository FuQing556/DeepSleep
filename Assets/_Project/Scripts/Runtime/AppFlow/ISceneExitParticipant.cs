using System.Collections;
using UnityEngine;

namespace DeepSleep.Runtime.AppFlow
{
    /// <summary>
    /// 显式登记到场景路由的跨场景对象。先在原场景仍存活时结束业务/异步关闭，
    /// 迭代完成后才由 Router 销毁根对象；失败必须抛出，不可带着残留连接切场景。
    /// </summary>
    public interface ISceneExitParticipant
    {
        GameObject SceneExitRoot { get; }
        IEnumerator PrepareForSceneExit();
    }
}
