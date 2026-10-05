# 2026-10-05 优化后结果（Editor）

以下为诊断实际返回值。CPU-only 与 GC 计数分开；跳过项原样保留。不是设备实战验收。

## perf_nav_final

```text
Navigation collision old/new reference PASS: 101620 fixed-seed/edge cases, including horizon split, tangent/corner offsets, negative/zero/tiny duration and high speed.
far-short-step: old p50=115.26 ns/call, new p50=87.70 ns/call, ratio=0.761, calls/implementation=98304, warmupBatches=4, hitChecksum=0
mixed-short-step: old p50=199.54 ns/call, new p50=177.66 ns/call, ratio=0.890, calls/implementation=98304, warmupBatches=4, hitChecksum=1036
split-horizon: old p50=232.62 ns/call, new p50=228.59 ns/call, ratio=0.983, calls/implementation=98304, warmupBatches=4, hitChecksum=733
Same-process Editor microbench; alternating old/new measurement order, fixed inputs, prewarmed. No scene objects, assets, damage or motor state changed; no GC-byte or phone-performance claim.
Companion navigation fixed-input benchmark v1
Unity=6000.6.0f1, platform=WindowsEditor, processor=13th Gen Intel(R) Core(TM) i7-13700H, debugBuild=True, stopwatchHz=10000000
warmup=8, measuredCycles=48, cell=0.36, expansionLimit=768
GC thread-counter calibration: retained new byte[4096], observed=0 B; UNAVAILABLE (zero is not a zero-allocation result)
direct-cold: decisions=48, mean=0.0003ms, p50=0.0003ms, p95=0.0003ms, max=0.0014ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=0, lastExpandedStateSumPerCycle=0, route/wait/reached/stuck=1/0/0/0, digest=6A8E63843F1FFEEF
wall-cold-7: decisions=48, mean=0.2320ms, p50=0.2055ms, p95=0.3592ms, max=0.5079ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=247, lastExpandedStateSumPerCycle=247, route/wait/reached/stuck=1/0/0/0, digest=B01D3035C4068858
U-backtrack-cold-11: decisions=48, mean=0.1894ms, p50=0.1889ms, p95=0.1926ms, max=0.1945ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=103, lastExpandedStateSumPerCycle=103, route/wait/reached/stuck=1/0/0/0, digest=8EDB13D1C423A209
blocked-cold-15: decisions=48, mean=0.1624ms, p50=0.1406ms, p95=0.3001ms, max=0.4343ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=189, lastExpandedStateSumPerCycle=189, route/wait/reached/stuck=0/1/0/0, digest=99D4FEAE3E82E0D6
dense-cold-128: decisions=48, mean=0.2112ms, p50=0.2083ms, p95=0.2416ms, max=0.2445ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=215, lastExpandedStateSumPerCycle=215, route/wait/reached/stuck=1/0/0/0, digest=B828F96503C9DE80
dense-cold-256: decisions=48, mean=0.3267ms, p50=0.3216ms, p95=0.3562ms, max=0.4401ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=207, lastExpandedStateSumPerCycle=207, route/wait/reached/stuck=1/0/0/0, digest=F0887042CBA51A1B
cached-route-sequence: decisions=2304, mean=0.0004ms, p50=0.0003ms, p95=0.0005ms, max=0.0369ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=0, lastExpandedStateSumPerCycle=0, route/wait/reached/stuck=48/0/0/0, digest=D550CF3801CC0E65
blocked-wait-sequence: decisions=2304, mean=0.0478ms, p50=0.0012ms, p95=0.1402ms, max=0.8078ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=189, lastExpandedStateSumPerCycle=9072, route/wait/reached/stuck=0/48/0/0, digest=AE8004A43231C395
falling-wall-sequence: decisions=2304, mean=0.0521ms, p50=0.0012ms, p95=0.1877ms, max=0.5347ms, threadBytes=UNAVAILABLE, bytes/decision=UNAVAILABLE, GC collections=0/0/0, expandedMax=224, lastExpandedStateSumPerCycle=6483, route/wait/reached/stuck=48/0/0/4, digest=EB98973502C71882
behaviorDigest=DA204AB3282AEE38
Navigation continuous collision, rounded corner, incoming/receding/prediction split, full side detour, U-shaped backtrack, blocked wait, dynamic snapshot loop, overflow, reset and bounded expansion checks passed. Synthetic math only; real motor/maze/mobile acceptance remains separate.
PASS: deterministic full-path/result/expanded-node signatures and per-decision expansion bound. All inputs stay fixed across revisions; sequence cases retain navigation state but do not simulate a motor. Times/bytes exclude fixture construction, Reset, CopyPath, assertions and reports; collection counts span the measured loop. No forced GC. Editor microbenchmark only, not phone performance or full gameplay acceptance.
Navigation candidate bit-scan PASS: 4625 masks, 66290 set bits; all 32 indices, all two-bit pairs and fixed-seed dense/sparse masks preserve ascending enumeration.
```

