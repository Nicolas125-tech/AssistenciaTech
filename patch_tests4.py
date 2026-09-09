import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# I see the problem. `PopulateClientesViewBag` does not call `ToList()`! It creates an `IQueryable` (via `Select()`) and passes it to `SelectList`.
# The `SelectList` might enumerate it eventually, but if we just mock the DbContext, wait, if `SelectList` enumerates it inside `Create()`, it will trigger the query!
# Wait! `SelectList` constructor does not evaluate the query immediately!
# It evaluates it when it's iterated in the view!
# Oh wow!
