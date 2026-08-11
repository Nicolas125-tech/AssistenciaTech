using System;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using QuestPDF.Infrastructure;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class PdfGeneratorServiceTests
    {
        public PdfGeneratorServiceTests()
        {
            // QuestPDF requires license configuration
            QuestPDF.Settings.License = LicenseType.Community;
        }

        [Fact]
        public void GenerateOsPdf_ReturnsValidByteArray_ForFullyPopulatedOs()
        {
            // Arrange
            var service = new PdfGeneratorService();
            var os = new OrdemServico
            {
                Id = 1,
                Cliente = new Cliente { Nome = "John Doe", Cpf = "123.456.789-00" },
                Equipamento = "Notebook Dell",
                NumeroSerie = "SN123456",
                ProblemaRelatado = "Não liga",
                LaudoTecnico = "Placa mãe em curto",
                AvariasPreExistentes = "Riscos na tampa",
                Status = WorkflowStatus.Concluido,
                DataEntrada = new DateTime(2023, 1, 1, 10, 0, 0),
                DataEntregaCliente = new DateTime(2023, 1, 5, 14, 0, 0),
                ValorOrcamento = 1500.00m
            };

            // Act
            var pdfBytes = service.GenerateOsPdf(os);

            // Assert
            pdfBytes.Should().NotBeNull();
            pdfBytes.Should().NotBeEmpty();

            // Check for PDF magic number (%PDF-)
            pdfBytes.Length.Should().BeGreaterThan(5);
            pdfBytes[0].Should().Be(0x25); // %
            pdfBytes[1].Should().Be(0x50); // P
            pdfBytes[2].Should().Be(0x44); // D
            pdfBytes[3].Should().Be(0x46); // F
            pdfBytes[4].Should().Be(0x2D); // -
        }

        [Fact]
        public void GenerateOsPdf_HandlesMinimalOsData()
        {
            // Arrange
            var service = new PdfGeneratorService();
            // Create an OS with only the required fields populated
            var os = new OrdemServico
            {
                Id = 2,
                Equipamento = "PC Gamer",
                ProblemaRelatado = "Formatacao",
                Status = WorkflowStatus.Recebido,
                DataEntrada = DateTime.Now
                // Missing Cliente, NumeroSerie, LaudoTecnico, AvariasPreExistentes, DataEntregaCliente
            };

            // Act
            var pdfBytes = service.GenerateOsPdf(os);

            // Assert
            pdfBytes.Should().NotBeNull();
            pdfBytes.Should().NotBeEmpty();

            // Check for PDF magic number (%PDF-)
            pdfBytes.Length.Should().BeGreaterThan(5);
            pdfBytes[0].Should().Be(0x25);
            pdfBytes[1].Should().Be(0x50);
            pdfBytes[2].Should().Be(0x44);
            pdfBytes[3].Should().Be(0x46);
            pdfBytes[4].Should().Be(0x2D);
        }

        [Fact]
        public void GenerateOsPdf_ThrowsArgumentNullException_WhenOsIsNull()
        {
            // Arrange
            var service = new PdfGeneratorService();

            // Act
            Action act = () => service.GenerateOsPdf(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("os");
        }

        [Fact]
        public void GenerateOsPdf_HandlesExpiredWarranty_AndEntregueStatus()
        {
            // Arrange
            var service = new PdfGeneratorService();
            var os = new OrdemServico
            {
                Id = 3,
                Equipamento = "Smartphone",
                ProblemaRelatado = "Tela quebrada",
                Status = WorkflowStatus.Entregue,
                DataEntrada = DateTime.Now.AddDays(-110),
                DataConclusao = DateTime.Now.AddDays(-105),
                DataEntregaCliente = DateTime.Now.AddDays(-100)
            };

            // Act
            var pdfBytes = service.GenerateOsPdf(os);

            // Assert
            pdfBytes.Should().NotBeNull();
            pdfBytes.Should().NotBeEmpty();

            // Check for PDF magic number (%PDF-)
            pdfBytes.Length.Should().BeGreaterThan(5);
            pdfBytes[0].Should().Be(0x25);
            pdfBytes[1].Should().Be(0x50);
            pdfBytes[2].Should().Be(0x44);
            pdfBytes[3].Should().Be(0x46);
            pdfBytes[4].Should().Be(0x2D);
        }
    }
}
