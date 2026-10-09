using System;
using System.Linq;
using System.Reflection;
using DeepSleep.Runtime.Combat.Encounters.Claude;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Players.Health;
using DeepSleep.Runtime.Players.Actions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    public static class ClaudeTrackingCutChecks
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Awake(object o) => o.GetType().GetMethod("Awake", Private).Invoke(o, null);
        [MenuItem("DeepSleep/Diagnostics/Claude Tracking Cut")]
        private static void Menu() => Debug.Log(Run());
        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run outside Play.");
            var original = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/World02_2066.unity");
            var physics = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(original);
            int checks = 0;
            void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
            try
            {
                ClaudeTrackingCutPattern2D cut = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<ClaudeTrackingCutPattern2D>(true))
                    { Check(cut == null, "One module"); cut = c; }
                Check(cut != null && cut.TryValidateConfiguration(out _), "Explicit configuration");
                Check(cut.Config.TrackingSeconds == .8f && cut.Config.LockedSeconds == .4f &&
                    cut.Config.FadeSeconds == .5f && cut.Config.Damage == 1, "Approved timing and damage");
                Check(cut.Marker.sortingLayerName == "UIWorld" &&
                    cut.Marker.GetComponentInParent<UnityEngine.Rendering.SortingGroup>() == null, "Independent marker sorting");
                Check(cut.GetComponentsInChildren<Collider2D>(true).Length == 0, "Visuals do not leave colliders");
                Awake(cut.HitEffects); Awake(cut.PoseTransition); Awake(cut);
                int impacts = 0;
                cut.HitEffects.Played += (_, _) => impacts++;
                var health = new HealthComponent[2]; var receiver = new PlayerDamageReceiver2D[2];
                for (int i = 0; i < 2; i++)
                {
                    SceneManager.MoveGameObjectToScene(cut.Targets[i].transform.root.gameObject, physics);
                    health[i] = cut.Targets[i].GetComponent<HealthComponent>();
                    receiver[i] = cut.Targets[i].GetComponent<PlayerDamageReceiver2D>();
                    Awake(health[i]); Awake(receiver[i]); Awake(cut.Targets[i]);
                }
                void Reset()
                {
                    cut.Cancel();
                    impacts = 0;
                    cut.Boss.BeginAuthority(1000, .5f, null, null); // Isolated fixture, not gameplay balance.
                    for (int i = 0; i < 2; i++)
                    {
                        cut.Gates[i].ClearBlock(typeof(ClaudeTrackingCutChecks));
                        health[i].ResetToMaximum(); receiver[i].ResetDamageGate();
                        cut.Targets[i].GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        cut.Targets[i].transform.position = new Vector2(-8, i == 0 ? 4 : -4);
                    }
                    Physics2D.SyncTransforms();
                }
                int target;
                Check(Mathf.Abs(cut.Config.Length - 4.2f) < .001f &&
                    Mathf.Abs(cut.Config.DamageWidth - .525f) < .001f, "Local slash enlarged 1.5 times, matching damage width");
                foreach (var velocity in new[] { new Vector2(1, 1), new Vector2(-1, 1),
                    new Vector2(-1, -1), new Vector2(1, -1) })
                {
                    Reset(); cut.Begin(false, 42);
                    target = cut.TargetIndex;
                    var body = cut.Targets[target].GetComponent<Rigidbody2D>();
                    body.linearVelocity = velocity; cut.Simulate(.8f);
                    bool positive = velocity.x * velocity.y > 0;
                    Check(cut.State == ClaudeTrackingCutState.Locked &&
                        (cut.Lane.Direction.y > 0) == positive, "Quadrant maps to approved diagonal");
                    Check(Mathf.Abs(Mathf.Abs(cut.Lane.Direction.x) - Mathf.Abs(cut.Lane.Direction.y)) < .001f, "Only 45 degree diagonals");
                    var lockedDirection = cut.Lane.Direction;
                    body.linearVelocity = new Vector2(-velocity.x, velocity.y); cut.Simulate(.2f);
                    Check(cut.Lane.Direction == lockedDirection, "Movement after lock does not rotate slash");
                }
                Reset(); cut.Begin(false, 42); target = cut.TargetIndex;
                var movingBody = cut.Targets[target].GetComponent<Rigidbody2D>();
                movingBody.linearVelocity = Vector2.one; cut.Simulate(.4f);
                movingBody.linearVelocity = new Vector2(-1, 1); cut.Simulate(.4f);
                Check(cut.Lane.Direction.y < 0, "Final tracking sample wins, not initial movement");
                Reset(); cut.Begin(false, 42); target = cut.TargetIndex;
                movingBody = cut.Targets[target].GetComponent<Rigidbody2D>();
                movingBody.linearVelocity = new Vector2(-1, 1); cut.Simulate(.4f);
                movingBody.linearVelocity = Vector2.right; cut.Simulate(.2f);
                movingBody.linearVelocity = Vector2.zero; cut.Simulate(.2f);
                Check(cut.Lane.Direction.y < 0, "Axis and stationary retain recent quadrant");
                Reset(); cut.Begin(true, 42);
                Check(cut.SecondaryTargetIndex == 1 - cut.TargetIndex && cut.SecondaryMarker.enabled, "Phase two tracks both players simultaneously");
                for (int shot = 0; shot < 3; shot++)
                {
                    for (int i = 0; i < 2; i++)
                        cut.Targets[i].GetComponent<Rigidbody2D>().linearVelocity = new Vector2(shot == 1 ? -1 : 1, 1);
                    cut.Simulate(.8f);
                    Check((cut.Lane.Direction.y > 0) == (shot != 1), "Phase two independently samples each slash");
                    cut.Simulate(.4f);
                    Check(cut.Blades[shot].enabled && cut.Blades[3 + shot].enabled, "Both local slashes fire in the same round");
                    Check(cut.CrossBlades[shot].enabled && cut.CrossBlades[3 + shot].enabled,
                        "Phase two fires both crossing blades for each target");
                    var crossed=ClaudeTrackingCutPattern2D.CrossLane(cut.Lane);
                    Check(Mathf.Abs(Vector2.Dot(crossed.Direction,cut.Lane.Direction))<.001f &&
                        Vector2.Distance(crossed.Center,cut.Lane.Center)<.001f,"Cross cut shares center and is perpendicular");
                }
                Check(cut.ShotsFired == 3, "Three local phase two slashes");
                Reset(); cut.Targets[1].transform.position = cut.Targets[0].transform.position;
                Physics2D.SyncTransforms(); cut.Begin(true, 42); cut.Simulate(1.2f);
                Check(cut.DamageApplications == 2, "Overlapping pair damages each player only once, not twice");
                Reset(); Check(!cut.Marker.enabled && cut.ShotsFired == 0, "No auto start");
                Check(cut.Begin(false, 42), "Begin");
                target = cut.TargetIndex;
                cut.Targets[target].transform.position += Vector3.up;
                Physics2D.SyncTransforms(); cut.Simulate(.4f);
                Check(Vector2.Distance(cut.MarkerPosition, cut.TargetShapes[target].bounds.center) < .001f, "Tracking follows");
                cut.Simulate(.4f); Check(cut.State == ClaudeTrackingCutState.Locked, "Lock at .8");
                var lane = cut.Lane; var marker = cut.MarkerPosition;
                cut.Targets[target].transform.position += Vector3.up * 3;
                Physics2D.SyncTransforms(); cut.Simulate(.2f);
                Check(cut.MarkerPosition == marker && cut.Lane.Origin == lane.Origin &&
                    cut.Lane.Direction == lane.Direction, "Local slash and marker freeze");
                cut.Simulate(.2f); Check(cut.ShotsFired == 1 && cut.Blades[0].enabled, "Instant cut");
                Check(cut.CrossBlades.All(b=>!b.enabled),"Phase one remains single slash");
                var sprite = cut.Blades[0].sprite;
                Vector2 Native(Vector2 normalized) => (Vector2.Scale(normalized, sprite.rect.size) - sprite.pivot) / sprite.pixelsPerUnit;
                Check(Vector2.Distance(cut.Blades[0].transform.TransformPoint(Native(cut.Config.BladeAxisStart)), lane.Origin) < .001f, "Art start aligned");
                Check(Vector2.Distance(cut.Blades[0].transform.TransformPoint(Native(cut.Config.BladeAxisEnd)), lane.Origin + lane.Direction * lane.Length) < .001f, "Art end aligned");
                Check(cut.DamageApplications == 0, "Dodge during locked window");
                Check(impacts == 0, "Dodge does not spawn Claude hit effect");
                cut.Simulate(3f); Check(cut.State == ClaudeTrackingCutState.Complete && !cut.Blades[0].enabled && cut.ShotsFired == 3, "Phase one completes three slashes and fades");
                Reset(); cut.Begin(false, 42); cut.Simulate(.8f);
                target = cut.TargetIndex;
                var hp = health[target].CurrentHealth;
                cut.Simulate(.4f);
                Check(cut.DamageApplications == 1 && health[target].CurrentHealth == hp - 1, "Real instant damage once");
                Check(impacts == 1, "Accepted slash spawns one Claude attack effect");
                cut.Simulate(.25f); Check(health[target].CurrentHealth == hp - 1, "Fade does not damage");
                Check(impacts == 1, "Fade does not repeat attack effect");
                Reset(); cut.Gates[0].SetBlock(typeof(ClaudeTrackingCutChecks), PlayerActionBlock.Movement);
                Check(cut.Begin(false, 42) && cut.TargetIndex == 1, "Frozen player excluded");
                cut.Gates[1].SetBlock(typeof(ClaudeTrackingCutChecks), PlayerActionBlock.Movement);
                cut.Simulate(.1f); Check(cut.State == ClaudeTrackingCutState.Complete, "Both frozen stop safely");
                Reset(); cut.Gates[0].SetBlock(typeof(ClaudeTrackingCutChecks), PlayerActionBlock.Movement);
                cut.Targets[0].transform.position = cut.Targets[1].transform.position;
                Physics2D.SyncTransforms(); Check(!cut.Begin(false, 42), "No lane through frozen bystander");
                Reset(); cut.Begin(true, 42); cut.Simulate(0); cut.Simulate(float.NaN);
                Check(cut.ShotsFired == 0, "Pause and invalid delta");
                cut.Simulate(5); Check(cut.ShotsFired == 3 && cut.State == ClaudeTrackingCutState.Complete, "Phase two three complete warnings");
                Reset(); cut.Begin(true, 42); for (int i = 0; i < 250; i++) cut.Simulate(.02f);
                Check(cut.ShotsFired == 3 && cut.State == ClaudeTrackingCutState.Complete, "Fixed step parity");
                Reset(); cut.Begin(false, 42);
                cut.Gates[cut.TargetIndex].SetBlock(typeof(ClaudeTrackingCutChecks), PlayerActionBlock.Movement);
                cut.Simulate(.1f); Check(cut.State == ClaudeTrackingCutState.Tracking && cut.TargetIndex >= 0 &&
                    !cut.Gates[cut.TargetIndex].IsBlocked(PlayerActionBlock.Movement), "New seal gives new full warning");
                Reset(); Action cancel = cut.Cancel; cut.Fired += cancel;
                cut.Begin(true, 42); cut.Simulate(5); cut.Fired -= cancel;
                Check(cut.State == ClaudeTrackingCutState.Idle && !cut.Marker.enabled, "Callback cancellation respected");
                return $"Claude tracking cut: {checks} checks passed (isolated Editor, not device/network).";
            }
            finally
            {
                EditorSceneManager.CloseScene(physics, true);
                EditorSceneManager.ClosePreviewScene(scene);
                SceneManager.SetActiveScene(original);
            }
        }
    }
}
