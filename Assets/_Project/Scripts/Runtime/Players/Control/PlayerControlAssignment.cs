using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Players.Control
{
    /// <summary>
    /// 按角色选择把本地控制与可选的同伴控制源分配给现有玩家实体。
    /// 角色本身不永久归属于真人、AI或网络。
    /// </summary>
    public sealed class PlayerControlAssignment : MonoBehaviour
    {
        [SerializeField] private PlayerActor _deepSeekActor;
        [SerializeField] private PlayerActor _harnessActor;
        [SerializeField] private MonoBehaviour _localCommandSourceComponent;
        [SerializeField] private MonoBehaviour _companionCommandSourceComponent;
        [SerializeField] private PlayerRole _initialLocalPlayerRole;

        private ICommandSource _localCommandSource;
        private ICommandSource _companionCommandSource;
        private bool _isInitialized;

        public PlayerRole CurrentLocalPlayerRole { get; private set; }

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(PlayerControlAssignment)}] " +
                    $"玩家控制分配装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _localCommandSource =
                (ICommandSource)_localCommandSourceComponent;
            _companionCommandSource =
                _companionCommandSourceComponent as ICommandSource;
            _isInitialized = true;
        }

        private void Start()
        {
            if (!TryAssign(
                    _initialLocalPlayerRole,
                    _localCommandSource,
                    _companionCommandSource))
            {
                Debug.LogError(
                    $"[{nameof(PlayerControlAssignment)}] " +
                    "初始角色控制分配失败。",
                    this);
                enabled = false;
            }
        }

        /// <summary>
        /// 将本地控制源交给所选角色，并把另一控制源交给未选角色。
        /// companionCommandSource 为空时，未选角色保持静止等待接管。
        /// </summary>
        public bool TryAssign(
            PlayerRole localPlayerRole,
            ICommandSource localCommandSource,
            ICommandSource companionCommandSource)
        {
            if (!_isInitialized || localCommandSource == null)
            {
                return false;
            }

            if (ReferenceEquals(
                    localCommandSource,
                    companionCommandSource))
            {
                return false;
            }

            PlayerActor localActor;
            PlayerActor companionActor;

            switch (localPlayerRole)
            {
                case PlayerRole.DeepSeek:
                    localActor = _deepSeekActor;
                    companionActor = _harnessActor;
                    break;

                case PlayerRole.Harness:
                    localActor = _harnessActor;
                    companionActor = _deepSeekActor;
                    break;

                default:
                    return false;
            }

            companionActor.CommandDispatcher.ClearCommandSource();

            if (!localActor.CommandDispatcher.TryBindCommandSource(
                    localCommandSource))
            {
                return false;
            }

            if (companionCommandSource != null &&
                !companionActor.CommandDispatcher.TryBindCommandSource(
                    companionCommandSource))
            {
                localActor.CommandDispatcher.ClearCommandSource();
                return false;
            }

            CurrentLocalPlayerRole = localPlayerRole;
            return true;
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (!TryValidateActor(
                    _deepSeekActor,
                    PlayerRole.DeepSeek,
                    out reason))
            {
                return false;
            }

            if (!TryValidateActor(
                    _harnessActor,
                    PlayerRole.Harness,
                    out reason))
            {
                return false;
            }

            if (_localCommandSourceComponent is not ICommandSource)
            {
                reason = "本地命令源必须实现 ICommandSource。";
                return false;
            }

            if (_companionCommandSourceComponent != null &&
                _companionCommandSourceComponent is not ICommandSource)
            {
                reason = "同伴命令源必须实现 ICommandSource。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool TryValidateActor(
            PlayerActor actor,
            PlayerRole expectedRole,
            out string reason)
        {
            if (actor == null)
            {
                reason = $"未配置 {expectedRole} 玩家实体。";
                return false;
            }

            if (!actor.TryValidateConfiguration(out reason))
            {
                return false;
            }

            if (actor.Definition.Role != expectedRole)
            {
                reason =
                    $"{actor.name} 的角色定义不是 {expectedRole}。";
                return false;
            }

            return true;
        }
    }
}
