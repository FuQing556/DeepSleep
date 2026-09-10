using System;
using System.IO;
using DeepSleep.Runtime.Players.Identity;
using UnityEngine;

namespace DeepSleep.Runtime.Progression.Economy
{
    public readonly struct TokenWalletSnapshot
    {
        public TokenWalletSnapshot(
            int deepSeekBalance,
            int harnessBalance,
            int battleEarned,
            bool isBattleSettled)
        {
            DeepSeekBalance = deepSeekBalance;
            HarnessBalance = harnessBalance;
            BattleEarned = battleEarned;
            IsBattleSettled = isBattleSettled;
        }

        public int DeepSeekBalance { get; }
        public int HarnessBalance { get; }
        public int BattleEarned { get; }
        public bool IsBattleSettled { get; }
    }

    /// <summary>
    /// 保存 DS、HS 各自余额，以及尚未结算的本轮共同战斗收益。
    /// </summary>
    public sealed class TokenWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int _deepSeekStartingBalance;
        [SerializeField, Min(0)] private int _harnessStartingBalance;

        public event Action Changed;
        public int DeepSeekBalance { get; private set; }
        public int HarnessBalance { get; private set; }
        public int BattleEarned { get; private set; }
        public bool IsBattleSettled { get; private set; }

        private void Awake()
        {
            DeepSeekBalance = Mathf.Max(0, _deepSeekStartingBalance);
            HarnessBalance = Mathf.Max(0, _harnessStartingBalance);
        }

        public int GetBalance(PlayerRole role)
        {
            return role == PlayerRole.DeepSeek
                ? DeepSeekBalance
                : HarnessBalance;
        }

        public void RecordBattleReward(int amount)
        {
            if (amount <= 0 || IsBattleSettled)
            {
                return;
            }
            BattleEarned = checked(BattleEarned + amount);
            Changed?.Invoke();
        }

        public bool SettleBattle()
        {
            if (IsBattleSettled)
            {
                return false;
            }
            DeepSeekBalance = checked(DeepSeekBalance + BattleEarned);
            HarnessBalance = checked(HarnessBalance + BattleEarned);
            IsBattleSettled = true;
            Changed?.Invoke();
            return true;
        }

        public void BeginBattle()
        {
            BattleEarned = 0;
            IsBattleSettled = false;
            Changed?.Invoke();
        }

        public bool TrySpend(PlayerRole role, int amount)
        {
            if (amount < 0 || GetBalance(role) < amount)
            {
                return false;
            }
            if (amount == 0)
            {
                return true;
            }
            if (role == PlayerRole.DeepSeek)
            {
                DeepSeekBalance -= amount;
            }
            else
            {
                HarnessBalance -= amount;
            }
            Changed?.Invoke();
            return true;
        }

        public void CreditRole(PlayerRole role, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            if (role == PlayerRole.DeepSeek)
            {
                DeepSeekBalance = checked(DeepSeekBalance + amount);
            }
            else
            {
                HarnessBalance = checked(HarnessBalance + amount);
            }
            Changed?.Invoke();
        }

        public TokenWalletSnapshot CaptureSnapshot()
        {
            return new TokenWalletSnapshot(
                DeepSeekBalance,
                HarnessBalance,
                BattleEarned,
                IsBattleSettled);
        }

        public void RestoreSnapshot(in TokenWalletSnapshot snapshot)
        {
            DeepSeekBalance = Mathf.Max(0, snapshot.DeepSeekBalance);
            HarnessBalance = Mathf.Max(0, snapshot.HarnessBalance);
            BattleEarned = Mathf.Max(0, snapshot.BattleEarned);
            IsBattleSettled = snapshot.IsBattleSettled;
            Changed?.Invoke();
        }

        public void WriteNetworkState(BinaryWriter writer)
        {
            writer.Write(DeepSeekBalance);
            writer.Write(HarnessBalance);
            writer.Write(BattleEarned);
            writer.Write(IsBattleSettled);
        }

        public void ReadNetworkState(BinaryReader reader)
        {
            DeepSeekBalance = Mathf.Max(0, reader.ReadInt32());
            HarnessBalance = Mathf.Max(0, reader.ReadInt32());
            BattleEarned = Mathf.Max(0, reader.ReadInt32());
            IsBattleSettled = reader.ReadBoolean();
            Changed?.Invoke();
        }
    }
}
