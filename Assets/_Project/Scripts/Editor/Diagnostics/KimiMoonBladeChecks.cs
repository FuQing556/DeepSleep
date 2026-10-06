using System;
using System.IO;
using System.Linq;
using DeepSleep.Editor.Setup;
using DeepSleep.Runtime.Combat.Damage;
using DeepSleep.Runtime.Combat.Encounters.Kimi;
using DeepSleep.Runtime.Combat.Health;
using DeepSleep.Runtime.Combat.Projectiles;
using DeepSleep.Runtime.Presentation;
using DeepSleep.Runtime.UI.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DeepSleep.Editor.Diagnostics
{
    /// <summary>独立且无存档服务的 Play 实测；不改正式关卡或构建清单。</summary>
    public static class KimiMoonBladeChecks
    {
        public const string Evidence = "docs/ImplementationEvidence/20261006_KimiMoonBlade";

        [MenuItem("DeepSleep/开发/Kimi/打开独立验收场")]
        public static void OpenPreview()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("请先退出Play并处理当前未保存场景。");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Kimi_Verification_Only");
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KimiContentInstaller.RigPath));
            var camera = new GameObject("VerificationCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5.4f;
            camera.transform.position = new Vector3(0,0,-10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.03f,.04f,.1f);
            var bg = new GameObject("MoonNight").AddComponent<SpriteRenderer>();
            bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Backgrounds/Kimi/BG_W01_MoonRiver_Panorama_v01.png");
            bg.sortingLayerName = "Background"; bg.sortingOrder = -100;
            bg.transform.localScale = Vector3.one * (10.8f / bg.sprite.bounds.size.y);
            ScaleReference("DS_size_reference", "DeepSeek/SPR_DS_Idle_Base_v01", new Vector2(-5, 0), 1.05f);
            ScaleReference("HS_size_reference", "Harness/SPR_HA_IdleFly_v03", new Vector2(-2,-2), 1);
            var canvas = new GameObject("VerificationCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            canvas.sortingLayerName = "UIWorld"; canvas.sortingOrder = 100;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = 1;
            var hud = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KimiContentInstaller.HudPath));
            hud.transform.SetParent(canvas.transform, false);
            var labelRoot = new GameObject("VerificationLabel", typeof(RectTransform)); labelRoot.transform.SetParent(canvas.transform,false);
            var label = labelRoot.AddComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22; label.color = Color.white; label.alignment = TextAnchor.LowerLeft;
            label.text = "UNITY PLAY TEST · Kimi skill module · Not full boss fight";
            var rect = (RectTransform)label.transform; rect.anchorMin = new Vector2(0,0); rect.anchorMax = new Vector2(1,0);
            rect.pivot = new Vector2(0,0); rect.offsetMin = new Vector2(24,18); rect.offsetMax = new Vector2(-24,60);
            // 不含 AppRoot / Profile / Session，任何测试失败都不可能写玩家存档。
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static string Run()
        {
            if (!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only") == null)
                throw new InvalidOperationException("必须在独立验收场 Play 中执行。");
            var pattern = UnityEngine.Object.FindAnyObjectByType<KimiMoonBladePattern2D>();
            var boss = pattern.Boss;
            var hud = UnityEngine.Object.FindAnyObjectByType<KimiBossHudView>();
            float oldScale = Time.timeScale;
            var oldMode = Physics2D.simulationMode;
            int checks = 0;
            Action<bool,string> require = (ok, message) => { if (!ok) throw new Exception(message); checks++; };
            GameObject target = null;
            HealthConfig targetConfig = null;
            try
            {
                Time.timeScale = 0; Physics2D.simulationMode = SimulationMode2D.Script;
                boss.BeginAuthority(new Vector2(6.5f,-.8f), null, null); hud.Bind(boss);
                require(boss.CanReceiveDamage && boss.CurrentHealth == 10000, "Boss initial HP/authority");
                Capture("phase1_idle", 1280,582);
                var packet = new DamagePacket(5000, boss.transform.position, Vector2.right, null);
                require(boss.TryReceiveDamage(in packet), "Boss accepts real damage");
                require(!boss.PhaseTwo && boss.CurrentHealth == 5000, "Half HP cannot interrupt current skill");
                require(boss.GetComponent<SpriteHitFlash2D>().IsPlaying, "Shared hit flash starts");
                require(boss.CommitPhaseAtSkillBoundary() && !boss.CommitPhaseAtSkillBoundary(), "Phase transition exactly once at skill boundary");
                hud.RenderNow(); require(hud.HealthFill.fillAmount == .5f, "Real uGUI half fill");
                boss.GetComponent<SpriteHitFlash2D>().ResetFeedback();
                Capture("phase2_half_health",1280,582);
                boss.ApplyReplica(true, new Vector2(6.5f,-.8f),5000,true,KimiPose.Idle);
                require(!boss.CanReceiveDamage && !boss.HitCollider.enabled && !boss.TryReceiveDamage(in packet), "Replica cannot author damage");
                boss.ResetEncounter(); hud.RenderNow(); require(hud.Visibility.alpha == 0, "Reset hides HUD");

                for (int phase = 0; phase < 2; phase++)
                {
                    boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                    if (phase == 1) { boss.TryReceiveDamage(in packet); boss.CommitPhaseAtSkillBoundary(); boss.GetComponent<SpriteHitFlash2D>().ResetFeedback(); }
                    require(pattern.Begin(phase == 1, 613 + phase,null), "Begin pattern");
                    int previousVolley = -1, maxActive = 0, warningChecks = 0;
                    bool capturedWarning = false, capturedFlight = false;
                    int completed = 0;
                    Action onComplete = () => completed++;
                    pattern.Completed += onComplete;
                    for (int step = 0; step < 2500 && pattern.State != KimiMoonState.Complete; step++)
                    {
                        if (pattern.State == KimiMoonState.Warning && pattern.FiredVolleys != previousVolley)
                        {
                            previousVolley = pattern.FiredVolleys;
                            bool left = false, right = false;
                            for (int i = 0; i < pattern.WarningCount; i++)
                            {
                                int lane = pattern.GetWarningLane(i);
                                require(lane >= 0 && lane < 10, "Lane bounds");
                                for(int j=0;j<i;j++) require(pattern.GetWarningLane(j)!=lane,"Distinct lanes within volley");
                                left |= pattern.IsWarningFromLeft(i); right |= !pattern.IsWarningFromLeft(i);
                            }
                            require(left && right,"Both directions in each volley"); warningChecks++;
                        }
                        if (!capturedWarning && pattern.State == KimiMoonState.Warning && pattern.WarningProgress > .85f)
                        { Capture("phase"+(phase+1)+"_warning",1280,582); capturedWarning=true; }
                        pattern.Projectiles.Simulate(.02f); pattern.Simulate(.02f); Physics2D.Simulate(.02f);
                        maxActive = Mathf.Max(maxActive,pattern.Projectiles.ActiveCount);
                        require(pattern.Projectiles.ActiveCount <= (phase == 0 ? 2 : 4),"Concurrent cap");
                        foreach(var projectile in pattern.Projectiles.Instances)
                            if(projectile.IsRented) require(!projectile.TryClear(),"HS cannot clear moon blade");
                        if (!capturedFlight && pattern.State == KimiMoonState.Flying &&
                            pattern.Projectiles.Instances.Any(p => p.IsRented && Mathf.Abs(p.transform.position.x) < 6))
                        { Capture("phase"+(phase+1)+"_flight",1280,582); capturedFlight=true; }
                    }
                    pattern.Completed -= onComplete;
                    require(pattern.State == KimiMoonState.Complete && completed==1,"Pattern completes exactly once");
                    require(pattern.FiredVolleys == 10 && pattern.FiredBlades == (phase==0?20:40),"Exact volley/blade totals");
                    require(warningChecks == 10 && maxActive == (phase==0?2:4),"10 warnings and expected peak occupancy");
                    require(pattern.Projectiles.ActiveCount==0 && pattern.Projectiles.TotalCount==4,"Prewarmed pool, no growth/leak");
                }

                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                pattern.Begin(false,10,null);
                pattern.Simulate(0);
                require(pattern.WarningProgress==0 && pattern.FiredBlades==0,"Pause cannot advance warning/fire");
                pattern.Simulate(1); pattern.Cancel();
                require(pattern.Projectiles.ActiveCount==0 && pattern.Warnings.All(w=>!w.enabled),"Cancel clears projectiles and warning");
                pattern.Begin(false,10,null);
                int firstLane=pattern.GetWarningLane(0); bool firstLeft=pattern.IsWarningFromLeft(0);
                pattern.Cancel(); pattern.Begin(false,10,null);
                require(firstLane==pattern.GetWarningLane(0) && firstLeft==pattern.IsWarningFromLeft(0),"Seed replay");
                var kill = new DamagePacket(10000,Vector2.zero,Vector2.right,null);
                boss.TryReceiveDamage(in kill); pattern.Simulate(.02f);
                require(pattern.State==KimiMoonState.Idle && pattern.Projectiles.ActiveCount==0,"Death cancels active pattern");

                // 真实 Unity Trigger -> DamageHitbox -> Health，检查两侧高速弹的伤害和回池。
                target = new GameObject("KimiDamageProbe"); target.SetActive(false); target.layer=LayerMask.NameToLayer("Player");
                targetConfig=ScriptableObject.CreateInstance<HealthConfig>();
                Set(targetConfig,"_maximumHealth",10f);
                var health=target.AddComponent<HealthComponent>(); Set(health,"_config",targetConfig);
                var hitbox=target.AddComponent<DamageHitbox2D>();Set(hitbox,"_receiverComponent",health);
                var collider=target.AddComponent<CircleCollider2D>();collider.radius=.25f;collider.isTrigger=true;
                target.SetActive(true);Physics2D.SyncTransforms();
                int accepted=0;ulong firstAttack=0;bool policy=true,unique=true;
                health.DamageAccepted += damage => { accepted++;policy &= damage.InterceptionPolicy==DamageInterceptionPolicy.Blockable;
                    if(firstAttack==0)firstAttack=damage.AttackId;else unique &= firstAttack!=damage.AttackId; };
                for(int side=0;side<2;side++)
                {
                    EnemyProjectile2D shot;
                    require(pattern.Projectiles.TryRent(new Vector2(side==0?-4:4,0),side==0?Vector2.right:Vector2.left,boss.gameObject,out shot),"Rent probe shot");
                    for(int i=0;i<80;i++){pattern.Projectiles.Simulate(.02f);Physics2D.Simulate(.02f);}
                }
                require(health.CurrentHealth==8 && accepted==2,"Two collisions deal exactly one damage each");
                require(policy && unique && firstAttack!=0,"DS-blockable policy and unique attack identities");
                require(pattern.Projectiles.ActiveCount==0,"Impact returns projectile");
                // 手动推进物理不推进渲染帧，清理诊断碰撞留下的纯表现特效后再截图。
                var effectPool=pattern.GetComponentInChildren<DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D>();
                effectPool.gameObject.SetActive(false);effectPool.gameObject.SetActive(true);
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);hud.Bind(boss);
                pattern.Begin(true,614,null);pattern.Simulate(.66f);Capture("final_mobile_warning",1280,582);
                Capture("final_desktop_warning",1920,1080);
                string report="PASS "+checks+" checks. Real Unity Play/Physics2D; phase1=10x2, phase2=10x4; no growth beyond4.\n"+
                    "Shared hit flash/pose module, boundary-only phase change, replica damage rejection, pause/cancel/death/reset, uGUI fill, bidirectional actual trigger damage checked.\n"+
                    "DS-blockable DamagePacket policy verified, not complete DS guard integration. No real AI, network transport, phone or full boss fight acceptance.\n";
                Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/verification.txt",report);
                return report;
            }
            finally
            {
                pattern.Cancel();
                if(target!=null) UnityEngine.Object.DestroyImmediate(target);
                if(targetConfig!=null) UnityEngine.Object.DestroyImmediate(targetConfig);
                Physics2D.simulationMode=oldMode;Time.timeScale=oldScale;
            }
        }

        public static string RunPrism()
        {
            if(!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only")==null)
                throw new InvalidOperationException("独立验收场 Play required");
            var prism=UnityEngine.Object.FindAnyObjectByType<KimiPrismPattern2D>();
            var boss=prism.Boss;var hud=UnityEngine.Object.FindAnyObjectByType<KimiBossHudView>();
            float oldScale=Time.timeScale;var oldMode=Physics2D.simulationMode;
            var targets=new DamageHitbox2D[2];var healths=new HealthComponent[2];
            var healthConfig=ScriptableObject.CreateInstance<HealthConfig>();Set(healthConfig,"_maximumHealth",10000f);
            GameObject registries=null;
            int checks=0;Action<bool,string> require=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
            try
            {
                Time.timeScale=0;Physics2D.simulationMode=SimulationMode2D.Script;
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject("Prism_PlayerProbe_"+i);go.SetActive(false);go.layer=LayerMask.NameToLayer("Player");
                    go.transform.position=i==0?new Vector2(-5,0):new Vector2(-2,-2);
                    healths[i]=go.AddComponent<HealthComponent>();Set(healths[i],"_config",healthConfig);
                    targets[i]=go.AddComponent<DamageHitbox2D>();Set(targets[i],"_receiverComponent",healths[i]);
                    go.AddComponent<CircleCollider2D>().radius=.25f;go.SetActive(true);
                }
                Physics2D.SyncTransforms();
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);hud.Bind(boss);
                require(prism.Begin(false,null,targets,null,null),"Prism begin");
                require(prism.Sides.All(s=>s.CurrentHealth==600 && s.CanReceiveDamage),"Four independent phase1 mirrors");
                Capture("prism_closed_frame",1280,582);
                registries=new GameObject("PrismRegistryProbe");
                var perception=registries.AddComponent<DeepSleep.Runtime.Combat.Perception.CombatPerceptionRegistry2D>();
                var obstacles=registries.AddComponent<DeepSleep.Runtime.Players.Companion.CompanionObstacleRegistry2D>();
                prism.Begin(false,null,targets,perception,obstacles);
                require(perception.TryResolve(prism.Sides[0].Shape,out var mirrorBody) && mirrorBody.IsObservable,"Mirror registered as attack target");
                require(prism.TryLaunch(Vector2.zero,Vector2.left),"Launch retained orb");
                Physics2D.SyncTransforms();
                var snapshot=new DeepSleep.Runtime.Players.Companion.CompanionObstacleSnapshot[16];
                require(obstacles.CopyVisible(Vector2.zero,50,snapshot,out bool saturated)==1 && !saturated,"Ball is avoidance obstacle");
                var orb=prism.Orbs.First(o=>o.Active);orb.Simulate(2.2f);
                require(orb.Active && !orb.Escaped && orb.Velocity.x>0 && orb.ReflectionCount==1,"Intact left edge reflects without consuming orb");
                orb.Clear();
                require(obstacles.RegisteredCount==0,"Return unregisters moving obstacle");
                Vector2 corner=new Vector2(prism.ReflectionBounds.xMax,prism.ReflectionBounds.yMax);
                prism.TryLaunch(Vector2.zero,corner);orb=prism.Orbs.First(o=>o.Active);orb.Simulate(corner.magnitude/5+.1f);
                require(orb.Velocity.x<0 && orb.Velocity.y<0 && orb.ReflectionCount==1,"Exact corner reflects both axes once");
                orb.Clear();
                var kill=new DamagePacket(2000,Vector2.zero,Vector2.right,null);
                require(prism.Sides[0].TryReceiveDamage(in kill),"Mirror accepts common damage packet");
                require(!prism.Sides[0].IsIntact && !prism.Sides[0].Shape.enabled && prism.Sides.Skip(1).All(s=>s.IsIntact),"Breaking one side preserves other3");
                prism.TryLaunch(Vector2.zero,Vector2.left);orb=prism.Orbs.First(o=>o.Active);orb.Simulate(2);
                require(orb.Active && orb.Escaped && orb.ReflectionCount==0,"Orb exits broken side, remains until outside margin");
                Capture("prism_left_broken",1280,582);
                orb.Simulate(1);require(!orb.Active,"Escaped orb recycled outside logical view");

                prism.Begin(false,null,targets,null,null);
                float before=healths[0].CurrentHealth;int contacts=0;bool blockable=true;
                healths[0].DamageAccepted+=damage=>{contacts++;blockable&=damage.InterceptionPolicy==DamageInterceptionPolicy.Blockable && damage.AttackId!=0;};
                targets[0].gameObject.AddComponent<BoxCollider2D>().size=new Vector2(.4f,.4f);
                Physics2D.SyncTransforms();
                prism.TryLaunch(new Vector2(-7,0),Vector2.right);orb=prism.Orbs.First(o=>o.Active);orb.Simulate(1);
                require(healths[0].CurrentHealth==before-1 && contacts==1,"Swept hit deals one damage across duplicate victim colliders");
                require(orb.Active && blockable,"Hit does not destroy ball; DS-blockable unique identity");
                require(orb.GetComponent<EnemyProjectile2D>()==null && orb.GetComponent<DamageHitbox2D>()==null,"No HS clear / attackable ball entry");

                int[] shotCounts=new int[2];
                for(int phase=0;phase<2;phase++)
                {
                    boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                    if(phase==1){boss.TryReceiveDamage(new DamagePacket(5000,Vector2.zero,Vector2.zero,null));boss.CommitPhaseAtSkillBoundary();boss.GetComponent<SpriteHitFlash2D>().ResetFeedback();}
                    prism.Begin(phase==1,null,targets,null,null);
                    require(prism.Sides.All(s=>s.CurrentHealth==(phase==0?600:1200)),"Phase health snapshot");
                    int completed=0;Action done=()=>completed++;prism.Completed+=done;
                    for(int step=0;step<610 && prism.Active;step++)
                    {
                        prism.Simulate(.02f);Physics2D.SyncTransforms();
                        // 固定步快速测试不推进LateUpdate；逐步释放纯表现，防止假报特效池耗尽。
                        ResetEffects(prism);
                        foreach(var activeOrb in prism.Orbs.Where(o=>o.Active))
                        {
                            require(prism.ReflectionBounds.Contains(activeOrb.Position),"Unbroken frame retains orb");
                            require(Mathf.Abs(activeOrb.Velocity.magnitude-(phase==0?5:7.5f))<.001f,"Phase speed without energy growth");
                        }
                        if(step==410) {ResetEffects(prism);Capture("prism_phase"+(phase+1)+"_active",1280,582);}
                    }
                    prism.Completed-=done;shotCounts[phase]=prism.FiredCount;
                    require(prism.Complete && !prism.Active && completed==1,"Timed complete exactly once");
                    require(prism.Orbs.All(o=>!o.Active) && prism.Sides.All(s=>!s.IsIntact && !s.Shape.enabled),"Expiry clears balls and sides");
                }
                require(shotCounts[0]==12 && shotCounts[1]==12,"Same12 shots both phases");
                prism.Begin(false,null,targets,null,null);
                foreach(var side in prism.Sides)side.TryReceiveDamage(in kill);
                prism.Simulate(1);
                require(prism.Active && !prism.Complete && prism.Sides.All(s=>!s.IsIntact),"All four broken does not end timer or repair frame");
                float elapsed=prism.Elapsed;prism.Simulate(0);require(prism.Elapsed==elapsed,"Pause");
                prism.Cancel();require(prism.Orbs.All(o=>!o.Active) && prism.Corners.All(c=>!c.enabled),"Cancel clears all slots/corners");
                prism.Begin(false,null,targets,null,null);boss.TryReceiveDamage(new DamagePacket(10000,Vector2.zero,Vector2.zero,null));prism.Simulate(.02f);
                require(!prism.Active && !prism.Complete && prism.Sides.All(s=>!s.IsIntact),"Boss death cancels without success event");
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                boss.TryReceiveDamage(new DamagePacket(5000,Vector2.zero,Vector2.zero,null));boss.CommitPhaseAtSkillBoundary();boss.GetComponent<SpriteHitFlash2D>().ResetFeedback();
                prism.Begin(true,null,targets,null,null);
                for(int i=0;i<350;i++){prism.Simulate(.02f);ResetEffects(prism);}
                ResetEffects(prism);Capture("prism_final_mobile",1280,582);Capture("prism_final_desktop",1920,1080);
                string report="PRISM PASS "+checks+" sampled checks; timed12s/12shots both phases; speeds5/7.5; mirrorHP600/1200.\n"+
                    "Independent damageable sides; retained/corner reflections; broken-side exit; swept player contact retained; duplicate collider dedup; cancel/death/expiry/pause/reset passed.\n"+
                    "No DS/HS full weapon integration, actual guard, AI navigation, network transport or device acceptance in this isolated test.\n";
                File.WriteAllText(Evidence+"/prism_verification.txt",report);return report;
            }
            finally
            {
                prism.Cancel();foreach(var target in targets)if(target!=null)UnityEngine.Object.DestroyImmediate(target.gameObject);
                if(registries!=null)UnityEngine.Object.DestroyImmediate(registries);
                UnityEngine.Object.DestroyImmediate(healthConfig);Physics2D.simulationMode=oldMode;Time.timeScale=oldScale;
            }
        }

        public static string RunEncounter()
        {
            if(!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only")==null)
                throw new InvalidOperationException("必须在独立验收场Play执行。");
            var e=UnityEngine.Object.FindAnyObjectByType<KimiEncounter2D>();
            var hud=UnityEngine.Object.FindAnyObjectByType<KimiBossHudView>();
            var targets=new DamageHitbox2D[2];var hc=ScriptableObject.CreateInstance<HealthConfig>();Set(hc,"_maximumHealth",100000f);
            float oldScale=Time.timeScale;var oldMode=Physics2D.simulationMode;
            int checks=0,takeovers=0,completions=0;
            Action<bool,string> require=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
            Action takeover=()=>takeovers++;Action complete=()=>completions++;
            e.TakeoverRequested+=takeover;e.Completed+=complete;
            var order=new System.Collections.Generic.List<KimiSkill>();
            Action<KimiSkill> cast=skill=>order.Add(skill);e.CastStarted+=cast;
            try
            {
                Time.timeScale=0;Physics2D.simulationMode=SimulationMode2D.Script;
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject("EncounterProbe_"+i);go.SetActive(false);go.layer=LayerMask.NameToLayer("Player");
                    go.transform.position=i==0?new Vector2(-5,0):new Vector2(-2,-2);
                    var health=go.AddComponent<HealthComponent>();Set(health,"_config",hc);
                    targets[i]=go.AddComponent<DamageHitbox2D>();Set(targets[i],"_receiverComponent",health);
                    go.AddComponent<CircleCollider2D>().radius=.2f;go.SetActive(true);
                }
                hud.Bind(e.Boss,e.Ultimate.Curtain);
                require(e.Begin(true,7,null,targets,null,null),"Begin timed prelude");
                e.Simulate(e.Config.PreludeSeconds-.1f);require(takeovers==0 && !e.Boss.IsShown,"No early boss before configured prelude ends");
                e.Simulate(0);require(takeovers==0,"Prelude pause");
                e.Simulate(.11f);require(takeovers==1 && e.State==KimiEncounterState.Entering && !e.Boss.CanReceiveDamage,"Single timed takeover and protected entrance");
                e.Cancel();require(e.State==KimiEncounterState.Idle && !e.Boss.IsShown,"Cancel prelude/entry cleans boss");
                require(e.Begin(false,7,null,targets,null,null),"Direct challenge skips prelude");
                require(takeovers==2 && e.State==KimiEncounterState.Entering,"Direct entry requests same takeover");
                bool crossed=false,sawPhaseChange=false;int previousFinished=0,previousStarted=0;
                float clock=0,lastFinish=-100;
                for(int step=0;step<30000 && e.CastsFinished<20;step++)
                {
                    if(!crossed && e.State==KimiEncounterState.Casting && e.Boss.CanReceiveDamage)
                    {
                        var half=new DamagePacket(5000,e.Boss.transform.position,Vector2.right,null);
                        require(e.Boss.TryReceiveDamage(in half) && !e.Boss.PhaseTwo,"HalfHP does not interrupt active cast");crossed=true;
                    }
                    clock+=.02f;e.Simulate(.02f);Physics2D.Simulate(.02f);ClearFrozenEffects();
                    if(e.CastsStarted!=previousStarted)
                    {
                        if(previousStarted>0)require(clock-lastFinish>=(previousFinished%5==0?5:2)-.03f,"Normal/5-cast long gap");
                        previousStarted=e.CastsStarted;
                    }
                    if(e.CastsFinished!=previousFinished){lastFinish=clock;previousFinished=e.CastsFinished;}
                    if(crossed && e.CastsFinished==0)require(!e.Boss.PhaseTwo,"Stillphase1 until cast boundary");
                    if(e.State==KimiEncounterState.PhaseChange)
                    {
                        require(!e.Boss.CanReceiveDamage && e.Boss.PhaseTwo,"Phase transition protected");
                        if(!sawPhaseChange)Capture("encounter_phase_change",1280,582);
                        sawPhaseChange=true;
                    }
                    int active=(e.Moon.State==KimiMoonState.Warning || e.Moon.State==KimiMoonState.Flying || e.Moon.State==KimiMoonState.Gap?1:0)+
                        (e.Prism.Active?1:0)+(e.Laser.State==KimiLaserState.Charging || e.Laser.State==KimiLaserState.Firing || e.Laser.State==KimiLaserState.Recovery?1:0)+
                        (e.Ultimate.State!=KimiUltimateState.Idle && e.Ultimate.State!=KimiUltimateState.Complete?1:0);
                    require(active<=1,"Only one active skill");
                }
                require(e.CastsFinished==20 && sawPhaseChange && order.Distinct().Count()==4,"Twenty real casts cover all4 and phase transition");
                for(int i=1;i<order.Count;i++)require(order[i]!=order[i-1],"No consecutive repeated skill");
                for(int i=0;i<20;i+=5)require(order.Skip(i).Take(5).Count(s=>s==KimiSkill.Ultimate)<=1,"Ultimate max once per5");
                var kill=new DamagePacket(10000,e.Boss.transform.position,Vector2.right,null);
                require(e.Boss.TryReceiveDamage(in kill),"Defeat boss in gap");
                require(e.State==KimiEncounterState.Complete && completions==1,"Boss victory once");
                e.Simulate(10);require(completions==1,"No repeated victory");
                require(e.Moon.Projectiles.ActiveCount==0 && e.Ultimate.Blades.ActiveCount==0 && e.Ultimate.ReinforcementPool.ActiveCount==0 && !e.Prism.Active,"Victory cancels all hazards");
                var expected=order.ToArray();order.Clear();
                e.Begin(false,7,null,targets,null,null);e.Simulate(e.Config.EntrySeconds+.01f);
                require(e.CastsStarted==1 && e.CurrentSkill==expected[0] && !e.Boss.PhaseTwo && e.Boss.CurrentHealth==10000,"Restart resets phase/health/counters and seed");
                e.Cancel();
                Action cancelTakeover=()=>e.Cancel();e.TakeoverRequested+=cancelTakeover;
                e.Begin(false,11,null,targets,null,null);e.TakeoverRequested-=cancelTakeover;
                require(e.State==KimiEncounterState.Idle && !e.Boss.IsShown,"Cancellation during takeover cannot resurrect boss");
                string report="ENCOUNTER PASS "+checks+" checks. Configured prelude/direct entry, protected entrance,20 actual sequential casts covering4skills,2s gaps/5s after5casts, no consecutive repeat, max1ultimate per5, halfHP waits for skill boundary, phase transition, victory/cancel/reset. Sequence: "+string.Join(",",expected)+". This is isolated orchestration, NOT chapter timer/night/network integration.";
                File.WriteAllText(Evidence+"/encounter_verification.txt",report);return report;
            }
            finally
            {
                e.TakeoverRequested-=takeover;e.Completed-=complete;e.CastStarted-=cast;e.Cancel();hud.Bind(e.Boss);
                foreach(var target in targets)if(target!=null)UnityEngine.Object.DestroyImmediate(target.gameObject);
                UnityEngine.Object.DestroyImmediate(hc);Time.timeScale=oldScale;Physics2D.simulationMode=oldMode;
            }
        }

        public static string RunUltimate()
        {
            if(!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only")==null)
                throw new InvalidOperationException("必须在独立验收场Play执行。");
            var u=UnityEngine.Object.FindAnyObjectByType<KimiUltimatePattern2D>();var boss=u.Boss;
            var hud=UnityEngine.Object.FindAnyObjectByType<KimiBossHudView>();
            var targets=new DamageHitbox2D[2];var healths=new HealthComponent[2];
            var hc=ScriptableObject.CreateInstance<HealthConfig>();Set(hc,"_maximumHealth",100f);
            float oldScale=Time.timeScale;var oldMode=Physics2D.simulationMode;
            int checks=0,completions=0;Action completed=()=>completions++;
            Action<bool,string> require=(ok,message)=>{if(!ok)throw new Exception(message);checks++;};
            u.Completed+=completed;
            try
            {
                Time.timeScale=0;Physics2D.simulationMode=SimulationMode2D.Script;
                UnityEngine.Object.FindAnyObjectByType<KimiMoonBladePattern2D>().Cancel();
                UnityEngine.Object.FindAnyObjectByType<KimiPrismPattern2D>().Cancel();
                UnityEngine.Object.FindAnyObjectByType<KimiLaserPattern2D>().Cancel();
                for(int i=0;i<2;i++)
                {
                    var go=new GameObject("UltimateProbe_"+i);go.SetActive(false);go.layer=LayerMask.NameToLayer("Player");
                    go.transform.position=i==0?new Vector2(-5,0):new Vector2(-2,-2);
                    healths[i]=go.AddComponent<HealthComponent>();Set(healths[i],"_config",hc);
                    targets[i]=go.AddComponent<DamageHitbox2D>();Set(targets[i],"_receiverComponent",healths[i]);
                    var circle=go.AddComponent<CircleCollider2D>();circle.radius=.2f;circle.isTrigger=true;go.SetActive(true);
                }
                for(int phase=0;phase<2;phase++)
                {
                    boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);hud.Bind(boss,u.Curtain);
                    if(phase==1){var half=new DamagePacket(5000,boss.transform.position,Vector2.left,null);boss.TryReceiveDamage(in half);boss.CommitPhaseAtSkillBoundary();boss.GetComponent<SpriteHitFlash2D>().ResetFeedback();}
                    require(u.Begin(phase==1,null,targets,null),"Ultimate starts");
                    int maximum=phase==0?500:1000;
                    require(u.Curtain.Remaining==maximum && !boss.CanReceiveDamage,"Correct hit shield protects boss");
                    u.Simulate(.02f);Physics2D.Simulate(.02f);
                    require(u.ReinforcementPool.ActiveCount==1,"360 spawned via real pool");
                    var guard=u.ReinforcementPool.Instances.First(a=>a.gameObject.activeSelf);
                    require(Vector2.Distance(guard.transform.position, (Vector2)boss.transform.position+new Vector2(-3,.2f))<.1f && guard.Health.MaximumHealth==24,"360 spawns in front of Kimi with3x base health");
                    for(int i=0;i<350;i++){u.Simulate(.02f);Physics2D.Simulate(.02f);}
                    require(u.ReinforcementPool.ActiveCount<=10 && u.ReinforcementPool.ActiveCount>1,"Reinforcement stream bounded");
                    ClearFrozenEffects();Capture("ultimate_phase"+(phase+1)+"_charge",1280,582);
                    require(hud.ShieldPanel.activeSelf && hud.ShieldText.text==maximum+" / "+maximum,"Live uGUI shield counter");
                    var massive=new DamagePacket(9999,u.Curtain.transform.position,Vector2.right,null,DamageAttackIdAllocator.Next());
                    require(u.Curtain.TryReceiveDamage(in massive) && u.Curtain.Remaining==maximum-1,"High damage only costs one hit");
                    require(!u.Curtain.TryReceiveDamage(in massive) && u.Curtain.Remaining==maximum-1,"Duplicate nonzero attack ID rejected");
                    var zero=new DamagePacket(0,Vector2.zero,Vector2.right,null);
                    require(!u.Curtain.TryReceiveDamage(in zero),"Invalid attack rejected");
                    for(int i=1;i<maximum;i++)
                    {
                        var hit=new DamagePacket(1,u.Curtain.transform.position,Vector2.right,null);
                        require(u.Curtain.TryReceiveDamage(in hit),"Each legacy execution counts");
                    }
                    hud.RenderNow();require(!hud.ShieldPanel.activeSelf && u.State==KimiUltimateState.Stagger && boss.CanReceiveDamage,"Exact break interrupts and restores vulnerability");
                    require(!u.Reinforcements.IsRunning && u.FiredBlades==0,"Break stops reinforcements; no giant blades");
                    u.Curtain.GetComponent<SpriteHitFlash2D>().ResetFeedback();Capture("ultimate_phase"+(phase+1)+"_interrupted",1280,582);
                    u.Simulate(3.1f);require(u.State==KimiUltimateState.Complete && u.ReinforcementPool.ActiveCount==0,"Stagger completes and clears only owned adds");
                    u.BreakEffects.gameObject.SetActive(false);u.BreakEffects.gameObject.SetActive(true);

                    require(u.Begin(phase==1,null,targets,null),"Fresh second cast");
                    float before=u.ChargeRemaining;u.Simulate(0);require(before==u.ChargeRemaining,"Pause charge");
                    int maxActive=0;bool captured=false;
                    for(int step=0;step<1600 && u.State!=KimiUltimateState.Complete;step++)
                    {
                        u.Simulate(.02f);Physics2D.Simulate(.02f);
                        maxActive=Mathf.Max(maxActive,u.Blades.ActiveCount);
                        if(!captured && u.FiredVolleys==1 && u.Blades.ActiveCount==(phase==0?1:3))
                        {
                            foreach(var blade in u.Blades.Instances) if(blade.IsRented)
                            {
                                require(!blade.TryClear(),"HS cannot clear giant blade");
                                var visual=blade.GetComponentInChildren<SpriteRenderer>();
                                require(!visual.flipX && !visual.flipY && Vector2.Dot(-(Vector2)visual.transform.right,
                                    blade.GetComponent<Rigidbody2D>().linearVelocity.normalized)>.99f,"Generated left-facing blade faces velocity");
                            }
                            for(int j=0;j<25;j++){u.Simulate(.02f);Physics2D.Simulate(.02f);}
                            ClearFrozenEffects();Capture("ultimate_phase"+(phase+1)+"_release",1280,582);
                            if(phase==1)Capture("ultimate_phase2_release_desktop",1920,1080);captured=true;
                        }
                        // 手动固定步不推进表现池Update；每轮释放静态测试的冻结特效。
                        var hits=UnityEngine.Object.FindAnyObjectByType<KimiPrismPattern2D>().HitEffects;
                        hits.gameObject.SetActive(false);hits.gameObject.SetActive(true);
                    }
                    require(captured && u.FiredVolleys==5 && u.FiredBlades==(phase==0?5:15),"Five volleys, phase1=5 phase2=15");
                    require(u.State==KimiUltimateState.Complete && u.Blades.ActiveCount==0 && maxActive<=15,"Wait for waves to drain, bounded pool");
                    require(u.ReinforcementPool.ActiveCount==0 && !u.Curtain.CanReceiveDamage,"Final cleanup");
                }
                require(completions==4,"Completion exactly once for four full casts");
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                u.Begin(false,null,targets,null);u.Simulate(.02f);u.Cancel();u.Simulate(30);
                require(completions==4 && u.Blades.ActiveCount==0 && u.ReinforcementPool.ActiveCount==0 && boss.CanReceiveDamage,"Cancel restores boss without completion");
                u.Begin(false,null,targets,null);boss.ResetEncounter();u.Simulate(.02f);
                require(u.State==KimiUltimateState.Idle && !u.Curtain.Shape.enabled,"Boss reset cancels charge");
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                foreach(var h in healths)h.ResetToMaximum();targets[0].transform.position=Vector2.zero;targets[1].transform.position=new Vector2(0,-8);Physics2D.SyncTransforms();
                require(u.Blades.TryRent(new Vector2(4,0),Vector2.left,boss.gameObject,out var probe),"Rent real giant hit probe");
                for(int i=0;i<90;i++){u.Blades.Simulate(.02f);Physics2D.Simulate(.02f);}
                require(healths[0].CurrentHealth==98 && u.Blades.ActiveCount==0,"Giant blade actual trigger deals2 and returns");
                string report="ULTIMATE PASS "+checks+" checks.500/1000 hit shield,10/14s charge, exact interrupt/stagger, high damage counts once, duplicate IDs rejected, legacy independent hits accepted. Real Kimi-local360 spawn/24HP/cap10. Five volleys=5/15 distinct tidal blades,6u/s,2damage actual trigger, cannot be HS-cleared. Pause/cancel/reset/owned-pool cleanup and live uGUI counter tested. Independent Unity test only, not full player/AI/network/phone/balance acceptance.";
                File.WriteAllText(Evidence+"/ultimate_verification.txt",report);return report;
            }
            finally
            {
                u.Completed-=completed;u.Cancel();hud.Bind(boss);
                foreach(var target in targets)if(target!=null)UnityEngine.Object.DestroyImmediate(target.gameObject);
                UnityEngine.Object.DestroyImmediate(hc);Time.timeScale=oldScale;Physics2D.simulationMode=oldMode;
            }
        }

        private static void ClearFrozenEffects()
        {
            // 仅无存档独立场诊断调用。TimeScale=0时清掉跨模拟批次冻结的命中图。
            foreach(var pool in UnityEngine.Object.FindObjectsByType<DeepSleep.Runtime.Presentation.Effects.OneShotSpriteEffectPool2D>(FindObjectsInactive.Exclude))
            {pool.gameObject.SetActive(false);pool.gameObject.SetActive(true);}
        }

        public static string RunLaser()
        {
            if (!EditorApplication.isPlaying || GameObject.Find("Kimi_Verification_Only") == null)
                throw new InvalidOperationException("必须在独立验收场Play执行。");
            var laser = UnityEngine.Object.FindAnyObjectByType<KimiLaserPattern2D>();
            var productionLaserConfig = laser.Config;
            var geometryLaserConfig = UnityEngine.Object.Instantiate(productionLaserConfig);
            var boss = laser.Boss; var hud = UnityEngine.Object.FindAnyObjectByType<KimiBossHudView>();
            var healthConfig = ScriptableObject.CreateInstance<HealthConfig>(); Set(healthConfig,"_maximumHealth",100f);
            var probes = new GameObject[2]; var health = new HealthComponent[2];
            float oldScale = Time.timeScale; var oldMode = Physics2D.simulationMode;
            int checks = 0, completed = 0;
            Action onComplete = () => completed++;
            Action<bool,string> require = (ok,message) => { if (!ok) throw new Exception(message); checks++; };
            laser.Completed += onComplete;
            try
            {
                Time.timeScale = 0; Physics2D.simulationMode = SimulationMode2D.Script;
                UnityEngine.Object.FindAnyObjectByType<KimiPrismPattern2D>().Cancel();
                UnityEngine.Object.FindAnyObjectByType<KimiMoonBladePattern2D>().Cancel();
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null); hud.Bind(boss);
                for (int i=0;i<2;i++)
                {
                    var go = new GameObject("LaserProbe_"+i); probes[i] = go; go.SetActive(false);
                    go.layer = LayerMask.NameToLayer("Player"); health[i] = go.AddComponent<HealthComponent>();
                    Set(health[i],"_config",healthConfig);
                    var hitbox = go.AddComponent<DamageHitbox2D>(); Set(hitbox,"_receiverComponent",health[i]);
                    go.AddComponent<CircleCollider2D>().radius = .1f;
                    go.AddComponent<BoxCollider2D>().size = Vector2.one*.15f; go.SetActive(true);
                }
                Vector2 origin = laser.Muzzle.position;
                var sequenceTargets = probes.Select(p => p.GetComponent<DamageHitbox2D>()).ToArray();
                probes[0].transform.position = origin + new Vector2(-8,2);
                probes[1].transform.position = origin + new Vector2(-8,-2);
                int fired = 0, charges = 0;
                Action firedCount = () => fired++;
                Action chargeCount = () => charges++;
                laser.Fired += firedCount; laser.ChargeStarted += chargeCount;
                try
                {
                    require(laser.Config.ShotCount==5,"Production sequence has five rounds");
                    require(laser.Begin(probes[0].transform.position,null,sequenceTargets,0),"Sequence starts");
                    var lockedDirection = laser.Lane.Direction;
                    probes[0].transform.position += Vector3.up;
                    laser.Simulate(.1f);
                    require(laser.Lane.Direction==lockedDirection && laser.MarkerPosition==(Vector2)probes[0].transform.position && laser.TargetMarker.enabled,"Marker follows player while direction stays locked");
                    laser.Simulate(1.91f);
                    require(charges==2 && fired==1 && Vector2.Distance(laser.Lane.Direction,((Vector2)probes[1].transform.position-origin).normalized)<.001f,"Second round retargets other player");
                    Capture("laser_sequence_marker_mobile",1280,582);
                    laser.Simulate(8.1f);
                    require(fired==5 && charges==5 && completed==1 && laser.State==KimiLaserState.Complete,"Exactly five charges/fires and one completion across long step");
                    require(!laser.TargetMarker.enabled,"Sequence completion hides marker");
                    laser.Begin(probes[0].transform.position,null,sequenceTargets,0); laser.Cancel();
                    require(!laser.TargetMarker.enabled && laser.FiredShots==0,"Cancel clears marker/shot counter");
                }
                finally { laser.Fired-=firedCount; laser.ChargeStarted-=chargeCount; }
                completed=0; health[0].ResetToMaximum(); health[1].ResetToMaximum();
                // 下列原有几何/重复伤害测试隔离成单束，不修改生产资产的五轮设置。
                geometryLaserConfig.ShotCount=1; laser.Config=geometryLaserConfig;
                probes[0].transform.position = origin + Vector2.left*8;
                probes[1].transform.position = origin + Vector2.left*10 + Vector2.up*2;
                Physics2D.SyncTransforms();
                require(laser.Begin(probes[0].transform.position,null),"Laser begins");
                var locked = laser.Lane;
                probes[0].transform.position += Vector3.up*3; Physics2D.SyncTransforms();
                laser.Simulate(.9f);
                require(laser.Lane.Direction==locked.Direction && laser.Lane.Origin==locked.Origin,"Aim locked after target moves");
                require(health[0].CurrentHealth==100 && health[1].CurrentHealth==100,"No charging damage");
                require(laser.Warning.GetComponent<MeshRenderer>().enabled && !laser.Beam.GetComponent<MeshRenderer>().enabled,"Warning only");
                var warningMesh=laser.Warning.GetComponent<MeshFilter>().sharedMesh;
                require(Mathf.Abs(warningMesh.bounds.size.y-laser.Lane.Width)<.0001f,"Warning width equals damage width");
                Capture("laser_warning_mobile",1280,582);
                float time=laser.Elapsed;laser.Simulate(0);require(time==laser.Elapsed,"Pause");
                laser.Simulate(.31f);
                require(laser.State==KimiLaserState.Firing && health[0].CurrentHealth==100,"Moved target evades release");
                // 发射中进入仍可命中；双碰撞体与后续帧不能重复扣血。
                probes[0].transform.position=origin+Vector2.left*8;
                probes[1].transform.position=origin+Vector2.left*10;Physics2D.SyncTransforms();
                laser.Simulate(.02f);
                require(health[0].CurrentHealth==99 && health[1].CurrentHealth==99,"Both players hit once; duplicate collider dedup");
                laser.Simulate(.2f);
                require(health[0].CurrentHealth==99 && health[1].CurrentHealth==99,"No repeated damage during beam");
                var mesh=laser.Beam.GetComponent<MeshFilter>().sharedMesh;
                require(Mathf.Abs(mesh.bounds.size.x-laser.Lane.Length)<.0001f,"Mesh length equals snapshot");
                require((Vector2)laser.Beam.transform.position==laser.Lane.Origin,"Mesh shares origin");
                require(Mathf.Abs(Vector2.Dot(laser.Beam.transform.right,laser.Lane.Direction)-1)<.0001f,"Mesh shares locked direction");
                var texture=laser.Beam.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
                require(texture.wrapModeU==TextureWrapMode.Mirror && texture.wrapModeV==TextureWrapMode.Clamp,"Mirrored repeat uses identical edge texels at seams");
                require(Mathf.Abs((mesh.uv[1].x-mesh.uv[0].x)-laser.Lane.Length/laser.Config.TextureRepeatLength)<.0001f,"Repeated UV not whole-image stretch");
                Capture("laser_fire_mobile",1280,582);Capture("laser_fire_desktop",1920,1080);
                laser.Simulate(1);
                require(laser.State==KimiLaserState.Complete && completed==1,"Completes once");
                require(!laser.Focus.enabled && !laser.Beam.GetComponent<MeshRenderer>().enabled,"Completion cleans visuals");
                laser.Simulate(1);require(completed==1,"No duplicate completion");
                laser.Begin(origin+Vector2.left*8,null);laser.Simulate(2.1f);
                require(health[0].CurrentHealth==98 && completed==2,"Long step fires once; restart clears hit cache: HP="+health[0].CurrentHealth+", completed="+completed+", state="+laser.State);
                laser.Begin(origin+Vector2.left*8,null);laser.Cancel();laser.Simulate(2);
                require(health[0].CurrentHealth==98 && !laser.Focus.enabled,"Cancel cannot fire");
                laser.Begin(origin+Vector2.left*8,null);boss.ResetEncounter();laser.Simulate(.02f);
                require(laser.State==KimiLaserState.Idle && !laser.Warning.GetComponent<MeshRenderer>().enabled,"Boss reset cancels warning");
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                ulong previousId=0;
                DamagePacket accepted=default;
                health[0].DamageAccepted+=packet=>accepted=packet;
                foreach(float angle in new[]{0f,45f,90f,180f,225f})
                {
                    laser.HitEffects.gameObject.SetActive(false);laser.HitEffects.gameObject.SetActive(true);
                    Vector2 direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                    Vector2 normal=new Vector2(-direction.y,direction.x);
                    health[0].ResetToMaximum();health[1].ResetToMaximum();
                    probes[0].transform.position=origin+direction*5+normal*(laser.Config.DamageWidth*.5f-.05f);
                    probes[1].transform.position=origin+direction*7+normal*(laser.Config.DamageWidth*.5f+.2f);
                    Physics2D.SyncTransforms();laser.Begin(origin+direction*8,null);laser.Simulate(1.3f);
                    require(health[0].CurrentHealth==99 && health[1].CurrentHealth==100,"Rotated hit boundary "+angle);
                    require(accepted.AttackId!=0 && accepted.AttackId!=previousId && accepted.InterceptionPolicy==DamageInterceptionPolicy.Blockable,"Unique blockable attack "+angle);
                    previousId=accepted.AttackId;
                }
                laser.enabled=false;
                require(laser.State==KimiLaserState.Idle && !laser.Beam.GetComponent<MeshRenderer>().enabled,"Disable clears active beam");
                laser.enabled=true;
                laser.Begin(origin+Vector2.left*8,null);laser.Simulate(1.3f);
                var lethal=new DamagePacket(10000,boss.transform.position,Vector2.right,null);
                boss.TryReceiveDamage(in lethal);laser.Simulate(.02f);
                require(laser.State==KimiLaserState.Idle && !laser.Focus.enabled,"Death clears active beam");
                boss.BeginAuthority(new Vector2(6.5f,-.8f),null,null);
                laser.HitEffects.gameObject.SetActive(false);laser.HitEffects.gameObject.SetActive(true);
                probes[0].transform.position=new Vector2(-15,8);probes[1].transform.position=new Vector2(-15,-8);Physics2D.SyncTransforms();
                laser.Begin(new Vector2(-5,0),null);laser.Simulate(1.32f);
                Capture("laser_final_mobile",1280,582);Capture("laser_final_desktop",1920,1080);
                string report="LASER PASS "+checks+" checks: production five-round sequence, per-round retarget, tracking marker with locked direction, five charge/fire events and one completion. Isolated single-beam clone verifies warning/dodge/late-entry damage/both-player/duplicate-collider dedup, UV mirror repeat, pause/cancel/reset/long-step. Config1.2s charge/.45s fire/.35s recovery per round,1.2u hit width,1damage. Isolated Unity Play only; no full guard, AI or phone acceptance.";
                File.WriteAllText(Evidence+"/laser_verification.txt",report);return report;
            }
            finally
            {
                laser.Completed-=onComplete;laser.Cancel();
                laser.Config=productionLaserConfig; UnityEngine.Object.DestroyImmediate(geometryLaserConfig);
                foreach(var probe in probes) if(probe!=null) UnityEngine.Object.DestroyImmediate(probe);
                UnityEngine.Object.DestroyImmediate(healthConfig);Time.timeScale=oldScale;Physics2D.simulationMode=oldMode;
            }
        }

        private static void ResetEffects(KimiPrismPattern2D prism)
        {
            prism.BreakEffects.gameObject.SetActive(false);prism.BreakEffects.gameObject.SetActive(true);
            prism.HitEffects.gameObject.SetActive(false);prism.HitEffects.gameObject.SetActive(true);
        }

        public static void Capture(string name,int width,int height)
        {
            var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            var hud=UnityEngine.Object.FindAnyObjectByType<KimiBossHudView>();hud.RenderNow();
            // 固定步诊断期间 TimeScale=0，静态截图清掉冻结的旧姿态残影，不改生产残影参数。
            hud.Boss.PoseTransition.ResetTo(hud.Boss.Body.sprite);
            var canvas=hud.GetComponentInParent<Canvas>();
            var scaler=canvas.GetComponent<CanvasScaler>();
            bool scalerEnabled=scaler.enabled; scaler.enabled=false; canvas.scaleFactor=height/1080f;
            var safe=hud.GetComponent<DeepSleep.Runtime.UI.Common.SafeAreaRectFitter>(); bool safeEnabled=safe.enabled;safe.enabled=false;
            var rect=(RectTransform)hud.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            RenderTexture old=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.aspect=(float)width/height;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                Directory.CreateDirectory(Evidence);File.WriteAllBytes(Evidence+"/"+name+".png",texture.EncodeToPNG());
            }
            finally {camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(texture);scaler.enabled=scalerEnabled;safe.enabled=safeEnabled;}
        }

        private static void ScaleReference(string name,string sprite,Vector2 position,float scale)
        {
            var renderer=new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/"+sprite+".png");
            renderer.transform.position=position;renderer.transform.localScale=Vector3.one*scale;
            renderer.sortingLayerName="Gameplay";renderer.sortingOrder=20;
        }
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        {var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Set(UnityEngine.Object target,string field,float value)
        {var so=new SerializedObject(target);so.FindProperty(field).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
