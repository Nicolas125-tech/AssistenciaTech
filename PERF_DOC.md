# Performance Optimization Details

**Change Made:**
Added `.AsNoTracking()` to the Entity Framework Core queries used to populate SelectList objects in `Controllers/AdminController.cs`.

**Why:**
The `.AsNoTracking()` method prevents Entity Framework Core from tracking entities retrieved from the database. When a query is read-only and its results won't be modified or saved back (such as fetching reference data for UI dropdown lists), tracking adds significant overhead in memory allocation and CPU cycles to maintain snapshots.

By disabling tracking for these queries, we reduce the computational footprint, decreasing both the response time and the memory pressure when generating the Edit view.

**Measurements:**
A standalone BenchmarkDotNet test simulating this specific query logic yielded the following results:

| Method         | Mean     | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------- |---------:|----------:|----------:|------:|--------:|----------:|------------:|
| WithTracking   | 7.159 ms | 1.3779 ms | 4.0412 ms |  1.40 |    1.21 |  738.5 KB |        1.00 |
| WithNoTracking | 2.789 ms | 0.0541 ms | 0.0531 ms |  0.55 |    0.31 | 288.26 KB |        0.39 |

* The AsNoTracking optimization is approximately **60% faster**.
* The AsNoTracking optimization uses approximately **60% less memory**.
