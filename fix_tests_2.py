import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Replace any lingering old constructor usages
content = re.sub(
    r'new AdminController\([^)]*?_mockNotificationService\.Object\s*\)',
    r'new AdminController(_context, _mockEstoqueService.Object, _mockEnv.Object, _mockPdfService.Object, _mockDashboardService.Object, _mockEquipamentoBackupService.Object, _mockLogger.Object, _mockScopeFactory.Object, _mockNotificationService.Object, _mockWorkflowProcessor.Object)',
    content
)

# For any direct variable references where _mockWorkflowProcessor was missing (like line 1404)
content = re.sub(
    r'new AdminController\([^)]*?mockNotificationService\.Object\s*\)',
    r'new AdminController(_context, mockEstoqueService.Object, mockEnv.Object, mockPdfService.Object, mockDashboardService.Object, mockEquipamentoBackupService.Object, mockLogger.Object, mockScopeFactory.Object, mockNotificationService.Object, new Mock<IWorkflowProcessor>().Object)',
    content
)

# I can just globally fix all `new AdminController(` patterns if there are others not matching.
# Let's check lines manually where the error is.
lines = content.split('\n')
for i, line in enumerate(lines):
    if "new AdminController(" in line and "_mockNotificationService.Object" in line and "_mockWorkflowProcessor" not in line:
        lines[i] = line.replace("_mockNotificationService.Object", "_mockNotificationService.Object, _mockWorkflowProcessor.Object")
    elif "new AdminController(" in line and "mockNotificationService.Object" in line and "Mock<IWorkflowProcessor>" not in line and "_mockWorkflowProcessor" not in line:
        lines[i] = line.replace("mockNotificationService.Object", "mockNotificationService.Object, new Mock<IWorkflowProcessor>().Object")

content = "\n".join(lines)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
