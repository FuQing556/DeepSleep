using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.AppFlow;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Presentation.Audio;
using DeepSleep.Runtime.Progression.Bestiary;
using DeepSleep.Runtime.Progression.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Diagnostics
{
    public static class AchievementCompletionChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Private).SetValue(instance, value);

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required.");
            if (GameAppRoot.Instance != null) throw new InvalidOperationException("A live app root must not be present during isolated profile checks.");
            int checks = 0;
            void Check(bool ok, string reason) { checks++; if (!ok) throw new Exception(reason); }
            string temporary = Path.Combine(Path.GetTempPath(), "DeepSleep_Achievements_" + Guid.NewGuid().ToString("N"));
            var boot = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Boot.unity");
            try
            {
                var service = All<AchievementService>(boot).Single();
                var profile = All<LocalPlayerProfileStore>(boot).Single();
                Set(profile, "TestSaveDirectory", temporary);
                typeof(LocalPlayerProfileStore).GetMethod("Awake", Private).Invoke(profile, null);
                Check(service.TryValidateConfiguration(out _), "Valid service");
                var definitions = service.Definitions;
                Check(definitions.Length == 15 && definitions.Select(d => d.AchievementId).Distinct().Count() == 15, "15 distinct registered achievements");
                foreach (string id in new[] { "first_flight", "first_clear", "first_revive", "first_merit", "flawless_clear" })
                    Check(definitions.Any(d => d.AchievementId == id), "Stable old identity " + id);
                foreach (var definition in definitions) Check(definition.TryValidate(out _), "Valid definition " + definition.AchievementId);
                int unlocks = 0; service.Unlocked += _ => unlocks++;
                // 临时目录的密集原子替换曾遇到Windows文件占用；测试按分帧节奏写入，不改运行时存档算法。
                void SettleSave() => System.Threading.Thread.Sleep(100);
                foreach (string trigger in new[] { AchievementTriggerIds.JourneyStarted, AchievementTriggerIds.LevelCleared,
                    AchievementTriggerIds.ProductPurchased, AchievementTriggerIds.FlawlessLevelCleared }) { SettleSave(); service.Report(trigger); }
                service.ReportLevelCleared(AchievementCompletionInstaller.Level("PrototypeSky"));
                Check(!profile.IsAchievementUnlocked("world01_clear") && !profile.IsAchievementUnlocked("world02_clear"), "Sky cannot unlock world achievements");
                SettleSave(); service.ReportLevelCleared(AchievementCompletionInstaller.Level("World01_EarlyInternet"));
                Check(profile.IsAchievementUnlocked("world01_clear") && !profile.IsAchievementUnlocked("world02_clear"), "World01 identity");
                SettleSave(); service.ReportLevelCleared(AchievementCompletionInstaller.Level("World02_2066"));
                Check(profile.IsAchievementUnlocked("world02_clear"), "World02 identity");

                foreach (string name in new[] { "Gameplay_Prototype", "World01_EarlyInternet", "World02_2066" })
                {
                    var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/" + name + ".unity");
                    try
                    {
                        var reporter = All<GameplayAchievementReporter>(scene).Single();
                        var so = new SerializedObject(reporter);
                        var audio = (CombatAudioPresenter)so.FindProperty("_facts").objectReferenceValue;
                        Check(audio == All<CombatAudioPresenter>(scene).Single(), "Explicit fact source " + name);
                        Check(so.FindProperty("_bindings").arraySize == 7, "Seven mappings " + name);
                        Set(reporter, "_service", service);
                    }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
                // Exercise the real presentation->reporter->service chain without playing any sound.
                var world = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
                try
                {
                    var reporter = All<GameplayAchievementReporter>(world).Single();
                    var audio = All<CombatAudioPresenter>(world).Single();
                    Set(reporter, "_service", service);
                    var callback = (Action<NetworkMessageCatalog.CombatPresentationKind>)Delegate.CreateDelegate(
                        typeof(Action<NetworkMessageCatalog.CombatPresentationKind>), reporter, typeof(GameplayAchievementReporter).GetMethod("OnFact", Private));
                    audio.FactPresented += callback;
                    var present = typeof(CombatAudioPresenter).GetMethod("Present", Private);
                    void Fact(NetworkMessageCatalog.CombatPresentationKind fact) { SettleSave(); present.Invoke(audio, new object[] { fact, DeepSleep.Runtime.Players.Identity.PlayerRole.DeepSeek, Vector2.zero }); }
                    var challengeProperty = audio.Chapter.GetType().GetProperty("Challenge");
                    int beforeChallengeFacts = unlocks;
                    challengeProperty.SetValue(audio.Chapter, definitions.Single(d => d.AchievementId == "all_bestiary").RequiredChallenges[0]);
                    foreach (var binding in ((GameplayAchievementReporter.FactBinding[])typeof(GameplayAchievementReporter).GetField("_bindings", Private).GetValue(reporter))) Fact(binding.Fact);
                    Check(unlocks == beforeChallengeFacts && !profile.IsAchievementUnlocked("first_revive") && service.GetProgress(definitions.Single(d => d.AchievementId == "ten_revives")) == 0, "Challenge combat facts cannot unlock or advance ordinary achievements");
                    challengeProperty.SetValue(audio.Chapter, null);
                    foreach (var id in new[] { "kimi_defeat", "claude_defeat" })
                        Check(definitions.Single(d => d.AchievementId == id).RequiredChallenges.Length == 0, "Boss achievement has no challenge completion shortcut");
                    foreach (var fact in new[] { NetworkMessageCatalog.CombatPresentationKind.KimiDefeat, NetworkMessageCatalog.CombatPresentationKind.ClaudeDefeat,
                        NetworkMessageCatalog.CombatPresentationKind.ClaudeBookBreak, NetworkMessageCatalog.CombatPresentationKind.KimiMirrorBreak,
                        NetworkMessageCatalog.CombatPresentationKind.KimiInterrupt, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat }) Fact(fact);
                    foreach (string id in new[] { "kimi_defeat", "claude_defeat", "permission_break", "mirror_break", "flute_interrupt", "quick_app_defeat" })
                        Check(profile.IsAchievementUnlocked(id), "Real fact unlock " + id);
                    for (int i = 0; i < 9; i++) Fact(NetworkMessageCatalog.CombatPresentationKind.ReviveCompleted);
                    Check(profile.IsAchievementUnlocked("first_revive") && !profile.IsAchievementUnlocked("ten_revives"), "Nine revives below cumulative target");
                    Fact(NetworkMessageCatalog.CombatPresentationKind.ReviveCompleted);
                    Check(profile.IsAchievementUnlocked("ten_revives"), "Tenth revive unlocks");
                    int beforeIrrelevant = unlocks;
                    Fact(NetworkMessageCatalog.CombatPresentationKind.ClaudeBookOpen);
                    Fact(NetworkMessageCatalog.CombatPresentationKind.QuickAppImpact);
                    Check(unlocks == beforeIrrelevant, "Irrelevant/contact facts cannot unlock anything");
                    // 客机走真正的Read校验与去重链，不直接调用Present。
                    Set(audio.Session, "_transport", null);
                    typeof(DeepSleep.Runtime.Networking.CoopSessionController).GetProperty("Phase").SetValue(audio.Session, SessionPhase.Playing);
                    Set(audio.Chapter, "_phase", DeepSleep.Runtime.Progression.Run.ChapterRunPhase.Combat);
                    Set(audio.Chapter.CombatWorld.Gate, "_hasState", true);
                    Set(audio.Chapter.CombatWorld.Gate, "_combatAllowed", true);
                    int accepted = 0;
                    Action<NetworkMessageCatalog.CombatPresentationKind> observed = _ => accepted++;
                    audio.FactPresented += observed;
                    var read = typeof(CombatAudioPresenter).GetMethod("Read", Private);
                    void Replay(uint context, uint sequence, NetworkMessageCatalog.CombatPresentationKind fact, bool truncated = false)
                    {
                        using var bytes = new MemoryStream();
                        using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
                        { writer.Write(context); writer.Write(sequence); writer.Write((byte)fact); writer.Write((byte)0); writer.Write(0f); writer.Write(0f); }
                        if (truncated) bytes.SetLength(bytes.Length - 1);
                        bytes.Position = 0;
                        using var reader = new BinaryReader(bytes);
                        read.Invoke(audio, new object[] { NetworkMessageCatalog.Authority.CombatPresentation, reader });
                    }
                    Replay(7, 1, NetworkMessageCatalog.CombatPresentationKind.ContextStarted);
                    Replay(7, 2, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat);
                    Check(accepted == 1, "Replica accepted fact reaches achievement bridge");
                    Replay(7, 2, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat);
                    Replay(7, 1, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat);
                    Replay(6, 3, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat);
                    Replay(7, 4, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat, true);
                    Check(accepted == 1, "Duplicate, old, wrong-context and truncated facts ignored");
                    Replay(7, 4, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat);
                    Check(accepted == 2, "Valid later fact still accepted");
                    Set(audio.Chapter, "_phase", DeepSleep.Runtime.Progression.Run.ChapterRunPhase.Node);
                    Replay(7, 5, NetworkMessageCatalog.CombatPresentationKind.QuickAppDefeat);
                    Check(accepted == 2, "No achievements from combat facts at rest node");
                    audio.FactPresented -= observed;
                    audio.FactPresented -= callback;
                }
                finally { EditorSceneManager.ClosePreviewScene(world); }

                var all = definitions.Single(d => d.AchievementId == "all_bestiary");
                var save = typeof(LocalPlayerProfileStore).GetMethod("TrySave", Private);
                var data = typeof(LocalPlayerProfileStore).GetField("_data", Private).GetValue(profile);
                for (int i = 0; i < 100; i++)
                {
                    object[] args = { data, null };
                    Check((bool)save.Invoke(profile, args), "Dense save without sleeps " + i + ": " + args[1]);
                }
                Check(all.RequiredChallenges.Length == 9, "Nine explicit challenge identities");
                var first = all.RequiredChallenges.First(e => e.CompletionVoucherReward == 0);
                for (int i = 0; i < 3; i++)
                {
                    SettleSave();
                    Check(profile.TryAwardChallengeCompletion(first, out int awarded, out _) && awarded == 0, "Zero reward challenge records completion");
                    SettleSave(); service.ReportChallengeCleared(); Check(service.GetProgress(all) == 1, "Duplicate challenge cannot advance");
                }
                Check(!profile.IsAchievementUnlocked("all_bestiary"), "Not unlocked by farming one challenge");
                foreach (var entry in all.RequiredChallenges)
                {
                    SettleSave();
                    bool awardedOk = profile.TryAwardChallengeCompletion(entry, out int awarded, out string awardMessage);
                    Check(awardedOk && awarded == entry.CompletionVoucherReward, "Unchanged reward " + entry.EntryId + ": " + awardMessage + "; awarded=" + awarded);
                    SettleSave(); service.ReportChallengeCleared();
                }
                Check(profile.WhaleVoucherBalance == 10, "Only existing two boss challenge rewards; no achievement currency");
                Check(unlocks == 15 && service.GetProgress(all) == 9, "All 15 unlock once: count=" + unlocks + "; unlocked=" + string.Join(",", definitions.Where(d => service.IsUnlocked(d)).Select(d => d.AchievementId)));
                foreach (var d in definitions) { SettleSave(); service.Report(d.TriggerId); Check(profile.IsAchievementUnlocked(d.AchievementId), "Unlocked " + d.AchievementId); }
                Check(unlocks == 15, "No repeat unlock notifications");
                typeof(LocalPlayerProfileStore).GetMethod("Awake", Private).Invoke(profile, null);
                Check(definitions.All(d => service.IsUnlocked(d)) && all.RequiredChallenges.All(e => profile.HasCompletedChallenge(e.EntryId)), "Reload preserves distinct challenges/unlocks");

                var type = typeof(LocalPlayerProfileStore).Assembly.GetType("DeepSleep.Runtime.Progression.Meta.LocalPlayerProfileData");
                var legacy = JsonUtility.FromJson("{\"version\":5,\"whaleVoucherBalance\":27,\"unlockedAchievements\":[\"first_merit\"],\"ownedProducts\":[{\"productId\":\"crown\",\"count\":1}],\"deepSeekHeadwear\":\"crown\"}", type);
                Check((bool)typeof(LocalPlayerProfileStore).GetMethod("TryNormalize", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { legacy }), "Version5 migrates");
                string migrated = JsonUtility.ToJson(legacy);
                Check(migrated.Contains("first_merit") && migrated.Contains("crown") && migrated.Contains("27"), "Old unlocked identity, inventory and currency preserved");
            }
            finally { EditorSceneManager.ClosePreviewScene(boot); }
            var menu = EditorSceneManager.OpenPreviewScene("Assets/Scenes/MainMenu.unity");
            try
            {
                var controller = All<MainMenuController>(menu).Single(); controller.RefreshLevelCardText();
                Check(All<Text>(menu).Any(t => t.text == "天空测试盒"), "Actual sky card renamed");
                Check(!All<Text>(menu).Any(t => t.text == "天空测试场"), "No old sky card title");
                var achievementMenu = All<AchievementMenuController>(menu).Single();
                var so = new SerializedObject(achievementMenu);
                var content = (Transform)so.FindProperty("_content").objectReferenceValue;
                Check(content.parent.GetComponent<ScrollRect>() != null && content.GetComponent<ContentSizeFitter>() != null, "Achievements can scroll beyond five entries");
                var bestiary = All<BestiaryMenuView>(menu).Single();
                for (int i = 0; i < bestiary.Entries.Length; i++)
                {
                    bestiary.SelectEntry(i);
                    Check(bestiary.ChallengeLabel.text == "快速挑战", "Challenge is not a claim button " + i);
                    Check(bestiary.SkillsLabel.text.Contains(bestiary.Entry.ChallengeSummary), "Config-derived challenge summary " + i);
                    var text = bestiary.SkillsLabel;
                    float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, text.GetGenerationSettings(text.rectTransform.rect.size)) / text.pixelsPerUnit;
                    Check(height <= text.rectTransform.rect.height, "Bestiary text not clipped " + bestiary.Entry.EntryId + ": " + height);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(menu); }
            return checks + " achievement completion checks passed; 15 actual unlocks, three scene bindings, distinct challenge saves/reload, legacy migration, sky text and scroll. Isolated save directory: " + temporary;
        }
    }
}
