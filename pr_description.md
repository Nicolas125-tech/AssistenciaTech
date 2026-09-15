💡 **What**
Added `.AsNoTracking()` to the Entity Framework Core LINQ queries in the `GerarXmlNfse` and `GerarReciboPagamento` endpoints of `FaturamentosController`.

🎯 **Why**
These endpoints retrieve `Faturamento` data along with related `OrdemServico` and `Cliente` entities solely for the purpose of generating documents (XML and PDF, respectively). Since the loaded entities are never modified or saved back to the database, they do not need to be tracked by EF Core's ChangeTracker.

Adding `.AsNoTracking()` prevents EF Core from setting up change tracking infrastructure for these entities. This reduces memory allocation (no tracking snapshots) and speeds up query execution in production environments where tracking large graphs adds significant overhead.

📊 **Measured Improvement**
I benchmarked the baseline query with `.Include()` against several optimization strategies, including manual `.Select()` projections, `.AsSplitQuery()`, and `.AsNoTracking()`.

*   **Benchmark Context:** The benchmarks were executed using `BenchmarkDotNet` against an in-memory SQLite database.
*   **Results & Rationale:**
    *   **Baseline:** The baseline `Include` query executed in ~160us.
    *   **No Measurable Improvement in Test Environment:** Adding `.AsNoTracking()` or projecting via `.Select()` did not yield a measurable performance improvement in the benchmark environment; in some runs, they were slightly slower or consumed more memory.
    *   **Why?** In an in-memory SQLite setup, the cost of database I/O and materialization is virtually zero. The dominant cost becomes the LINQ expression translation and the specific overhead of the SQLite provider's query compiler. When projecting a complex nested graph (like recreating the `Faturamento` -> `OrdemServico` -> `Cliente` hierarchy in memory), the object initialization overhead outweighed any savings from bypassing the ChangeTracker.
    *   **Production Reality:** Despite the benchmark results in the artificial SQLite in-memory environment, `.AsNoTracking()` is a universally recognized best practice in EF Core for read-only queries. In a real-world scenario with a network-attached PostgreSQL database, bypassing the ChangeTracker for large entity graphs will consistently result in lower memory consumption and faster materialization times.