## perf_maze_final1

```text
DeepSeek: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=32.31, net displacement=2.64, moving steps=573, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Evade
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.2981/2.3300/3.2844
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0651/0.1274/1.1943
Harness: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=45.01, net displacement=4.03, moving steps=749, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Navigate
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.2974/2.3278/3.4895
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0657/0.1339/0.8186
Diagnostic only: real Brain/motor and continuous encounter/physics; no attacks, player contact or damage. Unsafe intervals are geometric overlap observations, not actual battle hit counts. Zero overlaps alone do not prove combat/phone/network acceptance.
```

## perf_maze_final2

```text
DeepSeek: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=32.31, net displacement=2.64, moving steps=573, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Evade
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.2957/2.3354/3.5573
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0622/0.1181/0.7348
Harness: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=45.01, net displacement=4.03, moving steps=749, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Navigate
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.2951/2.3265/3.5912
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0621/0.1230/0.8803
Diagnostic only: real Brain/motor and continuous encounter/physics; no attacks, player contact or damage. Unsafe intervals are geometric overlap observations, not actual battle hit counts. Zero overlaps alone do not prove combat/phone/network acceptance.
```

## perf_maze_alloc_final

```text
GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
GC.Alloc count run: active recorder adds overhead; do not compare these timings to the CPU-only pass. Counts are NOT bytes.
DeepSeek: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=32.31, net displacement=2.64, moving steps=573, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Evade
Warmed current-thread GC.Alloc events: command=0, encounter+physics=528 over 2151 calls each; byte sizes not measured.
Harness: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=45.01, net displacement=4.03, moving steps=749, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Navigate
Warmed current-thread GC.Alloc events: command=0, encounter+physics=528 over 2151 calls each; byte sizes not measured.
Diagnostic only: real Brain/motor and continuous encounter/physics; no attacks, player contact or damage. Unsafe intervals are geometric overlap observations, not actual battle hit counts. Zero overlaps alone do not prove combat/phone/network acceptance.
```

## perf_perception_final

