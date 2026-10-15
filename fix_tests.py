import re

def fix():
    path = "tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerBenchmarkTests.cs"
    with open(path, "r") as f:
        content = f.read()

    # The mock cache in benchmark is not returning data, so it queries DB every time and overwrites.
    # We should set it up to return some data to actually measure cache hit performance.

    # Let's mock a working cache service
    replacement = """
            var cacheMock = new Mock<AssistenciaTech.Extensions.IResilientCacheService>();
            var cachedData = context.EquipamentosBackup.ToList();
            cacheMock.Setup(c => c.GetAsync<System.Collections.Generic.List<EquipamentoBackup>>(It.IsAny<string>()))
                .ReturnsAsync(cachedData);

            var mockFacade = new AdminFacade(
                mockEstoqueService.Object,
                mockEnv.Object,
                mockPdfGeneratorService.Object,
                mockDashboardService.Object,
                mockEquipamentoBackupService.Object,
                mockScopeFactory.Object,
                mockNotificationService.Object,
                mockClienteService.Object,
                cacheMock.Object
            );
"""

    # We will just replace the facade creation part
    old_facade_creation = """            var mockFacade = new AdminFacade(
                mockEstoqueService.Object,
                mockEnv.Object,
                mockPdfGeneratorService.Object,
                mockDashboardService.Object,
                mockEquipamentoBackupService.Object,
                mockScopeFactory.Object,
                mockNotificationService.Object,
                mockClienteService.Object,
                new Mock<AssistenciaTech.Extensions.IResilientCacheService>().Object
            );"""

    if old_facade_creation in content:
        content = content.replace(old_facade_creation, replacement)
        with open(path, "w") as f:
            f.write(content)

fix()
