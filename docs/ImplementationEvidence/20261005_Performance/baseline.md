# 2026-10-05 性能优化前基线（Editor）

本文件由实际诊断返回值保存。早期 maze/navigation/perception 输出中的 `0 bytes` 已作废：本机 Unity/Mono 的线程字节计数对已知 4096 字节分配也返回 0。不得据此声称零分配。网络最后一组已通过 GC.Alloc 次数校准，次数有效；字节数 unavailable。耗时是本机 Editor 的受控调用，不代表手机帧率。

## perf_maze_before1

```text
DeepSeek: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=32.31, net displacement=2.64, moving steps=573, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Evade
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.4091/3.2077/4.6459, managed bytes=0
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0662/0.1258/3.7572, managed bytes=0
Harness: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=45.01, net displacement=4.03, moving steps=749, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Navigate
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.4101/3.2383/4.6892, managed bytes=0
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0654/0.1281/1.1093, managed bytes=0
Diagnostic only: real Brain/motor and continuous encounter/physics; no attacks, player contact or damage. Unsafe intervals are geometric overlap observations, not actual battle hit counts. Zero overlaps alone do not prove combat/phone/network acceptance.
```

## perf_maze_before2

```text
DeepSeek: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=32.31, net displacement=2.64, moving steps=573, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Evade
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.4096/3.2102/4.7567, managed bytes=0
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0656/0.1312/0.6774, managed bytes=0
Harness: simulated 45.02s, unsafe swept intervals=0/2251, overlap episodes=0, distance=45.01, net displacement=4.03, moving steps=749, max expanded nodes=768, peak bubbles=110, observed sampled fall=37.213/37.213u over 2189 verified steps, HP unchanged=3, final plan=Navigate
TryGetCommand: warmed samples=2151, mean/p95/max ms=0.4101/3.2477/4.8014, managed bytes=0
Encounter+Physics2D: warmed samples=2151, mean/p95/max ms=0.0671/0.1341/0.8960, managed bytes=0
Diagnostic only: real Brain/motor and continuous encounter/physics; no attacks, player contact or damage. Unsafe intervals are geometric overlap observations, not actual battle hit counts. Zero overlaps alone do not prove combat/phone/network acceptance.
```

## perf_nav_before

```text
Companion navigation fixed-input benchmark v1
Unity=6000.6.0f1, platform=WindowsEditor, processor=13th Gen Intel(R) Core(TM) i7-13700H, debugBuild=True, stopwatchHz=10000000
warmup=8, measuredCycles=48, cell=0.36, expansionLimit=768
direct-cold: decisions=48, mean=0.0003ms, p50=0.0003ms, p95=0.0003ms, max=0.0013ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=0, lastExpandedStateSumPerCycle=0, route/wait/reached/stuck=1/0/0/0, digest=6A8E63843F1FFEEF
wall-cold-7: decisions=48, mean=0.2532ms, p50=0.2497ms, p95=0.2681ms, max=0.3040ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=247, lastExpandedStateSumPerCycle=247, route/wait/reached/stuck=1/0/0/0, digest=B01D3035C4068858
U-backtrack-cold-11: decisions=48, mean=0.2347ms, p50=0.2277ms, p95=0.2549ms, max=0.3558ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=103, lastExpandedStateSumPerCycle=103, route/wait/reached/stuck=1/0/0/0, digest=8EDB13D1C423A209
blocked-cold-15: decisions=48, mean=0.1813ms, p50=0.1747ms, p95=0.1877ms, max=0.2980ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=189, lastExpandedStateSumPerCycle=189, route/wait/reached/stuck=0/1/0/0, digest=99D4FEAE3E82E0D6
dense-cold-128: decisions=48, mean=0.2682ms, p50=0.2630ms, p95=0.2903ms, max=0.3176ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=215, lastExpandedStateSumPerCycle=215, route/wait/reached/stuck=1/0/0/0, digest=B828F96503C9DE80
dense-cold-256: decisions=48, mean=0.4046ms, p50=0.3988ms, p95=0.4384ms, max=0.4632ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=207, lastExpandedStateSumPerCycle=207, route/wait/reached/stuck=1/0/0/0, digest=F0887042CBA51A1B
cached-route-sequence: decisions=2304, mean=0.0003ms, p50=0.0003ms, p95=0.0004ms, max=0.0036ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=0, lastExpandedStateSumPerCycle=0, route/wait/reached/stuck=48/0/0/0, digest=D550CF3801CC0E65
blocked-wait-sequence: decisions=2304, mean=0.0603ms, p50=0.0015ms, p95=0.1800ms, max=0.9773ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=189, lastExpandedStateSumPerCycle=9072, route/wait/reached/stuck=0/48/0/0, digest=AE8004A43231C395
falling-wall-sequence: decisions=2304, mean=0.0637ms, p50=0.0016ms, p95=0.2347ms, max=0.3557ms, threadBytes=0, bytes/decision=0.00, GC collections=0/0/0, expandedMax=224, lastExpandedStateSumPerCycle=6483, route/wait/reached/stuck=48/0/0/4, digest=EB98973502C71882
behaviorDigest=DA204AB3282AEE38
Navigation continuous collision, rounded corner, incoming/receding/prediction split, full side detour, U-shaped backtrack, blocked wait, dynamic snapshot loop, overflow, reset and bounded expansion checks passed. Synthetic math only; real motor/maze/mobile acceptance remains separate.
PASS: deterministic full-path/result/expanded-node signatures and per-decision expansion bound. All inputs stay fixed across revisions; sequence cases retain navigation state but do not simulate a motor. Times/bytes exclude fixture construction, Reset, CopyPath, assertions and reports; collection counts span the measured loop. No forced GC. Editor microbenchmark only, not phone performance or full gameplay acceptance.
```