```text
Editor/Mono synthetic CPU microbenchmark; NOT mobile/device or full-brain acceptance.
Unity=6000.6.0f1, fixedDelta=0.0200, decision=0.1250, prediction=0.4500
P95 is a batch mean percentile, not an individual frame spike. Allocations exclude native engine memory.
Thread byte counter retained-4096 calibration: UNAVAILABLE, observed=0 B.
GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
Hazards=0, projectiles=0:
  Registry.CopyVisible: median=0.02 us/call, P95=0.02 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 1344 calls
  Sensor.Refresh one brain: median=0.10 us/call, P95=0.10 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 672 calls
  Sensor.Refresh two brains: median=0.19 us/call, P95=0.21 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 336 calls
  Steering open local corridor: median=67.95 us/call, P95=75.15 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
Hazards=64, projectiles=0:
  Registry.CopyVisible: median=12.26 us/call, P95=21.25 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 1344 calls
  Sensor.Refresh one brain: median=12.10 us/call, P95=13.88 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 672 calls
  Sensor.Refresh two brains: median=24.71 us/call, P95=25.14 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 336 calls
  Steering open local corridor: median=1755.05 us/call, P95=1822.15 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
  Steering blocked forward corridor: median=1793.65 us/call, P95=1967.30 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
Hazards=128, projectiles=0:
  Registry.CopyVisible: median=24.25 us/call, P95=31.23 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 1344 calls
  Sensor.Refresh one brain: median=24.97 us/call, P95=26.15 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 672 calls
  Sensor.Refresh two brains: median=49.66 us/call, P95=62.33 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 336 calls
  Steering open local corridor: median=3378.45 us/call, P95=3451.85 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
  Steering blocked forward corridor: median=3148.80 us/call, P95=3198.55 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
Hazards=256, projectiles=0:
  Registry.CopyVisible: median=45.36 us/call, P95=49.11 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 1344 calls
  Sensor.Refresh one brain: median=48.31 us/call, P95=52.63 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 672 calls
  Sensor.Refresh two brains: median=99.94 us/call, P95=103.95 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 336 calls
  Steering open local corridor: median=6672.85 us/call, P95=6736.15 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
  Steering blocked forward corridor: median=5869.85 us/call, P95=5932.75 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
Hazards=0, observable projectile bodies=64:
  Sensor.Refresh native bodies: median=14.47 us/call, P95=14.75 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 672 calls
  Sensor.Danger one evaluation: median=10.84 us/call, P95=11.23 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 1344 calls
  Sensor.NearestThreat one scan: median=6.08 us/call, P95=6.32 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 1344 calls
  Steering with 64 native threats: median=267.40 us/call, P95=274.30 us/call, bytes unavailable (thread counter failed retained-4096 calibration), GC.Alloc=0 events / 42 calls
Checksum sink=0.0000; rerun 3 times after import/compilation has settled.

Steering exact component comparison: 512 deterministic cases, 0 mismatches. Seed=20261005. Reference uses current collision geometry but original candidate/control algorithm.

```

## perf_net_final1

```text
PASS: 8 session buffer semantics: retained packet ownership/reliability, nested send, writer exception recovery, subscription snapshot order, nested receive, handler isolation, malformed recovery, direction separation. Temporary objects, no socket.
Isolated networking hot-path benchmark
GC thread-counter calibration: retained new byte[4096], observed=0 B; UNAVAILABLE (zero is not a zero-allocation result)
GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
iterations=256, warmup=32; per operation; CPU and allocation-count runs measured separately; no real socket, live scene, Physics or rendered frame.
Send feedback (30 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=1.00, meanUs=0.34, p95Us=0.50, maxUs=3.80, gen0=0; packets/op=1; wireSHA256=CD825C3DAE13BFA0
World.Publish x64 (one renderer/entity): bytes/op=UNAVAILABLE, gcAllocs/op=64.00, meanUs=76.25, p95Us=95.90, maxUs=343.00, gen0=0; packets/op=64; wireSHA256=039F044342BDC52C
Doubao.WriteSnapshot x240 (5785 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=3.00, meanUs=61.51, p95Us=92.20, maxUs=232.20, gen0=0; packets/op=1; wireSHA256=4A70ADCFA0B5F403
Receive feedback + 8 observers: bytes/op=UNAVAILABLE, gcAllocs/op=7.00, meanUs=1.25, p95Us=2.00, maxUs=8.40, gen0=0
Receive + Doubao.Read x240 + 7 observers: bytes/op=UNAVAILABLE, gcAllocs/op=249.00, meanUs=278.84, p95Us=471.70, maxUs=1029.60, gen0=0
Wire checks: pre/post byte-for-byte + reliability equality; fingerprints below are comparable across code revisions.
Includes real catalog preflight. No reflection, packet capture, hashing, test resets or report formatting inside timed/allocated scope.
CPU pass has no active allocation recorder. Allocation-count pass uses Begin/End around only the operation; bytes remain UNAVAILABLE if the thread API fails calibration.

PASS: 23 directional catalog entries, 831 protocol assertions; complete symbols, no numeric send IDs, sizes/directions/truncation/trailing bytes, real beam codec, dense Doubao payload. Protocol=3
```

