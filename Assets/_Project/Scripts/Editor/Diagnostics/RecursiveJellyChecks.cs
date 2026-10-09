using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Enemies;
using DeepSleep.Runtime.Networking;
using DeepSleep.Runtime.Progression.Levels;
using DeepSleep.Runtime.Progression.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>只由主动调试执行：编辑态装配检查，及独立 Play 中真实池/生命/镜像序列化验证。</summary>
    public static class RecursiveJellyChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, Private).Invoke(target, args);

        public static string RunAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run asset checks outside Play.");
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            var scene = EditorSceneManager.OpenPreviewScene(RecursiveJellyInstaller.ScenePath);
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var bindings = all.OfType<LevelSceneBindings>().Single();
                Check(bindings.TryValidateConfiguration(out string reason), reason);
                Check(LevelSceneInstaller.TryValidateDerived(bindings, out reason), reason);
                var first = bindings.Level.ChapterRunConfig.GetSegment(1);
                Check(first.DurationSeconds == 45 && first.RequiredDefeats == 13 && first.ObjectiveMode == ChapterObjectiveMode.AllEnemiesAndEncounters, "Wave1 objective");
                Check(first.SpawnRules.Count(r => r.Enabled) == 3, "Wave1 schedules recursion, 360 and sparse QuickApp");
                Check(!all.OfType<DoubaoChapterEncounterDriver2D>().Single().IsRequiredForSegment(1), "No Doubao in first wave");
                var old = AssetDatabase.LoadAssetAtPath<ChapterRunConfig>("Assets/_Project/Configs/Progression/CFG_ChapterRun_World01_EarlyInternet.asset");
                for (int i = 1; i <= 4; i++)
                {
                    var a = bindings.Level.ChapterRunConfig.GetSegment(i); var b = old.GetSegment(i);
                    Check(a.DurationSeconds == b.DurationSeconds && a.EnemyHealthMultiplier == b.EnemyHealthMultiplier, "World01 duration and health progression");
                    Check(a.ObjectiveMode == ChapterObjectiveMode.AllEnemiesAndEncounters && a.SpawnRules.Count(r => r.Enabled) == (i <= 3 ? 3 : 2),
                        "Three-type early waves and unchanged fourth-wave objective");
                    foreach (var rule in b.SpawnRules)
                    {
                        bool enabled = (i == 1 && rule.Channel.name == "CFG_SecurityGuard_Channel") ||
                            (i == 2 && rule.Channel.name == "CFG_EN_SpawnChannel_DataCrawlerSnake") ||
                            (i == 3 && rule.Channel.name == "CFG_Download_Channel");
                        Check(a.TryGetRule(rule.Channel, out var copy) && copy.Enabled == enabled, "Only selected legacy channel enabled per wave");
                    }
                    Check(!all.OfType<DoubaoChapterEncounterDriver2D>().Single().IsRequiredForSegment(i), "Doubao disabled in all four waves");
                    Check(!all.OfType<KimiChapterEncounterDriver2D>().Single().IsRequiredForSegment(i), "Kimi disabled in all four waves");
                }
                foreach (var entry in bindings.Enemies.Where(e => e.EntryId.StartsWith("recursive-", StringComparison.Ordinal)))
                {
                    var actor = (EnemyActor2D)new SerializedObject(entry.Pool).FindProperty("_enemyPrefab").objectReferenceValue;
                    var jelly = actor.GetComponent<RecursiveJelly2D>();
                    Check(actor.TryValidateConfiguration(out reason) && jelly.TryValidateConfiguration(out reason), reason);
                    Check(jelly.Renderer.sprite == AssetDatabase.LoadAssetAtPath<Sprite>(RecursiveJellyInstaller.ArtPath), "Approved artwork");
                    Check(jelly.Renderer.transform != actor.transform && actor.transform.localScale == Vector3.one, "Visual/physics roots separated");
                    Check(bindings.WorldSnapshot.Catalog.Entries.Any(e => e.Sprite == jelly.Renderer.sprite), "Sprite registered for peer");
                    for (int s = 1; s <= 4; s++)
                        Check(bindings.Level.ChapterRunConfig.GetSegment(s).TryGetRule(entry.Director.Channel, out var rule) &&
                            rule.Enabled == (entry.EntryId == "recursive-large"), "Only large recursion scheduled; children come from splitting");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return "RECURSIVE ASSETS PASS " + checks;
        }

        public static string RunPlay()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start isolated World02 Play first.");
            var scene = SceneManager.GetSceneByPath(RecursiveJellyInstaller.ScenePath);
            var bindings = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelSceneBindings>(true)).Single();
            var large = bindings.Enemies.Single(e => e.EntryId == "recursive-large");
            var medium = bindings.Enemies.Single(e => e.EntryId == "recursive-medium");
            var small = bindings.Enemies.Single(e => e.EntryId == "recursive-small");
            int checks = 0;
            void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
            void Clear() { large.Pool.DespawnAll(EnemyDespawnReason.RunReset); medium.Pool.DespawnAll(EnemyDespawnReason.RunReset); small.Pool.DespawnAll(EnemyDespawnReason.RunReset); }
            var variation = new EnemySpawnVariation2D(Vector2.left, .85f, 0, 0);
            var hit = new DamagePacket(1, Vector2.zero, Vector2.right, bindings.gameObject);
            var lethal = new DamagePacket(100, Vector2.zero, Vector2.right, bindings.gameObject);
            foreach (var e in bindings.Enemies) e.Director.Stop();
            Clear();
            try
            {
                Check(large.Pool.TryRent(Vector2.zero, in variation, out var parent), "Rent actual large actor");
                var jelly = parent.GetComponent<RecursiveJelly2D>(); var shape = (BoxCollider2D)jelly.Shape;
                Vector2 size = shape.size; Vector3 rootScale = parent.transform.localScale;
                Vector3 idleScale = jelly.Renderer.transform.localScale, idlePosition = jelly.Renderer.transform.localPosition;
                var stride = jelly.GetType().GetMethod("AdvanceStride", Private);
                var reachMove = (Vector2)stride.Invoke(jelly, new object[] { .18f });
                Call(jelly, "AdvanceVisual", .5f);
                Check(reachMove == Vector2.zero && jelly.Renderer.transform.localPosition.x < idlePosition.x &&
                    jelly.Renderer.transform.localScale.x > idleScale.x, "Front reaches before body advances");
                Check(jelly.Renderer.transform.localRotation == Quaternion.identity, "No side-to-side rocking");
                var pullMove = (Vector2)stride.Invoke(jelly, new object[] { .45f });
                Call(jelly, "AdvanceVisual", 0f);
                Check(Mathf.Abs(pullMove.magnitude - jelly.Config.StrideDistance) < .0001f &&
                    (jelly.Renderer.transform.localPosition - idlePosition).sqrMagnitude < .00001f, "Pull finishes one step and restores rear");
                var restMove = (Vector2)stride.Invoke(jelly, new object[] { .2f });
                Check(restMove == Vector2.zero && shape.size == size, "Rest interval stationary with fixed collider");
                parent.Activate(Vector2.zero, in variation);
                Call(jelly, "AdvanceVisual", .05f); Vector3 movingScale = jelly.Renderer.transform.localScale;
                Check(parent.Health.TryReceiveDamage(in hit) && parent.Health.CurrentHealth == 19, "Real damage/health");
                Call(jelly, "AdvanceVisual", 0f);
                Check(jelly.Renderer.transform.localScale != movingScale, "Hit squash changes visual");
                Check(shape.size == size && parent.transform.localScale == rootScale, "Squash never changes collider or physical root");
                Check(parent.Health.TryReceiveDamage(in lethal) && medium.Pool.ActiveCount == 4 && large.Pool.ActiveCount == 0, "1 large becomes 4 medium");
                var middles = medium.Pool.Instances.Where(a => a.gameObject.activeSelf).ToArray();
                Check(middles.Count(a => a.transform.position.x < 0 && a.transform.position.y > 0) == 1 &&
                    middles.Count(a => a.transform.position.x > 0 && a.transform.position.y > 0) == 1 &&
                    middles.Count(a => a.transform.position.x < 0 && a.transform.position.y < 0) == 1 &&
                    middles.Count(a => a.transform.position.x > 0 && a.transform.position.y < 0) == 1, "Four diagonal births");
                foreach (var middle in middles)
                {
                    var m = middle.GetComponent<RecursiveJelly2D>();
                    Check(m.IsBirthProtected && !m.Shape.enabled && !m.Hitbox.TryReceiveDamage(in hit), "Newborn collision/damage protected");
                    Call(m, "AdvanceVisual", .25f);
                    Check(m.Shape.enabled && m.Hitbox.enabled && middle.Health.MaximumHealth == 15, "Protection expires and stage health correct");
                    Check(m.Hitbox.TryReceiveDamage(in lethal), "Kill through actual hitbox");
                }
                Check(medium.Pool.ActiveCount == 0 && small.Pool.ActiveCount == 8, "4 medium become 8 small");
                foreach (var child in small.Pool.Instances.Where(a => a.gameObject.activeSelf).ToArray())
                {
                    var m = child.GetComponent<RecursiveJelly2D>(); Call(m, "AdvanceVisual", .25f);
                    Check(child.Health.MaximumHealth == 10 && m.Hitbox.TryReceiveDamage(in lethal), "Small leaf dies normally");
                }
                Check(small.Pool.ActiveCount == 0, "Leaves never split");
                Check(large.Pool.TryRent(Vector2.zero, in variation, out parent), "Rerent after death");
                jelly = parent.GetComponent<RecursiveJelly2D>();
                Check(parent.Health.CurrentHealth == 20 && jelly.Shape.enabled && !jelly.IsBirthProtected, "Pool resets health/protection");
                parent.TryRequestDespawn(EnemyDespawnReason.RunReset);
                Check(medium.Pool.ActiveCount == 0, "RunReset cannot split");
                large.Pool.TryRent(Vector2.zero, in variation, out parent); parent.TryRequestDespawn(EnemyDespawnReason.ContactImpact);
                Check(medium.Pool.ActiveCount == 0, "Contact despawn cannot split");
                for (int i = 0; i < 3; i++) { large.Pool.TryRent(Vector2.zero, in variation, out parent); parent.Health.TryReceiveDamage(in lethal); }
                Check(medium.Pool.ActiveCount == 12, "Three families reach twelve medium");
                Check(!large.Director.TrySpawnNow(), "New families blocked while descendants reserve full capacity");
                foreach (var m in medium.Pool.Instances.Where(a => a.gameObject.activeSelf).ToArray()) m.Health.TryReceiveDamage(in lethal);
                Check(small.Pool.ActiveCount == 24 && !large.Director.TrySpawnNow(), "All descendants fit, 24 leaves block new roots");
                var sample = small.Pool.Instances.First(a => a.gameObject.activeSelf).GetComponent<RecursiveJelly2D>();
                Call(sample, "AdvanceVisual", .08f);
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    NetworkEntityView.Write(writer, new[] { sample.Renderer }, bindings.WorldSnapshot.Catalog);
                    stream.Position = 0; using var reader = new BinaryReader(stream);
                    Check(reader.ReadByte() == 1 && reader.ReadUInt32() != 0 && reader.ReadBoolean(), "Peer snapshot has sprite/shown layer");
                    for (int i = 0; i < 3; i++) reader.ReadSingle();
                    var scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Check((scale - sample.Renderer.transform.lossyScale).sqrMagnitude < .00001f, "Existing wire carries jelly deformation");
                }
                Clear();
                Check(medium.Pool.ActiveCount == 0 && small.Pool.ActiveCount == 0 && large.Director.TrySpawnNow(), "Cleanup frees capacity and allows new family");
            }
            finally { Clear(); }
            return "RECURSIVE PLAY PASS " + checks + ":1→4→8, protected birth, damage squash, unchanged shape, reset/contact cleanup, bounded families, peer sprite/scale.";
        }
    }
}
