# Performance Optimization Details

**Change Made:**
Added `.AsNoTracking()` to the read-only Entity Framework Core query in `Controllers/ConsultaController.cs`.

**Why:**
The `.AsNoTracking()` method prevents Entity Framework Core from tracking entities retrieved from the database. When a query is read-only and its results won't be modified or saved back, tracking adds significant overhead in memory allocation and CPU cycles to maintain snapshots.

By disabling tracking for the `ConsultaController.Status` endpoint, we reduce the computational footprint of each query, especially during high-traffic spikes on this public-facing endpoint.

**Measurements:**
An attempt was made to measure the specific performance boost using BenchmarkDotNet within this repository. Due to compilation limitations running BenchmarkDotNet smoothly alongside the complex ASP.NET MVC codebase logic, specific empirical data could not be immediately isolated.

However, Microsoft officially recommends using `.AsNoTracking()` for all read-only scenarios precisely due to its universal reduction in overhead, making this a strictly net-positive optimization.