## perf_ai_world_regression

```text
PASS: 27 read-only portal-goal cases. Both human/AI role pairings, physical intent, full body entry, hold, cancel/re-ready/re-enter, control reset, both-AI/shared goal, no auto-ready, authority and node-state gates. Uses temporary role/node/pose state; does not prove movement simulation.
PASS: real brain + Motor.Simulate + numeric position integration (15s limit; >=1s stopped whole-body hold). solo Harness left Harness entry=1.04s, stable=1.02s; solo Harness right Harness entry=1.04s, stable=1.02s; solo DeepSeek left DeepSeek entry=1.04s, stable=1.02s; solo DeepSeek right DeepSeek entry=1.04s, stable=1.02s; disconnected guest Harness Harness entry=1.04s, stable=1.02s; disconnected guest DeepSeek DeepSeek entry=1.04s, stable=1.02s; voluntary host Harness Harness entry=1.04s, stable=1.02s; voluntary host DeepSeek DeepSeek entry=1.04s, stable=1.02s; both online AI DeepSeek entry=1.04s, stable=1.02s/Harness entry=1.04s, stable=1.02s; solo voluntary case SKIPPED: current launch is not a completed solo selection; combat dual-AI host=DeepSeek 20s anchorError=0.0000u, settledRadius=1.053u, last5sTravel=0.000u; combat dual-AI host=Harness 20s anchorError=0.0000u, settledRadius=1.053u, last5sTravel=0.000u. Human exit/cancel removed portal plans on the next command. No Physics2D.Simulate, combat execution, node Update or auto-ready; threats isolated. Temporary state restored.
AI_Harness: extent=(0.195, 0.450), offset=(0.260, -0.040), speed=6.5, acceleration=40, deceleration=50, fixed=0.020, decision=0.140
  vertical-wall PASS: contacts=0, reached=True, time=2.16s, minGoalDistance=0.092, final=(2.957, -0.081), goal=(3.000, 0.000), waits=1, stuck=0, backtracked=True, expandedMax=244, decisionAvg=0.271ms, max=0.587ms, decisions=16
  U-backtrack PASS: contacts=0, reached=True, time=2.42s, minGoalDistance=0.089, final=(0.015, 3.712), goal=(0.000, 3.800), waits=1, stuck=0, backtracked=True, expandedMax=78, decisionAvg=0.314ms, max=0.478ms, decisions=18
  falling-wall PASS: contacts=0, reached=True, time=1.84s, minGoalDistance=0.049, final=(2.976, 0.957), goal=(3.000, 1.000), waits=1, stuck=0, backtracked=True, expandedMax=169, decisionAvg=0.273ms, max=0.414ms, decisions=14
  live-maze-snapshot SKIPPED: no active bubbles or registry truncated; start World01 maze first.
AI_DeepSeek: extent=(0.195, 0.450), offset=(0.260, -0.040), speed=6.5, acceleration=40, deceleration=50, fixed=0.020, decision=0.140
  vertical-wall PASS: contacts=0, reached=True, time=2.16s, minGoalDistance=0.092, final=(2.957, -0.081), goal=(3.000, 0.000), waits=1, stuck=0, backtracked=True, expandedMax=244, decisionAvg=0.259ms, max=0.508ms, decisions=16
  U-backtrack PASS: contacts=0, reached=True, time=2.42s, minGoalDistance=0.089, final=(0.015, 3.712), goal=(0.000, 3.800), waits=1, stuck=0, backtracked=True, expandedMax=78, decisionAvg=0.314ms, max=0.510ms, decisions=18
  falling-wall PASS: contacts=0, reached=True, time=1.84s, minGoalDistance=0.049, final=(2.976, 0.957), goal=(3.000, 1.000), waits=1, stuck=0, backtracked=True, expandedMax=169, decisionAvg=0.273ms, max=0.459ms, decisions=14
  live-maze-snapshot SKIPPED: no active bubbles or registry truncated; start World01 maze first.
TOTAL: 6 cases, 0 failed. Contacts are continuous actual AABB/circle entries without padding; decision timings include Navigate + Steering, excluding snapshot copy. No attacks, invulnerability, physics contacts, network or new bubble spawns simulated.

PASS: 22 obstacle registry checks; world geometry, pooling, disabled states, buffer truncation and offline/authority/client gates. Isolated fixtures, no gameplay simulation.
```

