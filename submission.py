import json

title = "🧪 Add missing error path tests in AdminController"
body = """🎯 **What:**
Addressed testing gaps in `AdminController.cs` database failure paths. Specifically added verification for the `Create` (POST) error path (which writes exceptions to `ModelState`) and missing logger assertions for the `Index` (GET) `DbException` error path. The `Create` (GET) error path was investigated, but since `PopulateClientesViewBag` uses deferred execution with `SelectList`, the query is evaluated in the view, rendering synchronous catch blocks in the controller logic unreachable during typical unit tests.

📊 **Coverage:**
- Assertions for `_mockLogger.Verify` added to the existing `DbException` test in `Index` (GET).
- Created a new test `Create_Post_ReturnsViewResult_WithModelStateError_WhenDatabaseFails` to cover `Exception` catching in `Create` (POST).

✨ **Result:**
Improved test coverage, assertions, and reliability for `AdminController.cs` database failure scenarios, confirming the application correctly handles and logs these errors without breaking."""

import os
with open("pr_data.json", "w") as f:
    json.dump({"title": title, "body": body}, f)
