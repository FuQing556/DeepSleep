using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek;
using DeepSleep.Runtime.Combat.Weapons.DeepSeek.Guard;
using DeepSleep.Runtime.Combat.Weapons.Harness;
using DeepSleep.Runtime.Combat.Weapons.Harness.Melee;
using DeepSleep.Runtime.Players.Actions;
using DeepSleep.Runtime.Players.Identity;
using DeepSleep.Runtime.Players.LifeCycle;
using DeepSleep.Runtime.Players.Commands;
using DeepSleep.Runtime.Input.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudePermissionChecks
    {
        [MenuItem("DeepSleep/Diagnostics/Claude Permissions")]
        private static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            int checks = 0;
            void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            ClaudePermissionConfig warningFixture = null;
            try
            {
                ClaudePermissionModule2D module = null;
                var actors = new PlayerActor[2];
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var item in root.GetComponentsInChildren<ClaudePermissionModule2D>(true))
                    { Check(module == null, "Duplicate module"); module = item; }
                    foreach (var a in root.GetComponentsInChildren<PlayerActor>(true)) actors[(int)a.Definition.Role] = a;
                }
                Check(module != null && module.TryValidateConfiguration(out _), "Assembly");
                Check(module.HarnessMelee == actors[1].GetComponent<HarnessMeleeController>(), "Explicit HS melee binding");
                DeepSleep.Runtime.UI.Combat.ClaudePermissionStatusView status = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var item in root.GetComponentsInChildren<DeepSleep.Runtime.UI.Combat.ClaudePermissionStatusView>(true))
                    { Check(status == null, "Duplicate status UI"); status = item; }
                Check(status != null && status.Permissions == module && status.WorldCamera != null && status.Markers.Length == 6,
                    "Permission status UI assembly");
                for (int index = 0; index < 6; index++)
                {
                    var source = module.Books[0];
                    var decoration = status.Decorations[index];
                    Check(decoration != null && decoration.sprite == source.Decoration.sprite &&
                        Mathf.Abs(decoration.color.a - .7f) < .0001f && !decoration.raycastTarget &&
                        decoration.transform.parent == status.Markers[index].transform.parent &&
                        decoration.transform.GetSiblingIndex() < status.Markers[index].transform.GetSiblingIndex(),
                        "Concentric permission decoration matches book, 70 percent alpha and behind circle");
                    Check(status.Markers[index].color == source.IdentityCircle.color &&
                        status.Markers[index].rectTransform.localScale == source.IdentityCircle.rectTransform.localScale,
                        "Role circle matches book opacity and scale");
                    Check(status.Labels[index].text == module.Config.PermissionLabels[index % 3] &&
                        status.Labels[index].fontStyle == source.Label.fontStyle && status.Labels[index].color == source.DeepSeekInk &&
                        status.Labels[index].GetComponent<UnityEngine.UI.Outline>().effectColor == source.Label.GetComponent<UnityEngine.UI.Outline>().effectColor,
                        "Permission circle includes matching book text style");
                }
                var top = DeepSleep.Runtime.UI.Combat.ClaudePermissionStatusView.TriangleOffset(0, status.TriangleRadius);
                var right = DeepSleep.Runtime.UI.Combat.ClaudePermissionStatusView.TriangleOffset(1, status.TriangleRadius);
                var left = DeepSleep.Runtime.UI.Combat.ClaudePermissionStatusView.TriangleOffset(2, status.TriangleRadius);
                Check((top + right + left).sqrMagnitude < .00001f && top.x == 0 && top.y > 0 && right.x > 0 && left.x < 0 &&
                    right.y < 0 && left.y == right.y, "Triangle centered on player, fixed permission quadrants");
                Check(Mathf.Abs(Vector2.Distance(top, right) - Vector2.Distance(right, left)) < .00001f &&
                    Mathf.Abs(Vector2.Distance(top, left) - Vector2.Distance(right, left)) < .00001f, "Equilateral marker geometry");
                Check(module.Config.PhaseOneHealth == 50 && module.Config.PhaseTwoHealth == 50 &&
                    module.Config.WarningSeconds == 0 && module.Config.SealSeconds == 10, "Approved parameters: immediate seal");
                foreach (var book in module.Books)
                {
                    Check(book.Decoration != null && book.Decoration.sprite != null, "Book decoration must be explicitly bound");
                    book.Clear();
                    Check(!book.Decoration.enabled, "Hidden book hides decoration");
                    typeof(DamageHitbox2D).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(book.GetComponent<DamageHitbox2D>(), null);
                }
                Check(!module.HasOpenBooks, "No automatic spawn");
                var melee = module.HarnessMelee;
                var laser = actors[1].GetComponent<HarnessTerminalLaserController>();
                var meleeState = typeof(HarnessMeleeController).GetField("<IsMelee>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                meleeState.SetValue(melee, true); laser.SetInputSuppressed(true);
                var hsSkillBook = module.Books[0];
                Check(hsSkillBook.Open(module.Corners[0].position, PlayerRole.Harness, ClaudePermission.PrimaryAttack,
                    module.Config, false, module.Gates[1], module.Lives[1], null, module.Registry), "HS primary seal fixture");
                module.Advance(.01f);
                Check(melee.IsMelee, "Sealing primary alone does not cancel available melee skill");
                module.Clear();
                Check(hsSkillBook.Open(module.Corners[0].position, PlayerRole.Harness, ClaudePermission.Skill,
                    module.Config, false, module.Gates[1], module.Lives[1], null, module.Registry), "HS melee seal fixture");
                module.Advance(.01f);
                Check(!melee.IsMelee && !(bool)typeof(HarnessTerminalLaserController).GetField("_inputSuppressed",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(laser), "Skill seal ends melee and releases unsealed laser");
                Check(hsSkillBook.IsOpen && module.Gates[1].IsBlocked(PlayerActionBlock.Skill) &&
                    !module.Gates[1].IsBlocked(PlayerActionBlock.PrimaryAttack), "Skill seal remains; no free permission release");
                module.Clear();
                var instantBook = module.Books[0];
                Check(instantBook.Open(module.Corners[0].position, PlayerRole.DeepSeek, ClaudePermission.Skill,
                    module.Config, false, module.Gates[0], module.Lives[0], null, module.Registry), "Immediate book opens");
                Check(instantBook.State == ClaudeBookState.Sealed && instantBook.RemainingSeconds == 10 &&
                    module.Gates[0].IsBlocked(PlayerActionBlock.Skill) && instantBook.CaptureSnapshot().IsValid(module.Config),
                    "Immediate permission and valid sealed snapshot without an Advance tick");
                Canvas.ForceUpdateCanvases(); status.Refresh(0);
                Check(status.Markers[2].gameObject.activeSelf && status.Decorations[2].gameObject.activeSelf,
                    "Immediate seal displays role circle and decoration");
                instantBook.Advance(0); Check(instantBook.RemainingSeconds == 10, "Pause freezes immediate seal");
                instantBook.Advance(9.99f); Check(instantBook.IsOpen, "Immediate duration lasts ten seconds");
                instantBook.Advance(.02f);
                Check(!instantBook.IsOpen && !module.Gates[0].IsBlocked(PlayerActionBlock.Skill), "Immediate expiry releases permission");
                Check(!new ClaudeBookSnapshot(Vector2.zero, PlayerRole.DeepSeek, ClaudePermission.Skill,
                    ClaudeBookState.Warning, 100, 1, false).IsValid(module.Config), "Zero-warning config rejects warning replica");
                for (int seed = 0; seed < 16; seed++)
                {
                    Check(module.BeginBatch((seed & 1) != 0, seed), "Immediate batch begins");
                    foreach (var book in module.Books)
                        if (book.IsOpen) Check(book.State == ClaudeBookState.Sealed && book.RemainingSeconds == 10 &&
                            module.Gates[(int)book.Role].IsBlocked(ClaudePermissionBook2D.BlockFor(book.Permission)),
                            "Every batch book seals synchronously");
                    module.Clear();
                }
                // 保留可配置预警的边界回归，仅使用临时配置，不改正式零预警资产。
                warningFixture = UnityEngine.Object.Instantiate(module.Config);
                warningFixture.WarningSeconds = 1.5f; module.Config = warningFixture;
                object external = new object();
                module.Gates[0].SetBlock(external, PlayerActionBlock.Movement);
                var b = module.Books[0];
                bool Open(ClaudePermission permission, bool phaseTwo = false) => b.Open(module.Corners[0].position,
                    PlayerRole.DeepSeek, permission, module.Config, phaseTwo, module.Gates[0], module.Lives[0], null, module.Registry);
                Check(Open(ClaudePermission.Skill), "Open explicit book");
                Canvas.ForceUpdateCanvases(); status.Refresh(0);
                Check(!status.Markers[2].gameObject.activeSelf, "Warning must not show active seal");
                typeof(ClaudePermissionBook2D).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(b, null);
                Check(b.Decoration.enabled && b.Decoration.transform.position == b.Visual.transform.position &&
                    b.Decoration.sortingLayerID == b.Visual.sortingLayerID && b.Decoration.sortingOrder == b.Visual.sortingOrder - 1,
                    "Open book shows centered decoration directly below book");
                Check(module.Registry.TryResolve(b.Shape, out var registered) && registered == b.Perception, "Book is visible to shared AI perception");
                Check(!module.Gates[0].IsBlocked(PlayerActionBlock.Skill), "Warning is not a seal");
                b.Advance(0); Check(b.RemainingSeconds == 1.5f, "Pause freezes real battle timer");
                b.Advance(1.49f); Check(b.State == ClaudeBookState.Warning, "Warning boundary");
                b.Advance(.02f); Check(b.State == ClaudeBookState.Sealed && module.Gates[0].IsBlocked(PlayerActionBlock.Skill), "Seals after warning");
                status.Refresh(0);
                Check(status.Markers[2].gameObject.activeSelf && !status.Markers[0].gameObject.activeSelf &&
                    !status.Markers[1].gameObject.activeSelf, "Show skill seal, not unrelated external movement gate");
                Check(status.Decorations[2].gameObject.activeSelf && !status.Decorations[0].gameObject.activeSelf &&
                    status.Decorations[2].rectTransform.anchoredPosition == status.Markers[2].rectTransform.anchoredPosition &&
                    status.Decorations[2].rectTransform.sizeDelta == status.Markers[2].rectTransform.sizeDelta * 1.15f,
                    "Decoration follows circle center, visibility and 115 percent diameter");
                status.Refresh(.5f);
                Check(status.Rows[0].localScale == Vector3.one && status.Markers[2].gameObject.activeSelf,
                    "Persistent circle at fixed scale");
                var secondBook = module.Books[1];
                Check(secondBook.Open(module.Corners[1].position, PlayerRole.DeepSeek, ClaudePermission.Movement,
                    module.Config, false, module.Gates[0], module.Lives[0], null, module.Registry), "Second seal fixture");
                secondBook.Advance(module.Config.WarningSeconds); status.Refresh(0);
                Check(status.Markers[0].gameObject.activeSelf && status.Markers[2].gameObject.activeSelf,
                    "Movement and skill circles visible together");
                secondBook.Clear(); status.Refresh(0);
                Check(!status.Markers[0].gameObject.activeSelf && status.Markers[2].gameObject.activeSelf,
                    "Removing one seal preserves another");
                Check(!module.Gates[0].IsBlocked(PlayerActionBlock.PrimaryAttack), "Skill does not ban primary");
                var packet = new DamagePacket(100, b.transform.position, Vector2.right, null);
                Check(b.GetComponent<DamageHitbox2D>().TryReceiveDamage(packet), "Real hitbox damage route");
                Check(!b.IsOpen && !module.Gates[0].IsBlocked(PlayerActionBlock.Skill) &&
                    module.Gates[0].IsBlocked(PlayerActionBlock.Movement), "Break only clears this owner");
                Check(!b.Decoration.enabled, "Broken book hides decoration immediately");
                status.Refresh(0); Check(!status.Markers[2].gameObject.activeSelf, "Broken book removes role seal hint");
                Check(!b.TryReceiveDamage(packet), "No damage after return");
                Check(!module.Registry.TryResolve(b.Shape, out _), "Returned book unregisters from perception");
                Check(Open(ClaudePermission.Movement), "Break during warning fixture");
                Check(b.TryReceiveDamage(packet) && !b.IsOpen, "Warning book can be interrupted");
                Check(Open(ClaudePermission.PrimaryAttack, true) && b.CurrentHealth == 50, "Phase two health");
                b.Advance(1.5f);
                Check(module.Gates[0].IsBlocked(PlayerActionBlock.PrimaryAttack) &&
                    !module.Gates[0].IsBlocked(PlayerActionBlock.Skill), "Primary does not ban skill");
                b.Advance(9.99f); Check(b.IsOpen, "Duration starts after warning");
                b.Advance(.02f); Check(!b.IsOpen && !module.Gates[0].IsBlocked(PlayerActionBlock.PrimaryAttack), "10 second expiry");
                Check(Open(ClaudePermission.Movement), "Reopen after expiry");
                b.Advance(100); Check(!b.IsOpen, "Large timestep crosses both phases");
                Check(Open(ClaudePermission.Skill), "Downed fixture"); b.Advance(1.5f);
                // Invoke the subscribed life event with a downed fixture; never alter real health or saves.
                var lifeEvent = (Action<PlayerLifeStateController2D, PlayerLifeState>)typeof(PlayerLifeStateController2D)
                    .GetField("StateChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module.Lives[0]);
                Check(lifeEvent != null, "Subscribed to actual life event");
                lifeEvent.Invoke(module.Lives[0], PlayerLifeState.Downed);
                Check(!b.IsOpen && !module.Gates[0].IsBlocked(PlayerActionBlock.Skill), "Downed returns permission");
                module.Gates[0].ClearBlock(external);
                // 使用实际分发器和纯C#消费者记录路由，不补组件或影响正式玩家。
                var dispatcher = actors[0].CommandDispatcher;
                var move = new Recorder(PlayerActionBlock.Movement);
                var primary = new Recorder(PlayerActionBlock.AutomaticCombat | PlayerActionBlock.PrimaryAttack);
                var skill = new Recorder(PlayerActionBlock.ActiveCombat | PlayerActionBlock.Skill);
                void SetDispatcher(string field, object value) => typeof(PlayerCommandDispatcher)
                    .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dispatcher, value);
                SetDispatcher("_isInitialized", true);
                SetDispatcher("_commandPreprocessors", Array.Empty<IPlayerCommandPreprocessor>());
                SetDispatcher("_commandConsumers", new IPlayerCommandConsumer[] {move, primary, skill});
                dispatcher.TryBindCommandSource(new FixtureSource());
                Check(Open(ClaudePermission.Skill), "Dispatcher skill fixture"); b.Advance(1.5f);
                dispatcher.Simulate(1, .02f);
                Check(move.Accepted == 1 && primary.Accepted == 1 && skill.Accepted == 0 && skill.Blocked == 0, "Skill ban routes movement and primary without cancelling committed skill");
                b.Clear(); Check(Open(ClaudePermission.PrimaryAttack), "Dispatcher primary fixture"); b.Advance(1.5f);
                dispatcher.Simulate(2, .02f);
                Check(move.Accepted == 2 && primary.Accepted == 1 && primary.Blocked == 1 && skill.Accepted == 1, "Primary ban routes skill");
                b.Clear(); dispatcher.Simulate(3, .02f);
                Check(primary.Accepted == 2 && skill.Accepted == 2, "Unseal restores command route");
                module.Gates[0].SetBlock(external, PlayerActionBlock.Movement | PlayerActionBlock.AutomaticCombat | PlayerActionBlock.ActiveCombat);
                dispatcher.Simulate(4, .02f);
                Check(move.Accepted == 3 && primary.Accepted == 2 && skill.Accepted == 2, "Legacy rescue blocks preserved");
                module.Gates[0].ClearBlock(external);
                for (int seed = 0; seed < 128; seed++)
                {
                    bool phaseTwo = (seed & 1) != 0;
                    Check(module.BeginBatch(phaseTwo, seed), "Batch starts");
                    int mask = 0, count = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        var book = module.Books[i];
                        if (!book.IsOpen) continue;
                        bool rightCorner = book.transform.position == module.Corners[1].position ||
                            book.transform.position == module.Corners[2].position;
                        Check(book.Visual.flipX == rightCorner, "Only right corner book art is mirrored");
                        int bit = (int)book.Role * 3 + (int)book.Permission;
                        Check((mask & (1 << bit)) == 0, "No duplicate permission"); mask |= 1 << bit; count++;
                        for (int j = 0; j < i; j++) Check(!module.Books[j].IsOpen || module.Books[j].transform.position != book.transform.position, "Distinct corners");
                    }
                    Check(count == (phaseTwo ? 4 : 2) && ClaudePermissionModule2D.IsLegalMask(mask, count, true, true), "Legal combination");
                    Check(!module.BeginBatch(phaseTwo, seed), "No batch overlap");
                    module.Advance(1.5f); module.Clear();
                    Check(module.Gates[0].CombinedBlocks == PlayerActionBlock.None && module.Gates[1].CombinedBlocks == PlayerActionBlock.None, "Batch reset removes all owned bans");
                }
                for (int mask = 1; mask < 64; mask++)
                {
                    if (!ClaudePermissionModule2D.IsLegalMask(mask, 2, true, false)) continue;
                    Check((mask & 2) == 0 && (mask >> 3) == 0, "Survivor retains primary attack");
                }
                Check((actors[0].GetComponent<DeepSeekRiceAutoShooter>().ActionCategory & PlayerActionBlock.PrimaryAttack) != 0, "DS fire category");
                Check((actors[0].GetComponent<DeepSeekRiceGuardController>().ActionCategory & PlayerActionBlock.Skill) != 0, "DS skill category");
                Check((actors[1].GetComponent<HarnessTerminalLaserController>().ActionCategory & PlayerActionBlock.PrimaryAttack) != 0, "HS primary category");
                Check((actors[1].GetComponent<HarnessMeleeController>().ActionCategory & PlayerActionBlock.Skill) != 0, "HS skill category");
                Check(b.LabelCanvas.GetComponent<DeepSleep.Runtime.UI.Common.UiThemeView>() == null, "Identity cannot be overwritten by local menu theme");
                foreach (var role in new[] {PlayerRole.DeepSeek, PlayerRole.Harness})
                {
                    for (int permission = 0; permission < 3; permission++)
                    {
                        b.Clear();
                        Check(b.Open(module.Corners[0].position, role, (ClaudePermission)permission, module.Config, false,
                            module.Gates[(int)role], module.Lives[(int)role], null, module.Registry), "Page identity fixture");
                        var palette = role == PlayerRole.DeepSeek ? b.DeepSeekPalette : b.HarnessPalette;
                        Check(b.IdentityCircle.sprite == palette.CircleBase && b.Progress.sprite == palette.CircleFrame,
                            "Actual target role circle, not global theme");
                        Check(b.Label.text == module.Config.PermissionLabels[permission], "Only permission name on page");
                        Check(b.Label.color == (role == PlayerRole.DeepSeek ? b.DeepSeekInk : b.HarnessInk), "Role ink color");
                    }
                }
                b.Clear();
                // 完整帧副本检查；只操作隔离场景，不创建连接或写玩家存档。
                var frames = new ClaudeBookSnapshot[4];
                module.Session = null;
                Check(Open(ClaudePermission.Skill), "Authority snapshot fixture");
                b.Visual.flipX = true;
                Check(module.CaptureSnapshot(frames) && frames[0].State == ClaudeBookState.Warning &&
                    frames[0].Mirrored && frames[0].Health == 50, "Snapshot captures authoritative book");
                Check(!module.ApplyReplica(frames) && b.CanReceiveDamage, "Replica cannot replace authority book");
                module.Clear();
                module.Gates[0].SetBlock(external, PlayerActionBlock.Movement);
                foreach (var role in new[] { PlayerRole.DeepSeek, PlayerRole.Harness })
                {
                    for (int permission = 0; permission < 3; permission++)
                    {
                        Array.Clear(frames, 0, frames.Length);
                        var gate = module.Gates[(int)role];
                        var block = ClaudePermissionBook2D.BlockFor((ClaudePermission)permission);
                        frames[0] = new ClaudeBookSnapshot(module.Corners[1].position, role, (ClaudePermission)permission,
                            ClaudeBookState.Warning, 50, 1, true);
                        Check(module.ApplyReplica(frames), "Warning replica");
                        status.Refresh(0);
                        Check(!status.Markers[(int)role * 3 + permission].gameObject.activeSelf,
                            "Replica warning does not falsely show sealed hint");
                        Check(b.IsOpen && !b.CanReceiveDamage && !b.Shape.enabled && b.Visual.enabled &&
                            b.LabelCanvas.enabled && b.Visual.flipX && b.Progress.fillAmount == 1 / 1.5f,
                            "Replica UI visible, mirrored, collider non-authoritative");
                        Check(b.Label.text == module.Config.PermissionLabels[permission] &&
                            b.IdentityCircle.sprite == (role == PlayerRole.DeepSeek ? b.DeepSeekPalette : b.HarnessPalette).CircleBase,
                            "Replica permission and identity match host, not menu theme");
                        b.Advance(100); Check(b.State == ClaudeBookState.Warning && b.RemainingSeconds == 1,
                            "Replica does not locally expire or seal");
                        Check(!b.TryReceiveDamage(packet) && b.CurrentHealth == 50, "Replica cannot locally damage book");
                        frames[0] = new ClaudeBookSnapshot(module.Corners[1].position, role, (ClaudePermission)permission,
                            ClaudeBookState.Sealed, 30, 5, true);
                        bool hsSkill = role == PlayerRole.Harness && (ClaudePermission)permission == ClaudePermission.Skill;
                        if (hsSkill) { meleeState.SetValue(melee, true); laser.SetInputSuppressed(true); }
                        Check(module.ApplyReplica(frames) && gate.IsBlocked(block) && b.Progress.fillAmount == .5f,
                            "Late sealed snapshot immediately applies own gate");
                        if (hsSkill) Check(!melee.IsMelee && !(bool)typeof(HarnessTerminalLaserController).GetField("_inputSuppressed",
                            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(laser), "Replica skill seal also restores primary route");
                        status.Refresh(0);
                        Check(status.Markers[(int)role * 3 + permission].gameObject.activeSelf,
                            "Replica seal displays correct player permission hint");
                        Check(module.ApplyReplica(frames) && b.RemainingSeconds == 5, "Repeated snapshot does not restart duration");
                        // 最后一本无效：前三本也必须保持上一有效帧。
                        var valid = frames[0]; frames[0] = default;
                        frames[3] = new ClaudeBookSnapshot(Vector2.zero, role, (ClaudePermission)permission,
                            ClaudeBookState.Sealed, float.NaN, 5, false);
                        Check(!module.ApplyReplica(frames) && b.State == ClaudeBookState.Sealed && gate.IsBlocked(block),
                            "Invalid trailing book cannot partially release earlier gates");
                        frames[0] = valid; frames[3] = valid;
                        Check(!module.ApplyReplica(frames), "Duplicate permission rejected atomically");
                        frames[3] = default;
                        frames[0] = new ClaudeBookSnapshot(module.Corners[0].position, role, (ClaudePermission)permission,
                            ClaudeBookState.Warning, 50, 1.5f, false);
                        Check(module.ApplyReplica(frames) && !b.Visual.flipX, "Reused left book resets mirror");
                        Check(gate.CombinedBlocks == (role == PlayerRole.DeepSeek ? PlayerActionBlock.Movement : PlayerActionBlock.None),
                            "Warning clears only book owner, not external block");
                        frames[0] = valid; Check(module.ApplyReplica(frames), "Reopen sealed replica");
                        Array.Clear(frames, 0, frames.Length);
                        Check(module.ApplyReplica(frames) && !b.IsOpen && !b.Visual.enabled && !b.LabelCanvas.enabled,
                            "Authority return hides replica and releases own gate");
                        status.Refresh(0);
                        Check(!status.Markers[(int)role * 3 + permission].gameObject.activeSelf,
                            "Replica authority return clears permission hint");
                        Check(module.Gates[0].CombinedBlocks == PlayerActionBlock.Movement &&
                            module.Gates[1].CombinedBlocks == PlayerActionBlock.None, "External gate survives replica return");
                    }
                }
                module.Gates[0].ClearBlock(external);
                frames[0] = new ClaudeBookSnapshot(Vector2.zero, PlayerRole.DeepSeek, ClaudePermission.Skill,
                    ClaudeBookState.Sealed, 50, 5, false);
                Check(module.ApplyReplica(frames), "Replica disable fixture");
                module.enabled = false;
                typeof(ClaudePermissionModule2D).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(module, null); // 非ExecuteAlways组件在Editor隔离场景不自动派发生命周期。
                Check(!module.HasOpenBooks && !module.Gates[0].IsBlocked(PlayerActionBlock.Skill), "Disable releases replica ownership");
                return $"Claude permissions: {checks} checks passed (isolated Editor, not full fight/network/device verification).";
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (warningFixture != null) UnityEngine.Object.DestroyImmediate(warningFixture);
            }
        }

        private sealed class Recorder : IPlayerActionCommandConsumer, IPlayerBlockedCommandConsumer
        {
            public Recorder(PlayerActionBlock category) => ActionCategory = category;
            public PlayerActionBlock ActionCategory { get; }
            public int Accepted, Blocked;
            public void ConsumeCommand(in PlayerCommand command, float deltaTime) => Accepted++;
            public void ConsumeBlockedCommand(float deltaTime) => Blocked++;
        }
        private sealed class FixtureSource : ICommandSource
        {
            public bool TryGetCommand(uint simulationTick, out PlayerCommand command)
            { command = default; return true; }
        }
    }
}
