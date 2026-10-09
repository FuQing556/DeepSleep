using System;
using System.Reflection;
using DeepSleep.Runtime.Combat.Beams;
using DeepSleep.Runtime.Combat.Damage;
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
    public static class ClaudeSpatialCutChecks
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static void Awake(object o) => o.GetType().GetMethod("Awake", Private).Invoke(o, null);
        [MenuItem("DeepSleep/Diagnostics/Claude Spatial Cut")]
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
                ClaudeSpatialCutPattern2D cut = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var c in root.GetComponentsInChildren<ClaudeSpatialCutPattern2D>(true))
                    { Check(cut == null, "One fixed cut module"); cut = c; }
                Check(cut != null && cut.TryValidateConfiguration(out _), "Explicit references valid");
                Check(cut.Config.WarningSeconds == 1.5f && cut.Config.CutIntervalSeconds == 2f &&
                    cut.Config.FractureDelaySeconds == .1f && cut.Config.FlashFadeSeconds == .5f && cut.Config.FractureFadeSeconds == 1,
                    "Latest approved timing");
                Check(cut.Config.FractureColor.a == 1, "Fracture starts at full opacity multiplier");
                Check(Mathf.Abs(cut.Config.LineLength - 67.2f) < .001f, "Twice authored background width");
                Check(cut.GetComponentsInChildren<Collider2D>(true).Length == 0, "Visuals have no persistent damage collider");
                foreach (var view in cut.GetComponentsInChildren<ClaudeCutBatchView2D>(true))
                { Check(view.Renderer.sortingLayerName == "Gameplay" && view.transform.lossyScale == Vector3.one, "Sorting and no texture stretch"); Awake(view); }
                Awake(cut.HitEffects); Awake(cut.PoseTransition); Awake(cut);
                int impacts = 0;
                cut.HitEffects.Played += (_, _) => impacts++;
                var health = new HealthComponent[2]; var receiver = new PlayerDamageReceiver2D[2];
                for (int i = 0; i < 2; i++)
                {
                    SceneManager.MoveGameObjectToScene(cut.Targets[i].transform.root.gameObject, physics);
                    health[i] = cut.Targets[i].GetComponent<HealthComponent>(); receiver[i] = cut.Targets[i].GetComponent<PlayerDamageReceiver2D>();
                    Awake(health[i]); Awake(receiver[i]); Awake(cut.Targets[i]);
                }
                void Reset()
                {
                    cut.Cancel(); cut.Boss.BeginAuthority(1000, .5f, null, null); // Test only, not Claude balance.
                    impacts = 0;
                    for (int i = 0; i < 2; i++)
                    { cut.Gates[i].ClearBlock(typeof(ClaudeSpatialCutChecks)); health[i].ResetToMaximum(); receiver[i].ResetDamageGate();
                        cut.Targets[i].transform.position = new Vector2(-8, i == 0 ? 4 : -4); }
                    Physics2D.SyncTransforms();
                }
                BeamLaneSnapshot[][] Lanes() => (BeamLaneSnapshot[][])typeof(ClaudeSpatialCutPattern2D).GetField("_lanes", Private).GetValue(cut);
                Reset(); Check(!cut.Running && !cut.Warning.Renderer.enabled, "No automatic encounter start");
                cut.Begin(true, 42); cut.Simulate(1.499f); Check(cut.CutsFired == 0 && cut.Warning.Renderer.enabled, "Warning only");
                cut.Simulate(.001f); Check(cut.CutsFired == 1 && cut.Flashes[0].Renderer.enabled && !cut.Fractures[0].Renderer.enabled, "Instant flash before fracture");
                cut.Simulate(.101f); Check(cut.Fractures[0].Renderer.enabled, "Fracture after 0.1 seconds");
                cut.Simulate(.4f); Check(!cut.Flashes[0].Renderer.enabled && cut.Fractures[0].Renderer.enabled, "Flash fades in 0.5, fracture remains");
                cut.Simulate(1.499f); Check(cut.CutsFired == 2 && !cut.Fractures[0].Renderer.enabled, "Second cut at 3.5 after first residue fades");
                cut.Simulate(2f); Check(cut.CutsFired == 3 && !cut.Warning.Renderer.enabled, "Third cut at 5.5");
                cut.Simulate(1.11f); Check(!cut.Running && !cut.Fractures[2].Renderer.enabled, "Last fracture fades completely");
                Reset(); cut.Begin(false, 42); cut.Simulate(100); Check(cut.CutsFired == 1 && !cut.Running, "Phase one and large step");
                Reset(); cut.Begin(true, 42); cut.Simulate(100); Check(cut.CutsFired == 3 && !cut.Running, "Large step preserves three events");
                Reset(); cut.Begin(false, 42); var a = Lanes()[0][0]; cut.Cancel(); cut.Begin(false, 42);
                Check(a.Origin == Lanes()[0][0].Origin && a.Direction == Lanes()[0][0].Direction, "Deterministic seed");
                int displaced = 0;
                for (int seed = 0; seed < 128; seed++)
                {
                    Reset(); cut.Begin(false, seed);
                    foreach (var lane in Lanes()[0])
                    {
                        Check(lane.IsValid && Mathf.Abs(lane.Length - 67.2f) < .001f, "Fixed valid geometry");
                        if (lane.Center.magnitude > 1) displaced++;
                    }
                }
                Check(displaced > 128, "Cuts not all radial through center");
                Reset(); cut.Gates[0].SetBlock(typeof(ClaudeSpatialCutChecks), PlayerActionBlock.Movement); cut.Begin(false, 13);
                foreach (var lane in Lanes()[0])
                { var normal = new Vector2(-lane.Direction.y, lane.Direction.x);
                    Check(Mathf.Abs(Vector2.Dot((Vector2)cut.TargetShapes[0].bounds.center - lane.Center, normal)) >=
                        cut.TargetShapes[0].bounds.extents.magnitude + cut.Config.FrozenClearance + cut.Config.DamageWidth * .5f,
                        "Visible safe clearance around movement-sealed player"); }
                Reset(); cut.Begin(false, 13); cut.Simulate(1);
                cut.Gates[0].SetBlock(typeof(ClaudeSpatialCutChecks), PlayerActionBlock.Movement); cut.Simulate(.02f);cut.Simulate(1);
                Check(cut.CutsFired == 0, "New movement seal rebuilds warning with full escape notice");
                Reset(); cut.Begin(false, 13); var before = cut.Age; cut.Simulate(0);cut.Simulate(float.NaN);
                Check(cut.Age == before, "Paused/invalid dt no progression");
                // Replace isolated snapshot with 20 intersecting lanes through player; real physics and damage gate.
                void IntersectionFixture()
                {
                    Reset(); cut.Begin(false, 42);
                    Vector2 center = cut.TargetShapes[0].bounds.center;
                    for (int i = 0; i < cut.Config.LineCount; i++)
                    { float angle = i * Mathf.PI / cut.Config.LineCount; var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Lanes()[0][i] = new BeamLaneSnapshot(i, center - dir * 33.6f, dir, 67.2f, .16f, 1, 1); }
                }
                IntersectionFixture(); float hp = health[0].CurrentHealth;cut.Simulate(1.5f);
                Check(health[0].CurrentHealth == hp - 1, "Twenty intersections deduct only one HP");
                cut.Simulate(2.11f); Check(health[0].CurrentHealth == hp - 1, "Flash/residue never repeat damage");
                IntersectionFixture(); receiver[0].BeginInvulnerability(10); hp=health[0].CurrentHealth;cut.Simulate(1.5f);
                Check(health[0].CurrentHealth == hp, "Shared invulnerability respected");
                IntersectionFixture(); var shield = new BlockableShield(); receiver[0].RegisterDamageInterceptor(shield);
                hp = health[0].CurrentHealth; cut.Simulate(1.5f);
                Check(shield.Hits == 1 && health[0].CurrentHealth == hp, "Shield blocks once at all intersections");
                Check(impacts == cut.DamageApplications, "Intersection damage dedup also dedups Claude impact");
                receiver[0].UnregisterDamageInterceptor(shield);
                Reset(); cut.Begin(true, 42); Action cancelOnFire = cut.Cancel; cut.Fired += cancelOnFire; cut.Simulate(100);
                Check(!cut.Running && cut.CutsFired == 0, "Encounter callback cancellation stops remaining cuts"); cut.Fired -= cancelOnFire;
                Reset(); cut.Begin(true,42);cut.Boss.ResetEncounter();cut.Simulate(.02f);
                Check(!cut.Running && !cut.Warning.Renderer.enabled, "Boss lifecycle cancels pattern");
                Reset();cut.Begin(true,42);cut.Simulate(3.1f);cut.Cancel();
                foreach (var v in cut.GetComponentsInChildren<ClaudeCutBatchView2D>(true)) Check(!v.Renderer.enabled, "Cancel clears all banks");
                Check(cut.Begin(false, 42), "Explicit reuse");
                return "Claude Spatial Cut: " + checks + " checks passed (isolated Editor, not device/encounter acceptance).";
            }
            finally
            { EditorSceneManager.CloseScene(physics, true); EditorSceneManager.ClosePreviewScene(scene); SceneManager.SetActiveScene(original); }
        }
        private sealed class BlockableShield : IPlayerDamageInterceptor
        {
            public int Hits;
            public bool TryIntercept(PlayerDamageReceiver2D receiver, in DamagePacket packet)
            { if (packet.InterceptionPolicy != DamageInterceptionPolicy.Blockable) return false; Hits++; return true; }
        }
    }
}
