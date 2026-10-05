# 联机第一阶段与 float 分配校准

PASS: 8 session buffer semantics: retained packet ownership/reliability, nested send, writer exception recovery, subscription snapshot order, nested receive, handler isolation, malformed recovery, direction separation. Temporary objects, no socket.
Isolated networking hot-path benchmark
GC thread-counter calibration: retained new byte[4096], observed=0 B; UNAVAILABLE (zero is not a zero-allocation result)
GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
iterations=256, warmup=32; per operation; CPU and allocation-count runs measured separately; no real socket, live scene, Physics or rendered frame.
Send feedback (30 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=7.00, meanUs=0.75, p95Us=1.40, maxUs=5.70, gen0=0; packets/op=1; wireSHA256=CD825C3DAE13BFA0
World.Publish x64 (one renderer/entity): bytes/op=UNAVAILABLE, gcAllocs/op=960.00, meanUs=136.83, p95Us=193.30, maxUs=3444.90, gen0=1; packets/op=64; wireSHA256=039F044342BDC52C
Doubao.WriteSnapshot x240 (5785 B packet): bytes/op=UNAVAILABLE, gcAllocs/op=967.00, meanUs=113.31, p95Us=126.50, maxUs=4037.80, gen0=1; packets/op=1; wireSHA256=4A70ADCFA0B5F403
Receive feedback + 8 observers: bytes/op=UNAVAILABLE, gcAllocs/op=7.00, meanUs=1.06, p95Us=1.60, maxUs=4.50, gen0=0
Receive + Doubao.Read x240 + 7 observers: bytes/op=UNAVAILABLE, gcAllocs/op=249.00, meanUs=260.52, p95Us=403.90, maxUs=659.70, gen0=0
Wire checks: pre/post byte-for-byte + reliability equality; fingerprints below are comparable across code revisions.
Includes real catalog preflight. No reflection, packet capture, hashing, test resets or report formatting inside timed/allocated scope.
CPU pass has no active allocation recorder. Allocation-count pass uses Begin/End around only the operation; bytes remain UNAVAILABLE if the thread API fails calibration.

GC.Alloc current-thread calibration PASS: empty=0, retained byte[4096]=1 allocation; capacity=65536; count only (marker Value is TimeNanoseconds, not bytes).
GC thread-counter calibration: retained new byte[4096], observed=0 B; UNAVAILABLE (zero is not a zero-allocation result)
Preallocated BinaryWriter.Write(uint): bytes/op=UNAVAILABLE, gcAllocs/op=0.00, meanUs=0.03, p95Us=0.10, maxUs=0.10, gen0=0
Preallocated BinaryWriter.Write(float): bytes/op=UNAVAILABLE, gcAllocs/op=1.00, meanUs=0.07, p95Us=0.10, maxUs=0.70, gen0=0
Preallocated BinaryWriter.Write(string): bytes/op=UNAVAILABLE, gcAllocs/op=0.00, meanUs=0.05, p95Us=0.10, maxUs=0.10, gen0=0
