``` ini

BenchmarkDotNet=v0.10.8, OS=Windows 10 Redstone 1 (10.0.14393)
Processor=Intel Core i5-2430M CPU 2.40GHz (Sandy Bridge), ProcessorCount=4
Frequency=2338439 Hz, Resolution=427.6357 ns, Timer=TSC
  [Host] : Clr 4.0.30319.42000, 32bit LegacyJIT-v4.6.1648.0DEBUG [AttachedDebugger]


```
 |      Method | SiloCount | GrainTypeCount | GrainMethodCount | GrainActivationPerSiloCount | GrainCallsPerActivationCount | Mean | Error |
 |------------ |---------- |--------------- |----------------- |---------------------------- |----------------------------- |-----:|------:|
 | **Recalculate** |         **3** |             **50** |               **10** |                         **100** |                         **1000** |   **NA** |    **NA** |
 | **Recalculate** |         **3** |            **100** |               **10** |                         **100** |                         **1000** |   **NA** |    **NA** |
 | **Recalculate** |         **3** |            **200** |               **10** |                         **100** |                         **1000** |   **NA** |    **NA** |

Benchmarks with issues:
  DashboardGrainBenchmark.Recalculate: DefaultJob [SiloCount=3, GrainTypeCount=50, GrainMethodCount=10, GrainActivationPerSiloCount=100, GrainCallsPerActivationCount=1000]
  DashboardGrainBenchmark.Recalculate: DefaultJob [SiloCount=3, GrainTypeCount=100, GrainMethodCount=10, GrainActivationPerSiloCount=100, GrainCallsPerActivationCount=1000]
  DashboardGrainBenchmark.Recalculate: DefaultJob [SiloCount=3, GrainTypeCount=200, GrainMethodCount=10, GrainActivationPerSiloCount=100, GrainCallsPerActivationCount=1000]
