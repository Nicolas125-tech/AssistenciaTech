import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Add missing mock and parameter
if "Mock<IWorkflowProcessor> _mockWorkflowProcessor;" not in content:
    content = content.replace("Mock<INotificationService> _mockNotificationService;", "Mock<INotificationService> _mockNotificationService;\n        Mock<IWorkflowProcessor> _mockWorkflowProcessor;")
    content = content.replace("_mockNotificationService = new Mock<INotificationService>();", "_mockNotificationService = new Mock<INotificationService>();\n            _mockWorkflowProcessor = new Mock<IWorkflowProcessor>();")
    content = content.replace("using AssistenciaTech.Services;", "using AssistenciaTech.Services;\nusing AssistenciaTech.Services.Workflow;")

old_constructor = "new AdminController(\n                _context,\n                _mockEstoqueService.Object,\n                _mockEnv.Object,\n                _mockPdfService.Object,\n                _mockDashboardService.Object,\n                _mockEquipamentoBackupService.Object,\n                _mockLogger.Object,\n                _mockScopeFactory.Object,\n                _mockNotificationService.Object\n            );"

new_constructor = "new AdminController(\n                _context,\n                _mockEstoqueService.Object,\n                _mockEnv.Object,\n                _mockPdfService.Object,\n                _mockDashboardService.Object,\n                _mockEquipamentoBackupService.Object,\n                _mockLogger.Object,\n                _mockScopeFactory.Object,\n                _mockNotificationService.Object,\n                _mockWorkflowProcessor.Object\n            );"

content = content.replace(old_constructor, new_constructor)

# Search for other usages of AdminController constructor that might be on a single line
content = re.sub(r'new AdminController\(([^)]*?_mockNotificationService\.Object[^)]*?)\)', r'new AdminController(\1, _mockWorkflowProcessor.Object)', content)

# ensure we fix the mock returns for Workflow processor, it needs to return true by default
content = content.replace("_mockWorkflowProcessor = new Mock<IWorkflowProcessor>();", "_mockWorkflowProcessor = new Mock<IWorkflowProcessor>();\n            _mockWorkflowProcessor.Setup(x => x.ProcessAllAsync(It.IsAny<OrdemServico>(), It.IsAny<string>(), It.IsAny<OrdemServico>(), It.IsAny<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary>())).ReturnsAsync(true);")


with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
