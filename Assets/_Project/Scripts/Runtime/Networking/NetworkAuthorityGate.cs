using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>
    /// 显式列出仅主机执行的组件。客人不能产生伤害/生成敌人/运行同伴AI。
    /// 不在运行时搜索场景，不改用户的 Collider 几何参数。
    /// </summary>
    public sealed class NetworkAuthorityGate : MonoBehaviour
    {
        public CoopSessionController Session;
        public Behaviour[] AuthorityOnly;
        public Rigidbody2D[] AuthorityBodies;
        private bool[] _enabled, _simulated;
        private bool _clientMode;

        private void OnEnable()
        {
            if (Session == null) { Debug.LogError("[NetworkAuthorityGate] 缺少 Session。", this); return; }
            Session.SessionOpened += Open; Session.SessionClosed += Restore;
        }
        private void OnDisable()
        {
            if (Session == null) return;
            Session.SessionOpened -= Open; Session.SessionClosed -= Restore;
            Restore();
        }
        private void Open(bool authority)
        {
            if (authority || _clientMode) return;
            _clientMode = true; _enabled = new bool[AuthorityOnly.Length]; _simulated = new bool[AuthorityBodies.Length];
            for (int i = 0; i < AuthorityOnly.Length; i++)
            { _enabled[i] = AuthorityOnly[i].enabled; AuthorityOnly[i].enabled = false; }
            for (int i = 0; i < AuthorityBodies.Length; i++)
            { _simulated[i] = AuthorityBodies[i].simulated; AuthorityBodies[i].simulated = false; }
        }
        private void Restore()
        {
            if (!_clientMode) return;
            _clientMode = false;
            for (int i = 0; i < AuthorityOnly.Length; i++)
                if (AuthorityOnly[i] != null) AuthorityOnly[i].enabled = _enabled[i];
            for (int i = 0; i < AuthorityBodies.Length; i++)
                if (AuthorityBodies[i] != null) AuthorityBodies[i].simulated = _simulated[i];
        }
    }
}
