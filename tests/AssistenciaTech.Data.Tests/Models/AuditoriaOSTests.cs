using System;
using AssistenciaTech.Models;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Data.Tests.Models;

public class AuditoriaOSTests
{
    [Fact]
    public void AuditoriaOS_Initialization_SetsDefaultValues()
    {
        // Act
        var auditoria = new AuditoriaOS();

        // Assert
        auditoria.Usuario.Should().Be(string.Empty);
        auditoria.DataAlteracao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AuditoriaOS_Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var expectedDate = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var auditoria = new AuditoriaOS();

        // Act
        auditoria.Id = 1;
        auditoria.OrdemServicoId = 100;
        auditoria.Usuario = "Admin";
        auditoria.DataAlteracao = expectedDate;
        auditoria.CampoAlterado = "Status";
        auditoria.ValorAntigo = "Pendente";
        auditoria.ValorNovo = "Concluido";
        auditoria.DetalhesAlteracao = "{\"Status\": {\"Old\": \"Pendente\", \"New\": \"Concluido\"}}";

        // Assert
        auditoria.Id.Should().Be(1);
        auditoria.OrdemServicoId.Should().Be(100);
        auditoria.Usuario.Should().Be("Admin");
        auditoria.DataAlteracao.Should().Be(expectedDate);
        auditoria.CampoAlterado.Should().Be("Status");
        auditoria.ValorAntigo.Should().Be("Pendente");
        auditoria.ValorNovo.Should().Be("Concluido");
        auditoria.DetalhesAlteracao.Should().Be("{\"Status\": {\"Old\": \"Pendente\", \"New\": \"Concluido\"}}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AuditoriaOS_NullableProperties_CanBeNullOrWhitespace(string? value)
    {
        // Act
        var auditoria = new AuditoriaOS
        {
            CampoAlterado = value,
            ValorAntigo = value,
            ValorNovo = value,
            DetalhesAlteracao = value
        };

        // Assert
        auditoria.CampoAlterado.Should().Be(value);
        auditoria.ValorAntigo.Should().Be(value);
        auditoria.ValorNovo.Should().Be(value);
        auditoria.DetalhesAlteracao.Should().Be(value);
    }
}