## perf_world_network_regression

```text
PASS: 6 real writer packets (player pair, weapon state, 2 enemy pools, rice, enemy bullet) pass Session.Send preflight and catalog. Temporary inactive channels only; live gameplay state unchanged, no network connection.
PASS: 21 isolated real Session.Receive + node/Doubao checks; ID 40/47 separation, frame low bytes 0..4, malformed lengths/enums/bools/NaN/duplicate and zero IDs/capacity, no partial boss/node/sequence mutation. No live network or scene state used.
PASS: 15 checks. Both host roles, local feedback, reliable delivery through Session.Receive, client gate, DS/HS damage numbers, melee impacts, duplicate/stale rejection, reconnect, client-to-host and offline restore.
PASS: 20 AI routing/feedback cases. Host and guest DS/HS, human -> AI -> human repeated, real SetLocalAi/control packet/Receive/BindSources, local and replicated laser impact + damage number.
PASS: 25 player hit network checks. Both roles, reliable fact routing, no client local echo, AI, duplicate/stale/wrap, reconnect, malformed/truncated/trailing bytes, sender/phase, HP isolation, offline. Source DamageAccepted is injected; not a real dual-device test.
PASS: 40 real local-hit checks (DS/HS × both local roles). Actual non-lethal HP loss, exactly one presenter, HUD/overlay/protection, own-role-only camera, duplicate invulnerability, revive protection, absorbed/zero damage, immediate reset. Nonzero shake preference verified. HP, damage-gate, role/commands, presentation, camera and time state restored; lethal/downed transitions not exercised.
9 presentation lifetime assertions passed: birth frame, next frame, duplicate update, pause, negative dt, resume, replay. Play birth-frame checks: 1 HS pools, 1 number pools passed; twice-immediate LateUpdate, idle state and pool order restored. Actual frame dt=0.0000; long-dt coverage comes from the injected pure lifetime test.
```

## perf_prototype_regression

