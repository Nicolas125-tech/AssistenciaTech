import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# CS7036 means we still haven't fixed the constructor correctly for some lines!
# I will use a reliable string replacement for all exact strings that cause this.

old_str_1 = """var localController = new AdminController(
                _context,
                mockEstoqueService.Object,
                mockEnv.Object,
                mockPdfService.Object,
                mockDashboardService.Object,
                mockEquipamentoBackupService.Object,
                mockLogger.Object,
                mockScopeFactory.Object,
                mockNotificationService.Object
            );"""

new_str_1 = """var localController = new AdminController(
                _context,
                mockEstoqueService.Object,
                mockEnv.Object,
                mockPdfService.Object,
                mockDashboardService.Object,
                mockEquipamentoBackupService.Object,
                mockLogger.Object,
                mockScopeFactory.Object,
                mockNotificationService.Object,
                new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object
            );"""

content = content.replace(old_str_1, new_str_1)

old_str_2 = """var localController = new AdminController(
                _context,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfService.Object,
                _mockDashboardService.Object,
                _mockEquipamentoBackupService.Object,
                _mockLogger.Object,
                _mockScopeFactory.Object,
                _mockNotificationService.Object
            );"""

new_str_2 = """var localController = new AdminController(
                _context,
                _mockEstoqueService.Object,
                _mockEnv.Object,
                _mockPdfService.Object,
                _mockDashboardService.Object,
                _mockEquipamentoBackupService.Object,
                _mockLogger.Object,
                _mockScopeFactory.Object,
                _mockNotificationService.Object,
                _mockWorkflowProcessor.Object
            );"""

content = content.replace(old_str_2, new_str_2)

# Fix line 1404
content = content.replace("_mockWorkflowProcessor", "new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>()")

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
