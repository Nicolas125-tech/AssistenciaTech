🎯 **What:** Added missing tests for `ClienteService`. Specifically, the logic for generating SelectListItems for Clientes was uncovered by tests.
📊 **Coverage:** Added coverage for `GetClientesSelectListAsync` focusing on:
  - Empty database scenarios.
  - Correct formatting of the `SelectListItem.Text` field (combining Name, CPF, and Phone).
  - Null `selectedId` behavior (none selected).
  - Matching `selectedId` behavior (correct item marked as selected).
✨ **Result:** Enhanced the test coverage for application services, guaranteeing that future changes to `ClienteService` won't break the UI components depending on these dropdown lists.
