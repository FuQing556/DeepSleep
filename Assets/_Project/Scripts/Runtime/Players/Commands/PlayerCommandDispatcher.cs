using DeepSleep.Runtime.Input.Commands;
using DeepSleep.Runtime.Players.Actions;
using UnityEngine;
using UnityEngine.Serialization;

namespace DeepSleep.Runtime.Players.Commands
{
    /// <summary>
    /// 每个模拟刻读取一次控制源，并把同一份命令交给玩家的各个玩法模块。
    /// </summary>
    public sealed class PlayerCommandDispatcher : MonoBehaviour
    {
        [FormerlySerializedAs("commandSourceComponent")]
        [SerializeField] private MonoBehaviour _initialCommandSourceComponent;

        [FormerlySerializedAs("commandConsumerComponents")]
        [SerializeField] private MonoBehaviour[] _commandConsumerComponents;

        [SerializeField] private PlayerActionGate _actionGate;
        [SerializeField] private MonoBehaviour[] _commandPreprocessorComponents;

        private ICommandSource _commandSource;
        private IPlayerCommandConsumer[] _commandConsumers;
        private IPlayerCommandPreprocessor[] _commandPreprocessors;
        private bool _isInitialized;

        public bool HasCommandSource => _commandSource != null;

        private void Awake()
        {
            if (!TryResolveCommandConsumers())
            {
                enabled = false;
                return;
            }

            if (!TryResolveCommandPreprocessors())
            {
                enabled = false;
                return;
            }

            if (!TryResolveInitialCommandSource())
            {
                enabled = false;
                return;
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 把真人、AI或网络命令源绑定到现有角色实例。
        /// 可在选角、掉线接管和重连交还时替换，不重建角色。
        /// </summary>
        public bool TryBindCommandSource(ICommandSource commandSource)
        {
            if (commandSource == null)
            {
                return false;
            }

            _commandSource = commandSource;
            return true;
        }

        /// <summary>
        /// 仅当调用方仍是当前控制源时解除绑定，防止旧控制源误解绑新控制源。
        /// </summary>
        public void UnbindCommandSource(ICommandSource commandSource)
        {
            if (ReferenceEquals(_commandSource, commandSource))
            {
                _commandSource = null;
            }
        }

        /// <summary>
        /// 清除当前控制源，使角色保持存在但不再接收控制命令。
        /// 用于选角切换和等待AI或网络控制源接管。
        /// </summary>
        public void ClearCommandSource()
        {
            _commandSource = null;
        }

        public void Simulate(uint simulationTick, float deltaTime)
        {
            if (!_isInitialized ||
                !isActiveAndEnabled ||
                _commandSource == null)
            {
                return;
            }

            if (!_commandSource.TryGetCommand(
                    simulationTick,
                    out PlayerCommand command))
            {
                return;
            }

            for (int index = 0; index < _commandPreprocessors.Length; index++)
            {
                _commandPreprocessors[index].PreprocessCommand(
                    in command,
                    deltaTime);
            }

            for (int index = 0; index < _commandConsumers.Length; index++)
            {
                IPlayerCommandConsumer consumer = _commandConsumers[index];

                if (consumer is IPlayerActionCommandConsumer actionConsumer &&
                    _actionGate.IsBlocked(actionConsumer.ActionCategory))
                {
                    continue;
                }

                consumer.ConsumeCommand(
                    in command,
                    deltaTime);
            }
        }

        private bool TryResolveInitialCommandSource()
        {
            if (_initialCommandSourceComponent == null)
            {
                return true;
            }

            if (_initialCommandSourceComponent is ICommandSource resolvedSource)
            {
                _commandSource = resolvedSource;
                return true;
            }

            Debug.LogError(
                $"[{nameof(PlayerCommandDispatcher)}] " +
                "初始命令源必须实现 ICommandSource。",
                this);
            return false;
        }

        private bool TryResolveCommandConsumers()
        {
            if (_commandConsumerComponents == null ||
                _commandConsumerComponents.Length == 0)
            {
                Debug.LogError(
                    $"[{nameof(PlayerCommandDispatcher)}] 至少需要一个命令消费者。",
                    this);
                return false;
            }

            _commandConsumers =
                new IPlayerCommandConsumer[_commandConsumerComponents.Length];

            for (int index = 0;
                 index < _commandConsumerComponents.Length;
                 index++)
            {
                if (_commandConsumerComponents[index] is
                    IPlayerCommandConsumer resolvedConsumer)
                {
                    _commandConsumers[index] = resolvedConsumer;
                    continue;
                }

                Debug.LogError(
                    $"[{nameof(PlayerCommandDispatcher)}] " +
                    $"第 {index} 个命令消费者未实现" +
                    $"{nameof(IPlayerCommandConsumer)}。",
                    this);
                return false;
            }

            return true;
        }

        private bool TryResolveCommandPreprocessors()
        {
            if (_actionGate == null)
            {
                Debug.LogError(
                    $"[{nameof(PlayerCommandDispatcher)}] " +
                    "未配置玩家行动限制器。",
                    this);
                return false;
            }

            int count = _commandPreprocessorComponents?.Length ?? 0;
            _commandPreprocessors = new IPlayerCommandPreprocessor[count];

            for (int index = 0; index < count; index++)
            {
                if (_commandPreprocessorComponents[index] is
                    IPlayerCommandPreprocessor resolvedPreprocessor)
                {
                    _commandPreprocessors[index] = resolvedPreprocessor;
                    continue;
                }

                Debug.LogError(
                    $"[{nameof(PlayerCommandDispatcher)}] " +
                    $"第 {index} 个命令前置处理器未实现" +
                    $"{nameof(IPlayerCommandPreprocessor)}。",
                    this);
                return false;
            }

            return true;
        }
    }
}
