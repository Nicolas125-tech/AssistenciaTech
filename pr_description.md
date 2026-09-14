🎯 **What:**
Removed the dead code comment `// Gravar os impostos desmembrados` in `Controllers/FaturamentosController.cs` and other formatting issues caught by `dotnet format`. The manual tax calculation logic was already replaced by the domain service (`_tributacaoService.CalcularTributos(os)`).

💡 **Why:**
This improves readability and maintainability by removing comments that are no longer relevant to the current logic, avoiding confusion for future maintainers.

✅ **Verification:**
Confirmed via `git diff` that the correct line was removed. Ran the full test suite (`dotnet test`) and verified that no functionality was broken (only pre-existing failing tests remained).

✨ **Result:**
The codebase is cleaner and no longer contains dead comments related to tax calculation.
