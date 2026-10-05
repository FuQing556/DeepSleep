using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DeepSleep.Runtime.Combat.Encounters.Doubao;
using UnityEditor;
using UnityEngine;

namespace DeepSleep.Editor.Diagnostics
{
    public static class DoubaoMazeChecks
    {
        public static string RunSentenceSpacing()
        {
            var cfg=AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(DeepSleep.Editor.Setup.DoubaoMazeInstaller.ConfigPath);
            float columnPitch=cfg.CoverageWidth/cfg.MazeColumns;
            float minimum=float.PositiveInfinity;
            int pairs=0;
            var offsets=new[]{new Vector2(columnPitch,0),new Vector2(0,cfg.MazeRowPitch),
                new Vector2(columnPitch,cfg.MazeRowPitch),new Vector2(-columnPitch,cfg.MazeRowPitch)};
            for(int a=0;a<cfg.PatternCount;a++)for(int b=0;b<cfg.PatternCount;b++)
            foreach(var offset in offsets)
            {
                var first=cfg.GetPattern(a);var second=cfg.GetPattern(b);
                for(int i=0;i<first.SlotCount;i++)for(int j=0;j<second.SlotCount;j++)
                {
                    var x=first.GetSlot(i);var y=second.GetSlot(j);
                    float gap=Vector2.Distance(x.Offset,y.Offset+offset)-(x.Width+y.Width)*.5f;
                    minimum=Mathf.Min(minimum,gap);pairs++;
                    if(gap<cfg.MazeSentenceGap-.0001f)throw new Exception("Sentences overlap or lack gap");
                }
            }
            var invalid=UnityEngine.Object.Instantiate(cfg);
            try
            {
                var so=new SerializedObject(invalid);so.FindProperty("_mazeRowPitch").floatValue=1.75f;
                so.ApplyModifiedPropertiesWithoutUndo();
                if(invalid.TryValidate(out _))throw new Exception("Old overlapping row pitch accepted");
            }
            finally{UnityEngine.Object.DestroyImmediate(invalid);}
            return pairs+" cross-sentence pairs checked; minimum gap="+minimum.ToString("F4")+"; old overlapping config rejected.";
        }

