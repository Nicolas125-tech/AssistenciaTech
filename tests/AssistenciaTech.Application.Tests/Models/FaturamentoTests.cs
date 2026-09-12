using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using FluentAssertions;
using AssistenciaTech.Models;

namespace AssistenciaTech.Domain.Tests.Models;

public class FaturamentoTests
{
    private IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void Constructor_DeveInicializarComStatusPendente()
    {
        // Arrange & Act
        var faturamento = new Faturamento();

        // Assert
        faturamento.StatusPagamento.Should().Be(PagamentoStatus.Pendente);
    }

    [Fact]
    public void Faturamento_ValidModel_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var faturamento = new Faturamento
        {
            Id = 1,
            OrdemServicoId = 1,
            ValorTotal = 150.0m,
            DataVencimento = DateTime.UtcNow.AddDays(7),
            StatusPagamento = PagamentoStatus.Pendente,
            TxIdPix = "TXID123",
            QrCodePayload = "PAYLOAD",
            BaseCalculoISS = 100.0m,
            AliquotaISS = 5.0m,
            ValorISS = 5.0m,
            BaseCalculoICMS = 100.0m,
            AliquotaICMS = 18.0m,
            ValorICMS = 18.0m
        };

        // Act
        var results = ValidateModel(faturamento);

        // Assert
        results.Should().BeEmpty();
    }
}
