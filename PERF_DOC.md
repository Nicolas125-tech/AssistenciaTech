# Performance Optimization Details

**Change Made:**
Added `.AsNoTracking()` to the read-only Entity Framework Core query in `Controllers/ConsultaController.cs`.

**Why:**
Entity Framework Core tracks entities from the database by default. If a query is read-only, tracking wastes memory and CPU cycles to maintain those snapshots.

Disabling tracking for the `ConsultaController.Status` endpoint reduces overhead per query. This helps during traffic spikes on public endpoints.

**Measurements:**
I tried to measure the performance difference using BenchmarkDotNet, but compiling it alongside the ASP.NET MVC codebase made it hard to isolate the data. 

Microsoft recommends `.AsNoTracking()` for read-only queries because it consistently reduces overhead.