        public static string RunSpacingPlay()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play required");
            var encounter=UnityEngine.Object.FindAnyObjectByType<DoubaoWordWallEncounter2D>();
            var cfg=AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(DeepSleep.Editor.Setup.DoubaoMazeInstaller.ConfigPath);
            var playfield=(DeepSleep.Runtime.World.Playfield.CombatPlayfieldConfig)new SerializedObject(encounter).FindProperty("_playfield").objectReferenceValue;
            var colliders=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r=>r.GetComponentsInChildren<Collider2D>(true)).Where(c=>c.enabled).ToArray();
            var otherBodies=UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(b=>b.simulated && b.GetComponent<DoubaoWordWallBlock2D>()==null).ToArray();
            float previousTime=Time.timeScale;var mode=Physics2D.simulationMode;
            float minimum=float.PositiveInfinity;int checks=0,peak=0;
            try
            {
                Time.timeScale=0;Physics2D.simulationMode=SimulationMode2D.Script;
                foreach(var body in otherBodies)body.simulated=false;
                foreach(var c in colliders)c.enabled=false;
                foreach(float dt in new[]{.02f,1f/30f})
                {
                    encounter.ResetEncounter();encounter.BeginEncounter();float elapsed=0;
                    int ticks=Mathf.CeilToInt(90/dt);
                    for(int tick=0;tick<ticks;tick++)
                    {
                        elapsed+=dt;encounter.Simulate(dt);Physics2D.Simulate(dt);
                        peak=Mathf.Max(peak,encounter.ActiveBlocks.Count);
                        if(tick%10!=0)continue;
                        var blocks=encounter.ActiveBlocks;
                        var positions=blocks.Select(b=>b.GetComponent<Rigidbody2D>().position).ToArray();
                        var groups=new Vector2Int[blocks.Count];
                        float width=Mathf.Min(cfg.CoverageWidth,playfield.WorldBounds.width);
                        float top=playfield.WorldBounds.yMax+cfg.SpawnTopPadding;
                        for(int i=0;i<blocks.Count;i++)groups[i]=new Vector2Int(
                            Mathf.RoundToInt((positions[i].x-playfield.WorldBounds.center.x+width*.5f)/(width/cfg.MazeColumns)-.5f),
                            Mathf.RoundToInt((positions[i].y-top+(elapsed-cfg.InitialDelaySeconds)*cfg.FallSpeed)/cfg.MazeRowPitch));
                        for(int i=0;i<blocks.Count;i++)for(int j=i+1;j<blocks.Count;j++)
                        {
                            if(groups[i]==groups[j])continue;
                            float gap=Vector2.Distance(positions[i],positions[j])-(blocks[i].Size.x+blocks[j].Size.x)*.5f;
                            minimum=Mathf.Min(minimum,gap);checks++;
                            if(gap<cfg.MazeSentenceGap-.002f)throw new Exception("Runtime sentence gap lost: "+gap);
                        }
                        if(dt==.02f && tick==1000)Capture("spacing_20261005_20seconds.png");
                    }
                }
                return "2 x 90s physics simulation (50/30 Hz), "+checks+" cross-sentence checks; min="+
                    minimum.ToString("F4")+", peak="+peak+". Player/enemy contact isolated.";
            }
            finally
            {
                encounter.ResetEncounter();foreach(var c in colliders)if(c!=null)c.enabled=true;
                foreach(var body in otherBodies)if(body!=null)body.simulated=true;
                Physics2D.simulationMode=mode;Time.timeScale=previousTime;
            }
        }

        public static string RunMelee()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play required");
            var encounter=UnityEngine.Object.FindAnyObjectByType<DoubaoWordWallEncounter2D>();
            var executor=UnityEngine.Object.FindAnyObjectByType<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleeDamageExecutor2D>();
            var cfg=AssetDatabase.LoadAssetAtPath<DeepSleep.Runtime.Combat.Weapons.Harness.Melee.HarnessMeleeConfig>(
                "Assets/_Project/Configs/Combat/Harness/Melee/CFG_HA_Melee_Default.asset");
            var health=typeof(DoubaoWordWallBlock2D).GetField("_currentHealth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            try
            {
                foreach(var attack in cfg.Attacks)for(int mode=0;mode<2;mode++)
                {
                    encounter.ResetEncounter();encounter.BeginEncounter();encounter.Simulate(1.02f);
                    var b=encounter.ActiveBlocks.First();
                    Vector2 point;
                    if(mode==0)
                    {
                        DeepSleep.Runtime.Combat.Weapons.Harness.Melee.MeleeSwordGeometry2D.Evaluate(attack,Vector2.zero,0,.5f,out var hilt,out var tip);
                        point=(hilt+tip)*.5f;
                    }
                    else
                    {
                        Vector2 centroid=Vector2.zero;foreach(var vertex in attack.WavePolygon)centroid+=vertex;
                        centroid/=attack.WavePolygon.Length;
                        point=DeepSleep.Runtime.Combat.Weapons.Harness.Melee.MeleeSwordGeometry2D.WavePosition(attack,Vector2.zero,0)+
                            DeepSleep.Runtime.Combat.Weapons.Harness.Melee.MeleeSwordGeometry2D.WaveLocalVector(attack,centroid,0);
                    }
                    b.transform.position=point;b.GetComponent<Rigidbody2D>().position=point;Physics2D.SyncTransforms();
                    float before=(float)health.GetValue(b);executor.BeginSwing();
                    if(mode==0)executor.Sweep(attack,Vector2.zero,Vector2.zero,0,.5f,.5f);
                    else executor.Burst(attack,Vector2.zero,0,false);
                    if((float)health.GetValue(b)>=before)throw new Exception("Melee failed to damage bubble: "+attack.name+" mode "+mode);
                }
                return "PASS: all 3 blade and 3 sword-wave physics queries damage bubble layer.";
            }
            finally{encounter.ResetEncounter();}
        }

        public static string RunReplica()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play required");
            var channel=UnityEngine.Object.FindAnyObjectByType<DeepSleep.Runtime.Networking.DoubaoEncounterNetworkChannel>();
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var read=channel.GetType().GetMethod("Read",flags);
            var clear=channel.GetType().GetMethod("Clear",flags);
            var views=(System.Collections.IDictionary)channel.GetType().GetField("_views",flags).GetValue(channel);
            int bytes=0;
            try
            {
                clear.Invoke(channel,null);
                for(int pass=0;pass<4;pass++)
                {
                    using var stream=new MemoryStream();
                    using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                    {
                        w.Write((uint)(pass==2?1:pass+1));
                        w.Write((byte)(pass==3?DoubaoEncounterState.Complete:DoubaoEncounterState.Active));
                        w.Write(false);w.Write(0f);w.Write(0f);w.Write(45f);w.Write(false);w.Write(0f);
                        w.Write((uint)0);w.Write(1f);
                        w.Write((byte)(pass==3?0:240));
                        if(pass!=3)for(int i=0;i<240;i++)
                        {
                            w.Write((uint)(i+1+pass*240));w.Write(i*.01f);w.Write(0f);
                            w.Write(1.24f);w.Write(1.24f);w.Write("豆");
                        }
                    }
                    bytes=Math.Max(bytes,(int)stream.Length);stream.Position=0;
                    using var r=new BinaryReader(stream);
                    read.Invoke(channel,new object[]{DeepSleep.Runtime.Networking.NetworkMessageCatalog.Authority.DoubaoSnapshot,r});
                    if(views.Count!=(pass==3?0:240))throw new Exception("Replica count incorrect at pass "+pass);
                    if(pass==1 && !views.Contains((uint)241))throw new Exception("Full pool replacement lost new IDs");
                    if(pass==2 && !views.Contains((uint)241))throw new Exception("Stale snapshot accepted");
                }
                return "PASS: 240 replicas, full-ID replacement, stale-frame rejection, complete clear; synthetic payload="+bytes+" bytes (requires fragmentation). Not a real transport test.";
            }
            finally{clear.Invoke(channel,null);}
        }

        public static string RunInteractions()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play required");
            var director=UnityEngine.Object.FindAnyObjectByType<DeepSleep.Runtime.Combat.Enemies.EnemySpawnDirector2D>();
            var pool=(DeepSleep.Runtime.Combat.Enemies.EnemyActorPool2D)new SerializedObject(director).FindProperty("_pool").objectReferenceValue;
            var wallet=UnityEngine.Object.FindAnyObjectByType<DeepSleep.Runtime.Progression.Economy.TokenWallet>();
            var snapshot=wallet.CaptureSnapshot();
            var encounter=UnityEngine.Object.FindAnyObjectByType<DoubaoWordWallEncounter2D>();
            float previousTime=Time.timeScale;
            try
            {
                Time.timeScale=0;
                wallet.BeginBattle();
                pool.DespawnAll(DeepSleep.Runtime.Combat.Enemies.EnemyDespawnReason.ExitedPlayfield);
                director.SetEncounterMaximumAliveCount(1);
                if(!director.TrySpawnNow() || director.TrySpawnNow())throw new Exception("Spawn cap failed");
                var enemy=pool.Instances.Single(e=>e.gameObject.activeSelf);
                enemy.Health.RestoreCheckpointHealth(1);
                encounter.ResetEncounter();encounter.BeginEncounter();encounter.Simulate(1.02f);
                var bubble=encounter.ActiveBlocks.First();
                var hitbox=enemy.GetComponentInChildren<DeepSleep.Runtime.Combat.Damage.DamageHitbox2D>();
                var collider=hitbox.GetComponent<Collider2D>();
                // 使用真实接触处理函数和真实敌人/奖励订阅链；不声称物理移动验收。
                typeof(DoubaoWordWallBlock2D).GetMethod("OnTriggerEnter2D",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                    .Invoke(bubble,new object[]{collider});
                if(pool.ActiveCount!=0 || wallet.BattleEarned!=0 || bubble.IsActive)
                    throw new Exception("Bubble contact must consume, defeat enemy, and award no Token");
                if(!director.TrySpawnNow())throw new Exception("Pool reuse failed");
                enemy=pool.Instances.Single(e=>e.gameObject.activeSelf);
                if(enemy.Health.LastDamageSuppressesKillReward)throw new Exception("Reward flag leaked across pool reuse");
                var damage=new DeepSleep.Runtime.Combat.Damage.DamagePacket(999,enemy.transform.position,Vector2.left,encounter.gameObject);
                enemy.Health.TryReceiveDamage(in damage);
                if(wallet.BattleEarned<1 || wallet.BattleEarned>5)throw new Exception("Normal kill lost reward");
                director.SetEncounterMaximumAliveCount(0);
                if(director.EffectiveMaximumAliveCount<1)throw new Exception("Cap release failed");
                var effects=UnityEngine.Object.FindObjectsByType<DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D>(FindObjectsSortMode.None)
                    .Single(p=>new SerializedObject(p).FindProperty("_config").objectReferenceValue.name=="CFG_DB_BubbleBreak");
                effects.enabled=false;effects.enabled=true;
                for(int i=0;i<240;i++)if(!effects.TryPlay(Vector2.zero,0))throw new Exception("Full maze clear VFX dropped at "+i);
                effects.enabled=false;effects.enabled=true;
                return "PASS: actual bubble contact handler -> enemy death -> zero Token; pooled normal kill -> 1..5 Token; spawn cap; 240 simultaneous break effects. Contact callback injected, not a movement or network test.";
            }
            finally
            {
                director.SetEncounterMaximumAliveCount(0);
                encounter.ResetEncounter();wallet.RestoreSnapshot(in snapshot);Time.timeScale=previousTime;
            }
        }

        public static string RunPlay(string captureName = "maze_20seconds.png")
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Start through Boot first");
            var encounter=UnityEngine.Object.FindAnyObjectByType<DoubaoWordWallEncounter2D>();
            var cfg=AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(DeepSleep.Editor.Setup.DoubaoMazeInstaller.ConfigPath);
            var colliders=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(r=>r.GetComponentsInChildren<Collider2D>(true)).Where(c=>c.enabled).ToArray();
            var otherBodies=UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(b=>b.simulated && b.GetComponent<DoubaoWordWallBlock2D>()==null).ToArray();
            float time=Time.timeScale;
            var simulation=Physics2D.simulationMode;
            int peak=0;
            try
            {
                Time.timeScale=0;
                Physics2D.simulationMode=SimulationMode2D.Script;
                foreach(var body in otherBodies)body.simulated=false;
                foreach(var c in colliders)c.enabled=false;
                encounter.ResetEncounter();encounter.BeginEncounter();
                for(int tick=0;tick<9000;tick++)
                {
                    encounter.Simulate(.02f);Physics2D.Simulate(.02f);
                    peak=Mathf.Max(peak,encounter.ActiveBlocks.Count);
                    if(encounter.ActiveBlocks.Count>=cfg.PoolCapacity)throw new Exception("Pool capacity exhausted");
                    if(tick==999)Capture(captureName);
                }
                if(encounter.State!=DoubaoEncounterState.Active)throw new Exception("Boss did not appear");
                var packet=new DeepSleep.Runtime.Combat.Damage.DamagePacket(999,encounter.Boss.transform.position,
                    Vector2.up,encounter.gameObject,DeepSleep.Runtime.Combat.Damage.DamageAttackIdAllocator.Next(),
                    DeepSleep.Runtime.Combat.Damage.DamageInterceptionPolicy.Blockable);
                encounter.Boss.TryReceiveDamage(in packet);
                if(encounter.State!=DoubaoEncounterState.Complete || encounter.ActiveBlocks.Count!=0)
                    throw new Exception("Boss defeat did not clear maze");
                encounter.Simulate(10);
                if(encounter.ActiveBlocks.Count!=0)throw new Exception("Completed encounter restarted");
                encounter.ResetEncounter();encounter.BeginEncounter();encounter.Simulate(1.02f);Physics2D.Simulate(.02f);
                if(encounter.ActiveBlocks.Count==0)throw new Exception("Reset did not restart");
                // 首波被提前清空后，出场仍使用逻辑抵达时间。
                foreach(var b in encounter.ActiveBlocks.ToArray())b.ClearWithEffect();
                for(int i=0;i<1100;i++){encounter.Simulate(.02f);Physics2D.Simulate(.02f);}
                if(encounter.State!=DoubaoEncounterState.Active)throw new Exception("Cleared first wave blocked boss reveal");
                return "180 simulated seconds / 9000 physics steps; peak="+peak+"/"+cfg.PoolCapacity+
                    "; boss reveal, immediate clear, no restart, reset, early-first-wave-clear passed. Player/enemy contact disabled for isolation.";
            }
            finally
            {
                encounter.ResetEncounter();
                foreach(var c in colliders)if(c!=null)c.enabled=true;
                foreach(var body in otherBodies)if(body!=null)body.simulated=true;
                Physics2D.simulationMode=simulation;Time.timeScale=time;
            }
        }

        private static void Capture(string name)
        {
            var camera=Camera.main;
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            float previousAspect=camera.aspect;
            var rt=new RenderTexture(1280,720,24);Texture2D image=null;
            try
            {
                camera.targetTexture=rt;camera.aspect=16f/9f;
                UnityEngine.Object.FindAnyObjectByType<DeepSleep.Runtime.Presentation.FinitePanoramaLayer2D>().FitNow();
                camera.Render();RenderTexture.active=rt;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                string path="docs/ImplementationEvidence/20260915_DoubaoMaze";
                Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,name),image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previousTarget;camera.aspect=previousAspect;RenderTexture.active=previousActive;
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        public static string RunGeometry()
        {
            var cfg=AssetDatabase.LoadAssetAtPath<DoubaoWordWallConfig>(DeepSleep.Editor.Setup.DoubaoMazeInstaller.ConfigPath);
            int rows=0,groups=0,checks=0,minGroups=int.MaxValue,maxGroups=0;
            for(int seed=0;seed<100;seed++)
            {
                var route=new DoubaoMazeRoute(seed,cfg.MazeColumns,cfg.MazeHoldRows,cfg.CoverageWidth,cfg.MazeRowPitch);
                var repeat=new DoubaoMazeRoute(seed,cfg.MazeColumns,cfg.MazeHoldRows,cfg.CoverageWidth,cfg.MazeRowPitch);
                var random=new System.Random(seed);
                var bubbles=new List<Vector3>();
                for(int row=0;row<60;row++)
                {
                    int rowGroups=0;
                    for(int col=0;col<cfg.MazeColumns;col++)
                    {
                        var pattern=cfg.GetPattern(random.Next(cfg.PatternCount));
                        float x=-cfg.CoverageWidth*.5f+(col+.5f)*cfg.CoverageWidth/cfg.MazeColumns;
                        bool intersects=false;
                        for(int i=0;i<pattern.SlotCount;i++)
                        {
                            var s=pattern.GetSlot(i);
                            if(route.IntersectsRoute(new Vector2(x+s.Offset.x,row*cfg.MazeRowPitch+s.Offset.y),
                                cfg.MazeRouteRadius+s.Width*.5f,row))intersects=true;
                        }
                        if(intersects)continue;
                        rowGroups++;
                        for(int i=0;i<pattern.SlotCount;i++)
                        {
                            var s=pattern.GetSlot(i);
                            bubbles.Add(new Vector3(x+s.Offset.x,row*cfg.MazeRowPitch+s.Offset.y,s.Width*.5f));
                        }
                    }
                    minGroups=Mathf.Min(minGroups,rowGroups);maxGroups=Mathf.Max(maxGroups,rowGroups);
                    groups+=rowGroups;rows++;
                    if(route.Point(row)!=repeat.Point(row))throw new Exception("Non-deterministic route");
                    float horizontal=Mathf.Abs(route.Point(row+1).x-route.Point(row).x);
                    if(horizontal>cfg.CoverageWidth/cfg.MazeColumns+.001f)throw new Exception("Skipped route column");
                    // 玩家保持世界高度时的横移需求，给 6.5u/s 移速留出充分余量。
                    if(horizontal*cfg.FallSpeed/cfg.MazeRowPitch>3f)throw new Exception("Route turns too quickly");
                }
                // 独立采样线段，检查实际生成圆体到角色保护圆的净空（非仅验证列编号）。
                for(int row=0;row<59;row++)for(int sample=0;sample<=12;sample++)
                {
                    var point=Vector2.Lerp(route.Point(row),route.Point(row+1),sample/12f);
                    foreach(var b in bubbles)
                    {
                        if(Mathf.Abs(b.y-point.y)>2f)continue;
                        if(Vector2.Distance(point,new Vector2(b.x,b.y))<cfg.MazeRouteRadius+b.z-.0001f)
                            throw new Exception("Blocked route "+seed+" / "+row);
                    }
                    checks++;
                }
            }
            if(minGroups<3)throw new Exception("Maze too sparse");
            return rows+" seeded rows, "+checks+" route samples clear; groups/row="+minGroups+".."+maxGroups+
                ", average="+((float)groups/rows).ToString("F2")+". Geometry only, not gameplay acceptance.";
        }
    }
}
