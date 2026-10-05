using System;
using System.Collections.Generic;
using System.Linq;
using DeepSleep.Runtime.Combat.Beams.Presentation;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Combat.Weapons.Harness.Presentation;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Players.Control;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.Revive.Presentation;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.Presentation.DamageNumbers;
using DeepSleep.Runtime.Presentation.Effects;
using DeepSleep.Runtime.Progression.Run;
using DeepSleep.Runtime.World.Nodes;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Networking
{
    /// <summary>Editor 装配共用的权限规则；只过滤已知客户端表现，保留未知模块及原有相对顺序。</summary>
    public static class NetworkAuthorityRules
    {
        public static bool IsFeedbackPresentation(Behaviour component) =>
            component is SpriteHitFlash2D || component is PlayerHitFeedbackPresenter2D ||
            component is PlayerHitFeedbackOptions || component is HarnessLaserHitEffectPresenter2D ||
            component is CombatDamageNumberPresenter2D || component is DamageNumberEntryView ||
            component is HarnessLaserHitEffect2D;

        public static bool MustRemainEnabled(Behaviour component) =>
            IsFeedbackPresentation(component) || component is ChapterCombatWorld2D || component is RestNodeCombatGate ||
            component is NetworkCombatFeedbackChannel || component is NetworkEffectEventChannel ||
            component is NetworkWorldSnapshotChannel || component is DoubaoEncounterNetworkChannel || component is OneShotSpriteEffectPool2D ||
            component is NetworkPlayerHitFeedbackChannel || component is HarnessTerminalLaserPresenter ||
            component is HarnessMeleePresenter2D || component is MeleeWaveView2D || component is BeamTiledMeshView2D ||
            component is DeepSeekRiceGuardOrbitView2D || component is DeepSeekRiceGuardCircleView2D ||
            component is PlayerReviveHealingParticleView2D || component is PlayerReviveConvergeRingView2D ||
            component is PlayerReviveProtectionView2D;

        public static Behaviour[] FilterAuthority(IEnumerable<Behaviour> previous, params Behaviour[] additions)
        {
            if (previous == null) throw new InvalidOperationException("AuthorityOnly is missing; bind it explicitly.");
            var result = new List<Behaviour>();
            foreach (var value in previous)
            {
                if (value == null) throw new InvalidOperationException("AuthorityOnly contains a missing reference; resolve it before applying.");
                if (!MustRemainEnabled(value)) result.Add(value);
            }
            foreach (var value in additions)
            {
                if (value == null) throw new InvalidOperationException("Cannot register a missing authority component.");
                if (!MustRemainEnabled(value) && !result.Contains(value)) result.Add(value);
            }
            return result.ToArray();
        }

        public static void Apply(NetworkAuthorityGate gate, params Behaviour[] additions)
        {
            if (gate == null) throw new InvalidOperationException("Explicit authority gate is required.");
            var desired = FilterAuthority(gate.AuthorityOnly, additions);
            if (gate.AuthorityOnly.SequenceEqual(desired)) return;
            Undo.RecordObject(gate, "Apply shared network authority rules");
            gate.AuthorityOnly = desired;
            EditorUtility.SetDirty(gate);
        }

        /// <summary>仅供未登记旧场景首次初始化；现役关卡不得重新扫描生成权限列表。</summary>
        public static bool IsLegacyAuthorityCandidate(MonoBehaviour component)
        {
            if (component == null || MustRemainEnabled(component)) return false;
            string ns = component.GetType().Namespace ?? string.Empty;
            if (!ns.StartsWith("DeepSleep.Runtime", StringComparison.Ordinal) || ns.Contains("Networking") ||
                ns.Contains(".UI.") || ns.Contains(".Input.") || ns.Contains(".World.") ||
                ns.Contains(".Orientation") || ns.Contains("Presentation.Effects")) return false;
            return component is not PlayerActor && component is not PlayerControlAssignment;
        }
    }
}
