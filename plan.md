1. **Analyze the Issue:**
   - In `Controllers/AdminController.cs`, `PopulateViewBagsForEditAsync` fetches a list of backup equipment for a dropdown using `await _context.EquipamentosBackup.AsNoTracking().Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToListAsync();`.
   - The same issue exists in `Edit` (GET): `ViewBag.EquipamentosBackup = new SelectList(await _context.EquipamentosBackup.AsNoTracking().Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).Select(e => new { e.Id, e.Descricao }).ToListAsync(), "Id", "Descricao");`
   - This data is rarely changing, so it should be cached to improve performance.

2. **Refactor Code:**
   - Update `AdminController.cs` to use `IResilientCacheService`.
   - Inject `IResilientCacheService` into the `AdminController` constructor. Since `AdminController` constructor takes `IAdminFacade`, we can either add the cache to the facade or inject it directly. Since `IResilientCacheService` is available in `IAdminFacade`... wait, let's check if it is. No, `IAdminFacade` does not have `IResilientCacheService`. We'll inject `IResilientCacheService` directly into the `AdminController` constructor. Wait, no, we can't change the constructor signature without updating all tests. Let's look at how we can get it. Maybe we can get it via `HttpContext.RequestServices` or inject it. Let's update `AdminController`'s constructor and update the test instances to pass a mock. We already have 10 occurrences of `new AdminController` in tests.

   Wait, it might be easier to use `IMemoryCache` via `HttpContext.RequestServices` if we don't want to change constructor, but `IResilientCacheService` is the project standard wrapper for `IDistributedCache`. Let's update `IAdminFacade` to include `IResilientCacheService Cache { get; }`? `EstoqueService` and `AdminDashboardService` take `IResilientCacheService` directly. `AdminController` currently takes `AppDbContext`, `ILogger`, `IAdminFacade`.
   Let's check `AdminFacade`.

3. **Update AdminFacade**
   - Add `IResilientCacheService Cache { get; }` to `IAdminFacade`.
   - Update `AdminFacade` to accept `IResilientCacheService` in the constructor and set the property.
   - Update all `AdminFacade` instantiations in tests to include a `Mock<IResilientCacheService>.Object` argument.

4. **Implement Caching in AdminController:**
   - In `PopulateViewBagsForEditAsync`, instead of querying the DB directly, use `_facade.Cache.GetAsync<List<EquipamentoBackup>>("EquipamentosBackup_Disponiveis")`.
   - Wait, the query filters by `e.Disponivel || e.Id == ordemServico.EquipamentoBackupId`. So we can cache all `EquipamentosBackup` or all `Disponivel` ones. Caching the whole list of backup equipment and filtering in memory is much faster.
   - Let's create a helper method `GetEquipamentosBackupCachedAsync()`:
     ```csharp
     var cacheKey = "EquipamentosBackup_All";
     var allEquipamentos = await _facade.Cache.GetAsync<List<EquipamentoBackup>>(cacheKey);
     if (allEquipamentos == null)
     {
         allEquipamentos = await _context.EquipamentosBackup.AsNoTracking().ToListAsync();
         await _facade.Cache.SetAsync(cacheKey, allEquipamentos, TimeSpan.FromMinutes(60));
     }
     ```
   - Then use `allEquipamentos.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToList()`.
   - Same for the `Edit` (GET) method. Replace the direct query with the cached list.

5. **Invalidation:**
   - When an equipment becomes unavailable or available (e.g. `EquipamentoBackupService.ProcessarTrocaEquipamentoAsync`), the cache should be invalidated. We can inject `IResilientCacheService` into `EquipamentoBackupService` and call `_cache.RemoveAsync("EquipamentosBackup_All")`.
   - We need to find everywhere `EquipamentosBackup` is modified and invalidate the cache. Wait, maybe just short expiration (e.g., 5-10 minutes) is enough, or we can use the `IResilientCacheService` to invalidate it in `EquipamentoBackupService`.

6. **Verify and Present:**
   - Run the benchmark before and after.
   - Commit the changes using the `submit` tool.

Let's refine the plan.
