using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class AreaClienteEngineTests
    {
        private AppDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComOsECpfValidosFormatados_DeveRetornarOkComOrdemECliente()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 1, Nome = "Ana Paula", Cpf = "123.456.789-01", Telefone = "11988887777" };
            var os = new OrdemServico { Id = 10, Cliente = cliente, Equipamento = "Dell Inspiron", Status = WorkflowStatus.EmAnalise, DataEntrada = DateTime.UtcNow };

            context.Clientes.Add(cliente);
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act - Cliente envia CPF formatado
            var result = await engine.ConsultarStatusPublicoAsync(10, "123.456.789-01");

            // Assert
            result.Sucesso.Should().BeTrue();
            result.Ordem.Should().NotBeNull();
            result.Ordem!.Id.Should().Be(10);
            result.Ordem.Cliente.Should().NotBeNull();
            result.Ordem.Cliente!.Nome.Should().Be("Ana Paula");
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComOsECpfValidosApenasDigitos_DeveRetornarOkComOrdemECliente()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 2, Nome = "Bruno Silva", Cpf = "123.456.789-01", Telefone = "11988887777" };
            var os = new OrdemServico { Id = 20, Cliente = cliente, Equipamento = "MacBook Air", Status = WorkflowStatus.EmReparo, DataEntrada = DateTime.UtcNow };

            context.Clientes.Add(cliente);
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act - Cliente envia apenas números sem pontuação
            var result = await engine.ConsultarStatusPublicoAsync(20, "12345678901");

            // Assert
            result.Sucesso.Should().BeTrue();
            result.Ordem.Should().NotBeNull();
            result.Ordem!.Id.Should().Be(20);
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComCpfExcessivamenteLongo_DevePrevenirDoS_ERetornarInvalido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);
            string cpfLongo = new string('9', 100);

            // Act
            var result = await engine.ConsultarStatusPublicoAsync(1, cpfLongo);

            // Assert
            result.Sucesso.Should().BeFalse();
            result.ParametrosInvalidos.Should().BeTrue();
            result.Mensagem.Should().Contain("preencha o número da OS e o CPF");
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComNumeroOSInvalido_DeveRetornarInvalido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.ConsultarStatusPublicoAsync(0, "12345678901");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.ParametrosInvalidos.Should().BeTrue();
            result.Mensagem.Should().Contain("preencha o número da OS e o CPF");
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComCpfVazioOuNulo_DeveRetornarInvalido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.ConsultarStatusPublicoAsync(5, "   ");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.ParametrosInvalidos.Should().BeTrue();
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComOsInexistente_DeveRetornarNaoEncontrado()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.ConsultarStatusPublicoAsync(999, "12345678901");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.ParametrosInvalidos.Should().BeFalse();
            result.Mensagem.Should().Contain("não encontrada ou CPF inválido");
        }

        [Fact]
        public async Task ConsultarStatusPublicoAsync_ComCpfDivergenteDaOS_DeveRetornarNaoEncontrado()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 3, Nome = "Carlos", Cpf = "11122233344" };
            var os = new OrdemServico { Id = 30, Cliente = cliente, DataEntrada = DateTime.UtcNow };

            context.Clientes.Add(cliente);
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act - CPF incorreto para a OS 30
            var result = await engine.ConsultarStatusPublicoAsync(30, "99988877766");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.ParametrosInvalidos.Should().BeFalse();
            result.Mensagem.Should().Contain("não encontrada ou CPF inválido");
        }

        [Fact]
        public async Task ObterEquipamentosPorEmailAsync_ComEmailValido_DeveRetornarOrdensOrdenadasPorDataEntradaDesc()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 4, Nome = "Daniela", Email = "daniela@teste.com" };
            var os1 = new OrdemServico { Id = 41, Cliente = cliente, DataEntrada = DateTime.UtcNow.AddDays(-5) };
            var os2 = new OrdemServico { Id = 42, Cliente = cliente, DataEntrada = DateTime.UtcNow.AddDays(-1) };

            context.Clientes.Add(cliente);
            context.OrdensServico.AddRange(os1, os2);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var ordens = await engine.ObterEquipamentosPorEmailAsync("daniela@teste.com");

            // Assert
            ordens.Should().HaveCount(2);
            ordens[0].Id.Should().Be(42); // Mais recente primeiro
            ordens[1].Id.Should().Be(41);
        }

        [Fact]
        public async Task ObterEquipamentosPorEmailAsync_ComEmailVazioOuNulo_DeveRetornarListaVazia()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var ordens = await engine.ObterEquipamentosPorEmailAsync(null);

            // Assert
            ordens.Should().BeEmpty();
        }

        [Fact]
        public async Task ObterDetalhesSegurosAsync_ComIdEEmailCorrespondente_DeveRetornarOkComPecasEEvidencias()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 5, Nome = "Eduardo", Email = "eduardo@teste.com" };
            var peca = new Peca { Id = 1, Nome = "Memória RAM", QuantidadeEstoque = 10, ValorUnitario = 150 };
            var os = new OrdemServico { Id = 50, Cliente = cliente, DataEntrada = DateTime.UtcNow };
            var osPeca = new OrdemServicoPeca { Id = 1, OrdemServicoId = 50, PecaId = 1, Peca = peca, Quantidade = 2, ValorVenda = 150 };
            var evidencia = new Evidencia { Id = 1, OrdemServicoId = 50, CaminhoArquivo = "/uploads/ev1.jpg", DataUpload = DateTime.UtcNow };

            context.Clientes.Add(cliente);
            context.Pecas.Add(peca);
            context.OrdensServico.Add(os);
            context.OrdemServicoPecas.Add(osPeca);
            context.Evidencias.Add(evidencia);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.ObterDetalhesSegurosAsync(50, "eduardo@teste.com");

            // Assert
            result.Sucesso.Should().BeTrue();
            result.StatusCode.Should().Be(200);
            result.Ordem.Should().NotBeNull();
            result.Ordem!.PecasUtilizadas.Should().HaveCount(1);
            result.Ordem.Evidencias.Should().HaveCount(1);
        }

        [Fact]
        public async Task ObterDetalhesSegurosAsync_ComEmailDivergenteDoDonoDaOS_DeveRetornarProibido()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 6, Nome = "Fabio", Email = "fabio@teste.com" };
            var os = new OrdemServico { Id = 60, Cliente = cliente, DataEntrada = DateTime.UtcNow };

            context.Clientes.Add(cliente);
            context.OrdensServico.Add(os);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act - Outro cliente tentando visualizar a OS
            var result = await engine.ObterDetalhesSegurosAsync(60, "hacker@teste.com");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(403);
            result.Mensagem.Should().Contain("não autorizad");
        }

        [Fact]
        public async Task ObterDetalhesSegurosAsync_ComOsInexistente_DeveRetornarNaoEncontrado()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.ObterDetalhesSegurosAsync(999, "fabio@teste.com");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(404);
        }

        [Fact]
        public async Task AutenticarClientePorEmailAsync_ComEmailValidoCadastrado_DeveRetornarOkComClaimsPrincipalCliente()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var cliente = new Cliente { Id = 7, Nome = "Gisele", Email = "gisele@teste.com" };
            context.Clientes.Add(cliente);
            await context.SaveChangesAsync();

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act - Case insensitive
            var result = await engine.AutenticarClientePorEmailAsync("  GISELE@TESTE.COM  ");

            // Assert
            result.Sucesso.Should().BeTrue();
            result.Cliente.Should().NotBeNull();
            result.Cliente!.Nome.Should().Be("Gisele");
            result.Principal.Should().NotBeNull();
            result.Principal!.FindFirstValue(ClaimTypes.Name).Should().Be("Gisele");
            result.Principal.FindFirstValue(ClaimTypes.Email).Should().Be("gisele@teste.com");
            result.Principal.IsInRole("Cliente").Should().BeTrue();
        }

        [Fact]
        public async Task AutenticarClientePorEmailAsync_ComEmailNaoCadastrado_DeveRetornarFalha()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.AutenticarClientePorEmailAsync("desconhecido@teste.com");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("Nenhum cliente cadastrado");
        }

        [Fact]
        public async Task AutenticarClientePorEmailAsync_ComEmailVazio_DeveRetornarFalha()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = GetDbContext(dbName);

            var loggerMock = new Mock<ILogger<AreaClienteEngine>>();
            var engine = new AreaClienteEngine(context, loggerMock.Object);

            // Act
            var result = await engine.AutenticarClientePorEmailAsync("   ");

            // Assert
            result.Sucesso.Should().BeFalse();
            result.Mensagem.Should().Contain("Informe seu e-mail");
        }
    }
}
