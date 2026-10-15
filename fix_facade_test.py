import re

def fix():
    with open("tests/AssistenciaTech.Application.Tests/Services/AdminFacadeTests.cs", "r") as f:
        content = f.read()

    # We replaced incorrectly before: adminFacade.Cache.Should().BeSameAs(cacheMock.Object);
    # Let's write the whole file manually to be safe.

    new_content = """using System;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class AdminFacadeTests
    {
        [Fact]
        public void Constructor_ShouldInitializeAllPropertiesCorrectly()
        {
            // Arrange
            var estoqueMock = new Mock<IEstoqueService>();
            var envMock = new Mock<IWebHostEnvironment>();
            var pdfGeneratorMock = new Mock<IPdfGeneratorService>();
            var dashboardMock = new Mock<IAdminDashboardService>();
            var equipamentoBackupMock = new Mock<IEquipamentoBackupService>();
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var notificationMock = new Mock<INotificationService>();
            var clienteMock = new Mock<IClienteService>();
            var cacheMock = new Mock<AssistenciaTech.Extensions.IResilientCacheService>();

            // Act
            var adminFacade = new AdminFacade(
                estoqueMock.Object,
                envMock.Object,
                pdfGeneratorMock.Object,
                dashboardMock.Object,
                equipamentoBackupMock.Object,
                scopeFactoryMock.Object,
                notificationMock.Object,
                clienteMock.Object,
                cacheMock.Object);

            // Assert
            adminFacade.Estoque.Should().BeSameAs(estoqueMock.Object);
            adminFacade.Env.Should().BeSameAs(envMock.Object);
            adminFacade.PdfGenerator.Should().BeSameAs(pdfGeneratorMock.Object);
            adminFacade.Dashboard.Should().BeSameAs(dashboardMock.Object);
            adminFacade.EquipamentoBackup.Should().BeSameAs(equipamentoBackupMock.Object);
            adminFacade.ScopeFactory.Should().BeSameAs(scopeFactoryMock.Object);
            adminFacade.Notification.Should().BeSameAs(notificationMock.Object);
            adminFacade.Cliente.Should().BeSameAs(clienteMock.Object);
            adminFacade.Cache.Should().BeSameAs(cacheMock.Object);
        }
    }
}
"""
    with open("tests/AssistenciaTech.Application.Tests/Services/AdminFacadeTests.cs", "w") as f:
        f.write(new_content)

fix()
