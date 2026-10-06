using System;
using System.Linq;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>受控离线测试只读取正式装配，缺失时失败，禁止临时补装掩盖场景遗漏。</summary>
    internal sealed class KimiChapterVerificationRig : IDisposable
    {
        public readonly KimiChapterEncounterDriver2D Driver;
        public readonly Sprite Day;
        public KimiChapterVerificationRig(ChapterRunController chapter)
        {
            if (!EditorApplication.isPlaying || chapter.LevelBindings.Session.HasPeer)
                throw new InvalidOperationException("Offline Play verification only.");
            Driver = chapter.CombatWorld.ParticipantComponents.OfType<KimiChapterEncounterDriver2D>().SingleOrDefault();
            if (Driver == null) throw new InvalidOperationException("World01 is missing its authored Kimi driver.");
            if (!Driver.TryValidateConfiguration(out string reason)) throw new InvalidOperationException(reason);
            Day = Driver.Backdrop.sprite;
            Driver.ResetForSegment(chapter.SegmentNumber);
        }
        public void Dispose() { } // 正式对象由章节生命周期清理，不由测试销毁。
    }
}