## perf_perception_before

```text
Editor/Mono synthetic CPU microbenchmark; NOT mobile/device or full-brain acceptance.
Unity=6000.6.0f1, fixedDelta=0.0200, decision=0.1250, prediction=0.4500
P95 is a batch mean percentile, not an individual frame spike. Allocations exclude native engine memory.
Hazards=0, projectiles=0:
  Registry.CopyVisible: median=0.02 us/call, P95=0.02 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=0.09 us/call, P95=0.10 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=0.19 us/call, P95=0.19 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=69.25 us/call, P95=70.50 us/call, allocated=0 B / 42 calls
Hazards=64, projectiles=0:
  Registry.CopyVisible: median=10.83 us/call, P95=11.20 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=12.03 us/call, P95=13.40 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=24.48 us/call, P95=25.21 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=2327.85 us/call, P95=2393.75 us/call, allocated=0 B / 42 calls
  Steering blocked forward corridor: median=2407.25 us/call, P95=2470.40 us/call, allocated=0 B / 42 calls
Hazards=128, projectiles=0:
  Registry.CopyVisible: median=21.12 us/call, P95=23.31 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=23.63 us/call, P95=25.69 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=47.05 us/call, P95=53.16 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=4609.20 us/call, P95=4701.00 us/call, allocated=0 B / 42 calls
  Steering blocked forward corridor: median=4256.80 us/call, P95=4494.60 us/call, allocated=0 B / 42 calls
Hazards=256, projectiles=0:
  Registry.CopyVisible: median=43.21 us/call, P95=45.95 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=47.59 us/call, P95=60.38 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=95.89 us/call, P95=105.41 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=9080.55 us/call, P95=9249.10 us/call, allocated=0 B / 42 calls
  Steering blocked forward corridor: median=8008.80 us/call, P95=8092.35 us/call, allocated=0 B / 42 calls
Hazards=0, observable projectile bodies=64:
  Sensor.Refresh native bodies: median=14.03 us/call, P95=15.98 us/call, allocated=0 B / 672 calls
  Sensor.Danger one evaluation: median=12.63 us/call, P95=13.60 us/call, allocated=0 B / 1344 calls
  Sensor.NearestThreat one scan: median=5.94 us/call, P95=6.51 us/call, allocated=0 B / 1344 calls
  Steering with 64 native threats: median=295.95 us/call, P95=309.75 us/call, allocated=0 B / 42 calls
Checksum sink=0.0000; rerun 3 times after import/compilation has settled.

```

## perf_perception_before2

