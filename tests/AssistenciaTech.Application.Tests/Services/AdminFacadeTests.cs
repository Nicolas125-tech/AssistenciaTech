using System;
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

            // Act
            var adminFacade = new AdminFacade(
                estoqueMock.Object,
                envMock.Object,
                pdfGeneratorMock.Object,
                dashboardMock.Object,
                equipamentoBackupMock.Object,
                scopeFactoryMock.Object,
                notificationMock.Object,
                clienteMock.Object);

            // Assert
            adminFacade.Estoque.Should().BeSameAs(estoqueMock.Object);
            adminFacade.Env.Should().BeSameAs(envMock.Object);
            adminFacade.PdfGenerator.Should().BeSameAs(pdfGeneratorMock.Object);
            adminFacade.Dashboard.Should().BeSameAs(dashboardMock.Object);
            adminFacade.EquipamentoBackup.Should().BeSameAs(equipamentoBackupMock.Object);
            adminFacade.ScopeFactory.Should().BeSameAs(scopeFactoryMock.Object);
            adminFacade.Notification.Should().BeSameAs(notificationMock.Object);
            adminFacade.Cliente.Should().BeSameAs(clienteMock.Object);
        }
    }
}
