using System;
using DeepSleep.Runtime.Combat.Perception;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    /// <summary>权限轴的一批书；未来遭遇驱动显式触发/推进，不私自定首抽或轮次间隔。</summary>
    public sealed class ClaudePermissionModule2D : MonoBehaviour
    {
        public ClaudePermissionConfig Config;
        public ClaudePermissionBook2D[] Books;
        public Transform[] Corners;
        public PlayerActionGate[] Gates;
        public PlayerLifeStateController2D[] Lives;
        public HarnessMeleeController HarnessMelee;
        public CoopSessionController Session;
        public CombatPerceptionRegistry2D Registry;
        private readonly int[] _cornerOrder = new int[4];
        public bool HasOpenBooks
        { get { foreach (var book in Books) if (book.IsOpen) return true; return false; } }

        public bool TryValidateConfiguration(out string reason)
        {
            if (Config == null || !Config.TryValidate(out reason)) { reason = "权限配置缺失或无效。"; return false; }
            if (Books == null || Books.Length != 4 || Corners == null || Corners.Length != 4 ||
                Gates == null || Gates.Length != 2 || Lives == null || Lives.Length != 2 || Registry == null || HarnessMelee == null)
            { reason = "须显式配置四本书/四角、DS与HS门禁/生命、HS近战及感知。"; return false; }
            for (int i = 0; i < 4; i++)
            {
                if (Books[i] == null || !Books[i].TryValidateConfiguration(out reason) || Corners[i] == null) return false;
                for (int j = 0; j < i; j++)
                    if (Books[i] == Books[j] || Corners[i] == Corners[j]) { reason = "书或角落重复。"; return false; }
            }
            if (Gates[0] == null || Gates[1] == null || Lives[0] == null || Lives[1] == null ||
                Gates[0] == Gates[1] || Lives[0] == Lives[1]) { reason = "两角色绑定不能缺失或重复。"; return false; }
            reason = string.Empty; return true;
        }

        /// <summary>六项固定权限是算法全集；合法组合均匀抽，不靠重抽上限偏置概率。</summary>
        public static bool IsLegalMask(int mask, int count, bool dsAlive, bool hsAlive)
        {
            if (mask <= 0 || mask >= (1 << 6) || CountBits(mask) != count) return false;
            int ds = mask & 7, hs = (mask >> 3) & 7;
            if ((!dsAlive && ds != 0) || (!hsAlive && hs != 0) || ds == 7 || hs == 7) return false;
            bool dsCanAttack = dsAlive && (ds & 2) == 0;
            bool hsCanAttack = hsAlive && (hs & 2) == 0;
            return dsCanAttack || hsCanAttack;
        }

        private static int CountBits(int mask)
        { int count = 0; while (mask != 0) { mask &= mask - 1; count++; } return count; }

        private bool IsAvailableMask(int mask, int count)
        {
            bool dsAlive = Lives[0].State == PlayerLifeState.Alive;
            bool hsAlive = Lives[1].State == PlayerLifeState.Alive;
            bool dsAttack = dsAlive && !Gates[0].IsBlocked(PlayerActionBlock.AutomaticCombat | PlayerActionBlock.PrimaryAttack);
            bool hsAttack = hsAlive && !Gates[1].IsBlocked(PlayerActionBlock.ActiveCombat | PlayerActionBlock.PrimaryAttack);
            return IsLegalMask(mask, count, dsAlive, hsAlive) &&
                ((dsAttack && (mask & 2) == 0) || (hsAttack && (mask & (2 << 3)) == 0));
        }

        public bool CanBeginBatch(bool phaseTwo)
        {
            if (!isActiveAndEnabled || !TryValidateConfiguration(out _) || HasOpenBooks ||
                (Session != null && Session.Phase != SessionPhase.Offline && !Session.IsAuthority)) return false;
            for (int mask = 1; mask < (1 << 6); mask++)
                if (IsAvailableMask(mask, phaseTwo ? 4 : 2)) return true;
            return false;
        }

        public bool BeginBatch(bool phaseTwo, int seed)
        {
            if (!CanBeginBatch(phaseTwo)) return false;
            int count = phaseTwo ? 4 : 2;
            // 此批不能再封掉剩下的破书路线；外部门禁解除后由遭遇轴重试，不清别人的限制。
            var random = new System.Random(seed);
            int chosen = 0, legalCount = 0;
            for (int mask = 1; mask < (1 << 6); mask++)
            {
                if (!IsAvailableMask(mask, count)) continue;
                legalCount++;
                if (random.Next(legalCount) == 0) chosen = mask;
            }
            if (chosen == 0) return false;
            for (int i = 0; i < 4; i++) _cornerOrder[i] = i;
            for (int i = 0; i < count; i++)
            { int j = random.Next(i, 4); int temp = _cornerOrder[i]; _cornerOrder[i] = _cornerOrder[j]; _cornerOrder[j] = temp; }
            int index = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                if ((chosen & (1 << bit)) == 0) continue;
                int role = bit / 3;
                if (!Books[index].Open(Corners[_cornerOrder[index]].position, (PlayerRole)role,
                    (ClaudePermission)(bit % 3), Config, phaseTwo, Gates[role], Lives[role], Session, Registry))
                { Clear(); return false; }
                // 只镜像右侧书图，书页文字与身份圆圈保持可读方向；复用时每次重设。
                Books[index].Visual.flipX = _cornerOrder[index] == 1 || _cornerOrder[index] == 2;
                index++;
            }
            ApplySkillSeal();
            return true;
        }
        public void Advance(float realSeconds)
        {
            foreach (var book in Books) book.Advance(realSeconds);
            ApplySkillSeal();
        }

        private void ApplySkillSeal()
        {
            // 近战属于技能。封技后结束其姿态，不能连同未封禁的激光也锁死。
            // 不解封任何权限；队友倒地仍按原规则挣扎至破书或到期。
            if (HarnessMelee == null || !HarnessMelee.IsMelee) return;
            foreach (var book in Books)
                if (book.State == ClaudeBookState.Sealed && book.Role == PlayerRole.Harness &&
                    book.Permission == ClaudePermission.Skill)
                { HarnessMelee.CancelCombatSequence(); return; }
        }
        /// <summary>固定四书复用缓冲；未来整场快照通道直接读写，不分配列表。</summary>
        public bool CaptureSnapshot(ClaudeBookSnapshot[] frames)
        {
            if (frames == null || frames.Length != 4) return false;
            for (int i = 0; i < 4; i++) frames[i] = Books[i].CaptureSnapshot();
            return true;
        }

        public bool ApplyReplica(ClaudeBookSnapshot[] frames)
        {
            if (!isActiveAndEnabled || frames == null || frames.Length != 4 ||
                (Session != null && (Session.Phase == SessionPhase.Offline || Session.IsAuthority))) return false;
            int permissions = 0;
            for (int i = 0; i < 4; i++)
            {
                if (Books[i].IsAuthorityBook || !frames[i].IsValid(Config)) return false;
                if (frames[i].State == ClaudeBookState.Hidden) continue;
                int bit = 1 << ((int)frames[i].Role * 3 + (int)frames[i].Permission);
                if ((permissions & bit) != 0) return false;
                permissions |= bit;
            }
            for (int i = 0; i < 4; i++)
                Books[i].ApplyReplica(in frames[i], Config, Gates[(int)frames[i].Role]);
            ApplySkillSeal();
            return true;
        }
        public void Clear() { if (Books != null) foreach (var book in Books) if (book != null) book.Clear(); }
        private void OnDisable() => Clear();
    }
}