```text
Editor/Mono synthetic CPU microbenchmark; NOT mobile/device or full-brain acceptance.
Unity=6000.6.0f1, fixedDelta=0.0200, decision=0.1250, prediction=0.4500
P95 is a batch mean percentile, not an individual frame spike. Allocations exclude native engine memory.
Hazards=0, projectiles=0:
  Registry.CopyVisible: median=0.02 us/call, P95=0.02 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=0.10 us/call, P95=0.10 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=0.19 us/call, P95=0.19 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=67.25 us/call, P95=73.85 us/call, allocated=0 B / 42 calls
Hazards=64, projectiles=0:
  Registry.CopyVisible: median=10.63 us/call, P95=11.47 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=12.10 us/call, P95=14.24 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=24.46 us/call, P95=24.85 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=2317.45 us/call, P95=2381.25 us/call, allocated=0 B / 42 calls
  Steering blocked forward corridor: median=2394.15 us/call, P95=2489.85 us/call, allocated=0 B / 42 calls
Hazards=128, projectiles=0:
  Registry.CopyVisible: median=22.00 us/call, P95=23.58 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=23.72 us/call, P95=25.17 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=47.74 us/call, P95=52.50 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=4579.05 us/call, P95=4642.20 us/call, allocated=0 B / 42 calls
  Steering blocked forward corridor: median=4240.40 us/call, P95=4362.15 us/call, allocated=0 B / 42 calls
Hazards=256, projectiles=0:
  Registry.CopyVisible: median=42.90 us/call, P95=48.13 us/call, allocated=0 B / 1344 calls
  Sensor.Refresh one brain: median=47.53 us/call, P95=53.98 us/call, allocated=0 B / 672 calls
  Sensor.Refresh two brains: median=96.04 us/call, P95=131.35 us/call, allocated=0 B / 336 calls
  Steering open local corridor: median=9065.35 us/call, P95=9266.85 us/call, allocated=0 B / 42 calls
  Steering blocked forward corridor: median=7913.35 us/call, P95=8035.10 us/call, allocated=0 B / 42 calls
Hazards=0, observable projectile bodies=64:
  Sensor.Refresh native bodies: median=14.18 us/call, P95=15.18 us/call, allocated=0 B / 672 calls
  Sensor.Danger one evaluation: median=12.76 us/call, P95=13.46 us/call, allocated=0 B / 1344 calls
  Sensor.NearestThreat one scan: median=6.02 us/call, P95=6.23 us/call, allocated=0 B / 1344 calls
  Steering with 64 native threats: median=295.85 us/call, P95=306.15 us/call, allocated=0 B / 42 calls
Checksum sink=0.0000; rerun 3 times after import/compilation has settled.

```

## perf_net_calibrated_before

```text
Isolated networking hot-path benchmark
GC thread-counter calibration: retained new byte[4096], observed=0 B; UNAVAILABLE (zero is not a zero-allocation result)
GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
iterations=256, warmup=32; per operation; CPU and allocation-count runs measured separately; no real socket, live scene, Physics or rendered frame.
Send feedback (30 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=15.00, meanUs=1.44, p95Us=2.30, maxUs=8.00, gen0=0; packets/op=1; wireSHA256=CD825C3DAE13BFA0
World.Publish x64 (one renderer/entity): bytes/op=UNAVAILABLE, gcAllocs/op=1600.00, meanUs=308.99, p95Us=657.70, maxUs=8572.90, gen0=3; packets/op=64; wireSHA256=039F044342BDC52C
Doubao.WriteSnapshot x240 (5785 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=981.00, meanUs=201.33, p95Us=176.60, maxUs=5477.80, gen0=2; packets/op=1; wireSHA256=4A70ADCFA0B5F403
Receive feedback + 8 observers: bytes/op=UNAVAILABLE, gcAllocs/op=8.00, meanUs=1.63, p95Us=4.30, maxUs=16.90, gen0=0
Receive + Doubao.Read x240 + 7 observers: bytes/op=UNAVAILABLE, gcAllocs/op=250.00, meanUs=253.54, p95Us=318.30, maxUs=730.90, gen0=0
Wire checks: pre/post byte-for-byte + reliability equality; fingerprints below are comparable across code revisions.
Includes real catalog preflight. No reflection, packet capture, hashing, test resets or report formatting inside timed/allocated scope.
CPU pass has no active allocation recorder. Allocation-count pass uses Begin/End around only the operation; bytes remain UNAVAILABLE if the thread API fails calibration.

PASS: 8 session buffer semantics: retained packet ownership/reliability, nested send, writer exception recovery, subscription snapshot order, nested receive, handler isolation, malformed recovery, direction separation. Temporary objects, no socket.
```

