import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# We need to setup the mock to return true!
# In the test `Edit_Post_ReturnsRedirectToIndex_WhenValid`, it uses the global `_controller` with `_mockWorkflowProcessor`.
# Let's verify if `_mockWorkflowProcessor.Setup` returning true is defined in the test constructor.
# Oh, we used Python string replace to add it but maybe it got overridden or not properly formatted.
# Wait, `Edit_Post_ReturnsView_WhenDatabaseThrowsException` creates a `new Mock<...>()` locally, so it doesn't setup `ReturnsAsync(true)`.
# Since `ReturnsAsync(true)` is missing for the local mock, the default is false for bool, so `ProcessAllAsync` returns false, meaning `ModelState` gets an error or it just returns `View()` early.
# In `ProcessWorkflowRulesAsync`, if it returns false, it will redirect back to View.

def fix_local_mock(content):
    # For Edit_Post_ReturnsView_WhenDatabaseThrowsException and similar
    # We should define `var mockProcessor = new Mock<IWorkflowProcessor>(); mockProcessor.Setup(x => x.ProcessAllAsync(...)).ReturnsAsync(true);`
    # Let's just create a generic processor builder in our test setup instead of manually adding to every new AdminController call, or just search and replace.

    # Replace inline `new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object` with `CreateMockProcessor().Object`
    content = content.replace("new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object", "CreateMockProcessor().Object")
    content = content.replace("new Mock<IWorkflowProcessor>().Object", "CreateMockProcessor().Object")

    # Add a helper method
    helper = """
        private Mock<IWorkflowProcessor> CreateMockProcessor()
        {
            var mock = new Mock<IWorkflowProcessor>();
            mock.Setup(x => x.ProcessAllAsync(It.IsAny<OrdemServico>(), It.IsAny<string>(), It.IsAny<OrdemServico>(), It.IsAny<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>())).ReturnsAsync(true);
            return mock;
        }
    """
    # Insert it after the constructor
    content = re.sub(r'(public AdminControllerTests\(\)\s*\{[^}]*\})', r'\1\n' + helper, content)
    return content

content = fix_local_mock(content)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
