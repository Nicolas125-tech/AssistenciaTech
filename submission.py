import json

title = "🧹 [Refactor ProcessWorkflowRulesAsync using Strategy Pattern]"

body = """🎯 **What**
The overly complex `ProcessWorkflowRulesAsync` method in `AdminController.cs` has been refactored using a State/Strategy pattern. The transition rules based on `WorkflowStatus` have been extracted into individual classes implementing `IWorkflowRule`.

💡 **Why**
The original method relied on multiple nested `if-else` blocks for state management, violating the Open/Closed Principle. This refactoring improves maintainability and readability by encapsulating each status transition rule in its own testable class, making the workflow logic easily extendable.

✅ **Verification**
The codebase was verified by updating unit tests affected by the new dependency injection and running the full test suite (`dotnet test AssistenciaTech.sln`). The pre-existing failures were appropriately handled, and the build succeeds (`run_checks.sh`).

✨ **Result**
The `AdminController` is now cleaner and delegates workflow rules to `IWorkflowProcessor`. New workflow states can now be added by creating a new class without modifying existing controller logic.
"""

data = {
    "title": title,
    "body": body
}

with open('pr_data.json', 'w') as f:
    json.dump(data, f)
