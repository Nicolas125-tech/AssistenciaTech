using System;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class OrdemServicoLifecycleTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<IEstoqueEngine> _estoqueMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly Mock<IWebHostEnvironment> _envMock;
        private readonly Mock<ILogger<OrdemServicoLifecycle>> _loggerMock;
        private readonly OrdemServicoLifecycle _lifecycle;

        public OrdemServicoLifecycleTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _estoqueMock = new Mock<IEstoqueEngine>();
            _estoqueMock.Setup(e => e.DeduzirEstoqueAsync(It.IsAny<int>())).ReturnsAsync(EstoqueResult.Ok());
            _estoqueMock.Setup(e => e.RestaurarEstoqueAsync(It.IsAny<int>())).ReturnsAsync(EstoqueResult.Ok());
            _notificationMock = new Mock<INotificationService>();
            _envMock = new Mock<IWebHostEnvironment>();
            _envMock.Setup(e => e.ContentRootPath).Returns(AppDomain.CurrentDomain.BaseDirectory);
            _loggerMock = new Mock<ILogger<OrdemServicoLifecycle>>();

            _lifecycle = new OrdemServicoLifecycle(
                _context,
                _estoqueMock.Object,
                _notificationMock.Object,
                _envMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task RegistrarEntradaAsync_WhenValidDto_CreatesOrderAndReturnsOk()
        {
            var cliente = new Cliente { Id = 10, Nome = "João da Silva", Cpf = "12345678901", Telefone = "4399999999" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var dto = new OrdemServicoCreateDto
            {
                ClienteId = 10,
                Equipamento = "Notebook Dell G15",
                ProblemaRelatado = "Não liga",
                Prioridade = 1,
                CustoPecas = 100m,
                CustoMaoDeObra = 200m
            };

            var result = await _lifecycle.RegistrarEntradaAsync(dto, "operador_teste");

            result.Sucesso.Should().BeTrue();
            result.Ordem.Should().NotBeNull();
            result.Ordem!.Status.Should().Be(WorkflowStatus.Recebido);
            result.Ordem.ValorOrcamento.Should().Be(300m);

            var saved = await _context.OrdensServico.FindAsync(result.Ordem.Id);
            saved.Should().NotBeNull();
        }

        [Fact]
        public async Task AtualizarOrdemAsync_WhenTransitioningToConcluido_DeductsStockAndNotifies()
        {
            var cliente = new Cliente { Id = 20, Nome = "Maria Souza", Cpf = "98765432100", Telefone = "43988888888" };
            var os = new OrdemServico
            {
                Id = 101,
                ClienteId = 20,
                Cliente = cliente,
                Equipamento = "MacBook Pro",
                ProblemaRelatado = "Bateria estufada",
                Status = WorkflowStatus.EmReparo,
                DataEntrada = DateTime.UtcNow.AddDays(-2)
            };
            _context.Clientes.Add(cliente);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var update = new OrdemServico
            {
                Id = 101,
                ClienteId = 20,
                Equipamento = "MacBook Pro",
                ProblemaRelatado = "Bateria estufada",
                Status = WorkflowStatus.Concluido,
                LaudoTecnico = "Bateria trocada e calibrada"
            };

            var result = await _lifecycle.AtualizarOrdemAsync(101, update);

            result.Sucesso.Should().BeTrue();
            os.Status.Should().Be(WorkflowStatus.Concluido);
            os.DataConclusao.Should().NotBeNull();

            _estoqueMock.Verify(e => e.DeduzirEstoqueAsync(101), Times.Once);
            _notificationMock.Verify(n => n.EnviarNotificacaoStatusAsync(cliente, os, WorkflowStatus.EmReparo), Times.Once);
        }

        [Fact]
        public async Task AtualizarOrdemAsync_WhenLeavingConcluido_RestoresStock()
        {
            var cliente = new Cliente { Id = 30, Nome = "Carlos", Cpf = "11122233344", Telefone = "43977777777" };
            var os = new OrdemServico
            {
                Id = 102,
                ClienteId = 30,
                Cliente = cliente,
                Equipamento = "PC Gamer",
                ProblemaRelatado = "Artefatos de vídeo",
                Status = WorkflowStatus.Concluido,
                DataEntrada = DateTime.UtcNow.AddDays(-3),
                DataConclusao = DateTime.UtcNow.AddHours(-1)
            };
            _context.Clientes.Add(cliente);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var update = new OrdemServico
            {
                Id = 102,
                ClienteId = 30,
                Equipamento = "PC Gamer",
                ProblemaRelatado = "Artefatos de vídeo",
                Status = WorkflowStatus.EmReparo
            };

            var result = await _lifecycle.AtualizarOrdemAsync(102, update);

            result.Sucesso.Should().BeTrue();
            os.Status.Should().Be(WorkflowStatus.EmReparo);
            os.DataConclusao.Should().BeNull();

            _estoqueMock.Verify(e => e.RestaurarEstoqueAsync(102), Times.Once);
        }

        [Fact]
        public async Task AtualizarOrdemAsync_WhenTransitioningToEntregueWithUnreturnedBackup_FailsDomainValidation()
        {
            var cliente = new Cliente { Id = 40, Nome = "Empresa X", Cpf = "12312312312", Telefone = "43966666666" };
            var backup = new EquipamentoBackup { Id = 5, Descricao = "Notebook Reserva Dell", Disponivel = false };
            var os = new OrdemServico
            {
                Id = 103,
                ClienteId = 40,
                Cliente = cliente,
                Equipamento = "Servidor Dell",
                ProblemaRelatado = "Fonte queimada",
                Status = WorkflowStatus.Concluido,
                EquipamentoBackupId = 5,
                DataConclusao = DateTime.UtcNow
            };
            _context.Clientes.Add(cliente);
            _context.EquipamentosBackup.Add(backup);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var update = new OrdemServico
            {
                Id = 103,
                ClienteId = 40,
                Equipamento = "Servidor Dell",
                ProblemaRelatado = "Fonte queimada",
                Status = WorkflowStatus.Entregue,
                EquipamentoBackupId = 5
            };

            var result = await _lifecycle.AtualizarOrdemAsync(103, update);

            result.Sucesso.Should().BeFalse();
            result.Erros.Should().NotBeEmpty();
            os.Status.Should().Be(WorkflowStatus.Concluido);
            os.DataEntregaCliente.Should().BeNull();
        }

        [Fact]
        public async Task AtualizarOrdemAsync_WhenSwappingBackupDevice_UpdatesAvailabilityFlags()
        {
            var cliente = new Cliente { Id = 1, Nome = "Cliente Teste", Cpf = "12345678900", Telefone = "4399999999" };
            var backupAntigo = new EquipamentoBackup { Id = 1, Descricao = "Backup 1", Disponivel = false };
            var backupNovo = new EquipamentoBackup { Id = 2, Descricao = "Backup 2", Disponivel = true };
            var os = new OrdemServico
            {
                Id = 104,
                ClienteId = 1,
                Cliente = cliente,
                Equipamento = "Lenovo ThinkPad",
                ProblemaRelatado = "Teclado falhando",
                Status = WorkflowStatus.EmAnalise,
                EquipamentoBackupId = 1
            };
            _context.Clientes.Add(cliente);
            _context.EquipamentosBackup.AddRange(backupAntigo, backupNovo);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var update = new OrdemServico
            {
                Id = os.Id,
                ClienteId = 1,
                Equipamento = "Lenovo ThinkPad",
                ProblemaRelatado = "Teclado falhando",
                Status = WorkflowStatus.EmAnalise,
                EquipamentoBackupId = 2
            };

            var result = await _lifecycle.AtualizarOrdemAsync(os.Id, update);

            result.Sucesso.Should().BeTrue(result.Mensagem ?? string.Join("; ", result.Erros));
            backupAntigo.Disponivel.Should().BeTrue();
            backupNovo.Disponivel.Should().BeFalse();
        }

        [Fact]
        public async Task AvancarStatusAsync_WhenCalled_ExecutesStateTransitionsAndSaves()
        {
            var cliente = new Cliente { Id = 50, Nome = "Lucas", Cpf = "55566677788", Telefone = "43955555555" };
            var os = new OrdemServico
            {
                Id = 105,
                ClienteId = 50,
                Cliente = cliente,
                Equipamento = "Impressora HP Laser",
                ProblemaRelatado = "Atolamento de papel",
                Status = WorkflowStatus.EmReparo
            };
            _context.Clientes.Add(cliente);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var result = await _lifecycle.AvancarStatusAsync(105, WorkflowStatus.Concluido, "Mecanismo tracionador limpo", "tecnico_mobile");

            result.Sucesso.Should().BeTrue();
            os.Status.Should().Be(WorkflowStatus.Concluido);
            os.LaudoTecnico.Should().Be("Mecanismo tracionador limpo");
            _estoqueMock.Verify(e => e.DeduzirEstoqueAsync(105), Times.Once);
            _notificationMock.Verify(n => n.EnviarNotificacaoStatusAsync(cliente, os, WorkflowStatus.EmReparo), Times.Once);
        }

        [Fact]
        public async Task AtualizarOrdemAsync_WhenNotificationThrows_DoesNotFailOperation()
        {
            var cliente = new Cliente { Id = 60, Nome = "Ana", Cpf = "99988877766", Telefone = "43944444444" };
            var os = new OrdemServico
            {
                Id = 106,
                ClienteId = 60,
                Cliente = cliente,
                Equipamento = "Tablet Samsung",
                ProblemaRelatado = "Touch quebrado",
                Status = WorkflowStatus.EmReparo
            };
            _context.Clientes.Add(cliente);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            _notificationMock.Setup(n => n.EnviarNotificacaoStatusAsync(It.IsAny<Cliente>(), It.IsAny<OrdemServico>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Telegram API timeout"));

            var update = new OrdemServico
            {
                Id = 106,
                ClienteId = 60,
                Equipamento = "Tablet Samsung",
                ProblemaRelatado = "Touch quebrado",
                Status = WorkflowStatus.Concluido
            };

            var result = await _lifecycle.AtualizarOrdemAsync(106, update);

            result.Sucesso.Should().BeTrue();
            os.Status.Should().Be(WorkflowStatus.Concluido);
        }
    }
}
