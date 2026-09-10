using System.IO;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Economy
{
    /// <summary>
    /// 只在敌人被击败时按固定权重发放 1–5 Token。
    /// 联机时只有主机抽取并广播余额。
    /// </summary>
    public sealed class EnemyTokenRewardController : MonoBehaviour
    {
        private const byte NetworkBalance = 44;

        [SerializeField] private TokenWallet _wallet;
        [SerializeField] private EnemyActorPool2D[] _enemyPools;
        [SerializeField] private CoopSessionController _session;

        private bool IsOnline =>
            _session != null && _session.Phase == SessionPhase.Playing;
        private bool CanReward => !IsOnline || _session.IsAuthority;

        private void Awake()
        {
            if (_wallet == null || _enemyPools == null ||
                _enemyPools.Length == 0)
            {
                Debug.LogError(
                    $"[{nameof(EnemyTokenRewardController)}] " +
                    "未配置钱包或敌人池。",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_enemyPools != null)
            {
                for (int index = 0; index < _enemyPools.Length; index++)
                {
                    if (_enemyPools[index] != null)
                    {
                        _enemyPools[index].ActorDespawned += OnActorDespawned;
                    }
                }
            }
            if (_session != null)
            {
                _session.AuthorityMessage += ReadAuthorityMessage;
                _session.PeerJoined += BroadcastBalance;
            }
        }

        private void OnDisable()
        {
            if (_enemyPools != null)
            {
                for (int index = 0; index < _enemyPools.Length; index++)
                {
                    if (_enemyPools[index] != null)
                    {
                        _enemyPools[index].ActorDespawned -= OnActorDespawned;
                    }
                }
            }
            if (_session != null)
            {
                _session.AuthorityMessage -= ReadAuthorityMessage;
                _session.PeerJoined -= BroadcastBalance;
            }
        }

        private void OnActorDespawned(EnemyDespawnRequest2D request)
        {
            if (!CanReward || request.Reason != EnemyDespawnReason.Defeated)
            {
                return;
            }

            _wallet.RecordBattleReward(
                RollTokenCount(Random.Range(0, 100)));
            BroadcastBalance();
        }

        public static int RollTokenCount(int roll)
        {
            int value = Mathf.Clamp(roll, 0, 99);
            if (value < 10) return 1;
            if (value < 35) return 2;
            if (value < 65) return 3;
            if (value < 90) return 4;
            return 5;
        }

        public void BroadcastBalance()
        {
            if (IsOnline && _session.IsAuthority)
            {
                _session.SendAuthority(
                    NetworkBalance,
                    _wallet.WriteNetworkState,
                    reliable: true);
            }
        }

        private void ReadAuthorityMessage(byte kind, BinaryReader reader)
        {
            if (kind == NetworkBalance && IsOnline && !_session.IsAuthority)
            {
                _wallet.ReadNetworkState(reader);
            }
        }
    }
}
