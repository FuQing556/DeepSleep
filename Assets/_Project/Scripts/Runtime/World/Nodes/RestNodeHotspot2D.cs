using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.World.Nodes
{
    /// <summary>
    /// 休息节点中的透明空间触发器。它只报告进入/离开，
    /// 文本选择和本地玩家过滤由节点控制器统一处理。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class RestNodeHotspot2D : MonoBehaviour
    {
        [SerializeField] private RestNodePrototypeController2D _owner;
        [SerializeField] private RestNodeHotspotKind _kind;
        [SerializeField, TextArea] private string _prompt;
        [SerializeField] private bool _restrictRole;
        [SerializeField] private PlayerRole _requiredRole;

        public string Prompt => _prompt;
        public RestNodeHotspotKind Kind => _kind;

        public void Configure(
            RestNodePrototypeController2D owner,
            RestNodeHotspotKind kind,
            string prompt,
            bool restrictRole,
            PlayerRole requiredRole)
        {
            _owner = owner;
            _kind = kind;
            _prompt = prompt;
            _restrictRole = restrictRole;
            _requiredRole = requiredRole;
        }

        private void Awake()
        {
            if (_owner == null)
            {
                _owner = GetComponentInParent<
                    RestNodePrototypeController2D>();
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_owner == null)
            {
                reason = "未配置所属休息节点。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_prompt))
            {
                reason = "未配置交互提示。";
                return false;
            }

            BoxCollider2D trigger = GetComponent<BoxCollider2D>();
            if (trigger == null || !trigger.isTrigger || !trigger.enabled)
            {
                reason = "需要启用的触发型 BoxCollider2D。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool Allows(PlayerActor actor)
        {
            return actor != null &&
                (!_restrictRole ||
                 actor.Definition.Role == _requiredRole);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerActor actor = other != null
                ? other.GetComponentInParent<PlayerActor>()
                : null;
            if (actor != null)
            {
                _owner?.NotifyEntered(this, actor);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            PlayerActor actor = other != null
                ? other.GetComponentInParent<PlayerActor>()
                : null;
            if (actor != null)
            {
                _owner?.NotifyExited(this, actor);
            }
        }
    }
}
