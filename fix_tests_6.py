import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Let's replace any `new AdminController(...)` with simply creating the mock processor where missing
# Look for CS7036 in these lines: 544, 588, 637, 956, 1276, and CS0103 in 1404
lines = content.split('\n')
for i in range(len(lines)):
    if "new AdminController(" in lines[i]:
        # we can just find where it ends and insert the argument.
        pass

# Since regex is failing, let's just append the mockWorkflowProcessor to the argument list of all AdminController instantiations.
content = re.sub(
    r'(mockNotificationService\.Object\s*)\)',
    r'\1, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object)',
    content
)
content = re.sub(
    r'(_mockNotificationService\.Object\s*)\)',
    r'\1, _mockWorkflowProcessor.Object)',
    content
)

# And specifically fix line 1404 which is currently:
# _mockWorkflowProcessor does not exist...
# Let's just create a Mock<IWorkflowProcessor> at the top of the test class if it's missing, wait it's there.
# Let's just replace `_mockWorkflowProcessor` with `new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>()` in the test where it fails if it's out of scope.
content = re.sub(
    r'var localController = new AdminController\(\s*_context,\s*mockEstoqueService\.Object,\s*mockEnv\.Object,\s*mockPdfService\.Object,\s*mockDashboardService\.Object,\s*mockEquipamentoBackupService\.Object,\s*mockLogger\.Object,\s*mockScopeFactory\.Object,\s*mockNotificationService\.Object\s*\);',
    r'var localController = new AdminController(_context, mockEstoqueService.Object, mockEnv.Object, mockPdfService.Object, mockDashboardService.Object, mockEquipamentoBackupService.Object, mockLogger.Object, mockScopeFactory.Object, mockNotificationService.Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object);',
    content, flags=re.DOTALL
)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