```text
PASS: 6 real writer packets (player pair, weapon state, 2 enemy pools, rice, enemy bullet) pass Session.Send preflight and catalog. Temporary inactive channels only; live gameplay state unchanged, no network connection.
PASS: 15 checks. Both host roles, local feedback, reliable delivery through Session.Receive, client gate, DS/HS damage numbers, melee impacts, duplicate/stale rejection, reconnect, client-to-host and offline restore.
PASS: 20 AI routing/feedback cases. Host and guest DS/HS, human -> AI -> human repeated, real SetLocalAi/control packet/Receive/BindSources, local and replicated laser impact + damage number.
PASS: 25 player hit network checks. Both roles, reliable fact routing, no client local echo, AI, duplicate/stale/wrap, reconnect, malformed/truncated/trailing bytes, sender/phase, HP isolation, offline. Source DamageAccepted is injected; not a real dual-device test.
PASS: 40 real local-hit checks (DS/HS × both local roles). Actual non-lethal HP loss, exactly one presenter, HUD/overlay/protection, own-role-only camera, duplicate invulnerability, revive protection, absorbed/zero damage, immediate reset. Nonzero shake preference verified. HP, damage-gate, role/commands, presentation, camera and time state restored; lethal/downed transitions not exercised.
PASS: 27 read-only portal-goal cases. Both human/AI role pairings, physical intent, full body entry, hold, cancel/re-ready/re-enter, control reset, both-AI/shared goal, no auto-ready, authority and node-state gates. Uses temporary role/node/pose state; does not prove movement simulation.
PASS: real brain + Motor.Simulate + numeric position integration (15s limit; >=1s stopped whole-body hold). solo Harness left Harness entry=1.04s, stable=1.02s; solo Harness right Harness entry=1.04s, stable=1.02s; solo DeepSeek left DeepSeek entry=1.04s, stable=1.02s; solo DeepSeek right DeepSeek entry=1.04s, stable=1.02s; disconnected guest Harness Harness entry=1.04s, stable=1.02s; disconnected guest DeepSeek DeepSeek entry=1.04s, stable=1.02s; voluntary host Harness Harness entry=1.04s, stable=1.02s; voluntary host DeepSeek DeepSeek entry=1.04s, stable=1.02s; both online AI DeepSeek entry=1.04s, stable=1.02s/Harness entry=1.04s, stable=1.02s; solo voluntary case SKIPPED: current launch is not a completed solo selection. Human exit/cancel removed portal plans on the next command. No Physics2D.Simulate, combat execution, node Update or auto-ready; threats isolated. Temporary state restored.
AI_DeepSeek: extent=(0.195, 0.450), offset=(0.260, -0.040), speed=6.5, acceleration=40, deceleration=50, fixed=0.020, decision=0.140
  vertical-wall PASS: contacts=0, reached=True, time=2.16s, minGoalDistance=0.092, final=(2.957, -0.081), goal=(3.000, 0.000), waits=1, stuck=0, backtracked=True, expandedMax=244, decisionAvg=0.384ms, max=2.243ms, decisions=16
  U-backtrack PASS: contacts=0, reached=True, time=2.42s, minGoalDistance=0.089, final=(0.015, 3.712), goal=(0.000, 3.800), waits=1, stuck=0, backtracked=True, expandedMax=78, decisionAvg=0.322ms, max=0.504ms, decisions=18
  falling-wall PASS: contacts=0, reached=True, time=1.84s, minGoalDistance=0.049, final=(2.976, 0.957), goal=(3.000, 1.000), waits=1, stuck=0, backtracked=True, expandedMax=169, decisionAvg=0.269ms, max=0.424ms, decisions=14
  live-maze-snapshot SKIPPED: no active bubbles or registry truncated; start World01 maze first.
AI_Harness: extent=(0.195, 0.450), offset=(0.260, -0.040), speed=6.5, acceleration=40, deceleration=50, fixed=0.020, decision=0.140
  vertical-wall PASS: contacts=0, reached=True, time=2.16s, minGoalDistance=0.092, final=(2.957, -0.081), goal=(3.000, 0.000), waits=1, stuck=0, backtracked=True, expandedMax=244, decisionAvg=0.256ms, max=0.472ms, decisions=16
  U-backtrack PASS: contacts=0, reached=True, time=2.42s, minGoalDistance=0.089, final=(0.015, 3.712), goal=(0.000, 3.800), waits=1, stuck=0, backtracked=True, expandedMax=78, decisionAvg=0.322ms, max=0.474ms, decisions=18
  falling-wall PASS: contacts=0, reached=True, time=1.84s, minGoalDistance=0.049, final=(2.976, 0.957), goal=(3.000, 1.000), waits=1, stuck=0, backtracked=True, expandedMax=169, decisionAvg=0.271ms, max=0.406ms, decisions=14
  live-maze-snapshot SKIPPED: no active bubbles or registry truncated; start World01 maze first.
TOTAL: 6 cases, 0 failed. Contacts are continuous actual AABB/circle entries without padding; decision timings include Navigate + Steering, excluding snapshot copy. No attacks, invulnerability, physics contacts, network or new bubble spawns simulated.

```

## perf_foundation_audit

```text
Assets/Scenes/Gameplay_Prototype.unity: 881 checks, 0 errors, 0 warnings.
Read-only assembly audit; does not establish gameplay, network or device correctness.
Assets/Scenes/World01_EarlyInternet.unity: 915 checks, 0 errors, 0 warnings.
Read-only assembly audit; does not establish gameplay, network or device correctness.
PASS: 23 directional catalog entries, 831 protocol assertions; complete symbols, no numeric send IDs, sizes/directions/truncation/trailing bytes, real beam codec, dense Doubao payload. Protocol=3
```

