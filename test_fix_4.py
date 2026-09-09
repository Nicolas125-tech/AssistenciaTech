import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Instead of relying on specific matching, let's just make ALL instances of new AdminController valid.
# Currently they fail on CS7036, which means they are missing the 10th parameter (IWorkflowProcessor).

# We will just manually fix the known lines: 544, 588, 637, 956, 1276.
lines = content.split('\n')
for i in range(len(lines)):
    if "new AdminController(" in lines[i]:
        # If it's single line and we see mockNotificationService.Object
        if "mockNotificationService.Object)" in lines[i] and "_mockWorkflowProcessor.Object" not in lines[i] and "new Mock" not in lines[i]:
            lines[i] = lines[i].replace("mockNotificationService.Object)", "mockNotificationService.Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object)")

        # Or if it spans lines, let's look down until we find the closing )
        elif "mockNotificationService.Object" not in lines[i]:
            # This is a bit tricky, let's just do a big regex replacement again properly
            pass

content = "\n".join(lines)
# Regex to find mockNotificationService.Object followed by ) with optional whitespace
content = re.sub(r'(mockNotificationService\.Object\s*)\)', r'\1, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object)', content)
content = re.sub(r'(_mockNotificationService\.Object\s*)\)', r'\1, _mockWorkflowProcessor.Object)', content)

# Fix line 1404 where _mockWorkflowProcessor was missing
content = content.replace("1404", "1404") # dummy
content = content.replace("var localController = new AdminController(\n                _context,\n                mockEstoqueService.Object,\n                mockEnv.Object,\n                mockPdfService.Object,\n                mockDashboardService.Object,\n                mockEquipamentoBackupService.Object,\n                mockLogger.Object,\n                mockScopeFactory.Object,\n                mockNotificationService.Object\n            );", "var localController = new AdminController(\n                _context,\n                mockEstoqueService.Object,\n                mockEnv.Object,\n                mockPdfService.Object,\n                mockDashboardService.Object,\n                mockEquipamentoBackupService.Object,\n                mockLogger.Object,\n                mockScopeFactory.Object,\n                mockNotificationService.Object,\n                new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object\n            );")

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
