```

BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Processor 2.30GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  Job-CNUJVU : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

InvocationCount=1  UnrollFactor=1

```
| Method                          | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated  | Alloc Ratio |
|-------------------------------- |----------:|----------:|----------:|----------:|------:|--------:|-----------:|------------:|
| EFCoreTracking_Original         | 40.849 ms | 0.7666 ms | 1.9513 ms | 40.434 ms |  1.00 |    0.07 | 9419.95 KB |        1.00 |
| ExecuteUpdate_Optimized_Batched |  6.409 ms | 0.3224 ms | 0.9404 ms |  5.957 ms |  0.16 |    0.02 |  308.09 KB |        0.03 |
