# 2026-10-05 最终确认

## perf_scene_lifecycle

```text
PASSED 16 lifecycle stations; local host/connecting only, not dual-device or relay acceptance.
1. Initial menu cleanup: session/manager/registered=0/0/0
2. World solo selection: session/manager/registered=1/1/1
3. Selection back button: session/manager/registered=0/0/0
4. World solo re-entry: session/manager/registered=1/1/1
5. Chapter return after solo selection: session/manager/registered=0/0/0
6. World before direct switch: session/manager/registered=1/1/1
7. Direct StartLevel World -> Prototype: session/manager/registered=1/1/1
8. Prototype menu return: session/manager/registered=0/0/0
9. Online selection: session/manager/registered=1/1/1
10. Start local DS host: session/manager/registered=1/1/1
11. Online menu Leave: session/manager/registered=0/0/0
12. Online re-entry after shutdown: session/manager/registered=1/1/1
13. Rebind same port as HS host: session/manager/registered=1/1/1
14. Host direct Router return: session/manager/registered=0/0/0
15. Client selection: session/manager/registered=1/1/1
16. Exit while loopback client is connecting: session/manager/registered=0/0/0

```

## perf_final_confirmation

```text
PASS: 4113 IEEE float bit patterns + mixed strings/integers/bools exactly match BinaryWriter; positive/negative zero, subnormal, finite limits, infinities, NaN payloads, fixed xorshift corpus (runtime may quiet signaling NaN before Write). Preallocated override float write=0 GC.Alloc events. GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
Isolated networking hot-path benchmark
GC thread-counter calibration: retained new byte[4096], observed=0 B; UNAVAILABLE (zero is not a zero-allocation result)
GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
iterations=256, warmup=32; per operation; CPU and allocation-count runs measured separately; no real socket, live scene, Physics or rendered frame.
Send feedback (30 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=1.00, meanUs=0.28, p95Us=0.30, maxUs=1.60, gen0=0; packets/op=1; wireSHA256=CD825C3DAE13BFA0
World.Publish x64 (one renderer/entity): bytes/op=UNAVAILABLE, gcAllocs/op=64.00, meanUs=99.41, p95Us=136.70, maxUs=3292.60, gen0=0; packets/op=64; wireSHA256=039F044342BDC52C
Doubao.WriteSnapshot x240 (5785 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=3.00, meanUs=59.34, p95Us=71.50, maxUs=151.40, gen0=0; packets/op=1; wireSHA256=4A70ADCFA0B5F403
Receive feedback + 8 observers: bytes/op=UNAVAILABLE, gcAllocs/op=7.00, meanUs=1.40, p95Us=2.70, maxUs=13.90, gen0=0
Receive + Doubao.Read x240 + 7 observers: bytes/op=UNAVAILABLE, gcAllocs/op=249.00, meanUs=299.41, p95Us=347.90, maxUs=6114.30, gen0=1
Wire checks: pre/post byte-for-byte + reliability equality; fingerprints below are comparable across code revisions.
Includes real catalog preflight. No reflection, packet capture, hashing, test resets or report formatting inside timed/allocated scope.
CPU pass has no active allocation recorder. Allocation-count pass uses Begin/End around only the operation; bytes remain UNAVAILABLE if the thread API fails calibration.

Assets/Scenes/Gameplay_Prototype.unity: 881 checks, 0 errors, 0 warnings.
Read-only assembly audit; does not establish gameplay, network or device correctness.
Assets/Scenes/World01_EarlyInternet.unity: 915 checks, 0 errors, 0 warnings.
Read-only assembly audit; does not establish gameplay, network or device correctness.
```

最终状态：Play=false，Boot，Profiler=false。最终读取 Console error 为 0；未打包、提交、推送。已知接收侧仍有分配，CPU 有编辑器/GC 波动，不能把发送侧改善当作接收侧或整体帧率改善。

