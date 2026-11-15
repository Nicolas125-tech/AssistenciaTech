using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.DTOs;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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
        private readonly Mock<IEvidenciaStorage> _evidenciaStorageMock;
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
            _evidenciaStorageMock = new Mock<IEvidenciaStorage>();
            _loggerMock = new Mock<ILogger<OrdemServicoLifecycle>>();

            _lifecycle = new OrdemServicoLifecycle(
                _context,
                _estoqueMock.Object,
                _notificationMock.Object,
                _evidenciaStorageMock.Object,
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

        [Fact]
        public async Task AtualizarOrdemAsync_ComUploadValido_RegistraUrlCanonicaGetEvidencia()
        {
            var cliente = new Cliente { Id = 70, Nome = "Marcos", Cpf = "12312312300", Telefone = "43911111111" };
            var os = new OrdemServico
            {
                Id = 107,
                ClienteId = 70,
                Cliente = cliente,
                Equipamento = "Dell XPS",
                ProblemaRelatado = "Tela piscando",
                Status = WorkflowStatus.EmReparo
            };
            _context.Clientes.Add(cliente);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var fileMock = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "fotos", "tela.jpg");
            var fotos = new FormFileCollection { fileMock };

            _evidenciaStorageMock
                .Setup(s => s.SalvarAsync(fileMock, It.IsAny<CancellationToken>()))
                .ReturnsAsync(EvidenciaSaveResult.Ok("abc.jpg", "/Admin/GetEvidencia?fileName=abc.jpg"));

            var update = new OrdemServico
            {
                Id = 107,
                ClienteId = 70,
                Equipamento = "Dell XPS",
                ProblemaRelatado = "Tela piscando",
                Status = WorkflowStatus.EmReparo
            };

            var result = await _lifecycle.AtualizarOrdemAsync(107, update, fotos);

            result.Sucesso.Should().BeTrue();
            os.Evidencias.Should().ContainSingle(e => e.CaminhoArquivo == "/Admin/GetEvidencia?fileName=abc.jpg");
        }

        [Fact]
        public async Task AtualizarOrdemAsync_QuandoSaveChangesFalha_FazRollbackDosArquivosSalvos()
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: "Rollback_" + Guid.NewGuid())
                .Options;

            using (var seed = new AppDbContext(dbOptions))
            {
                seed.Clientes.Add(new Cliente { Id = 71, Nome = "Marina", Cpf = "00011122233", Telefone = "43922222222" });
                seed.OrdensServico.Add(new OrdemServico { Id = 108, ClienteId = 71, Equipamento = "iPad", ProblemaRelatado = "Sem som", Status = WorkflowStatus.EmReparo });
                await seed.SaveChangesAsync();
            }

            await using var failingContext = new FailingSaveDbContext(dbOptions);
            var storageMock = new Mock<IEvidenciaStorage>();
            var fileMock = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "fotos", "foto.png");
            storageMock
                .Setup(s => s.SalvarAsync(fileMock, It.IsAny<CancellationToken>()))
                .ReturnsAsync(EvidenciaSaveResult.Ok("rollback.png", "/Admin/GetEvidencia?fileName=rollback.png"));

            var lifecycle = new OrdemServicoLifecycle(
                failingContext,
                _estoqueMock.Object,
                _notificationMock.Object,
                storageMock.Object,
                _loggerMock.Object);

            var update = new OrdemServico { Id = 108, ClienteId = 71, Equipamento = "iPad", ProblemaRelatado = "Sem som", Status = WorkflowStatus.EmReparo };

            Func<Task> act = () => lifecycle.AtualizarOrdemAsync(108, update, new FormFileCollection { fileMock });

            await act.Should().ThrowAsync<InvalidOperationException>();
            storageMock.Verify(s => s.RemoverAsync("rollback.png"), Times.Once);
        }

        [Fact]
        public async Task ObterDadosEdicaoAsync_RetornaLookupsAtualizadosSemCacheObsoleto()
        {
            var cliente = new Cliente { Id = 80, Nome = "Empresa Alfa", Cpf = "11223344556", Telefone = "4333334444" };
            var tecnicoAtivo = new Tecnico { Id = 1, Nome = "Carlos", Ativo = true };
            var tecnicoInativo = new Tecnico { Id = 2, Nome = "Inativo", Ativo = false };
            var backupAtual = new EquipamentoBackup { Id = 10, Descricao = "Backup Emprestado", Disponivel = false };
            var backupLivre = new EquipamentoBackup { Id = 11, Descricao = "Backup Livre", Disponivel = true };
            var backupTerceiro = new EquipamentoBackup { Id = 12, Descricao = "Backup Outro Cliente", Disponivel = false };
            var contrato = new Contrato { Id = 5, ClienteId = 80, HorasSLA = 8, ValorMensal = 500m };

            var os = new OrdemServico
            {
                Id = 200,
                ClienteId = 80,
                Equipamento = "ThinkPad",
                ProblemaRelatado = "SSD",
                EquipamentoBackupId = 10
            };

            _context.Clientes.Add(cliente);
            _context.Tecnicos.AddRange(tecnicoAtivo, tecnicoInativo);
            _context.EquipamentosBackup.AddRange(backupAtual, backupLivre, backupTerceiro);
            _context.Contratos.Add(contrato);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var primeiraLeitura = await _lifecycle.ObterDadosEdicaoAsync(200);
            primeiraLeitura.Should().NotBeNull();
            primeiraLeitura!.Lookups.Tecnicos.Should().ContainSingle(t => t.Id == 1 && t.Descricao == "Carlos");
            primeiraLeitura.Lookups.EquipamentosBackup.Select(b => b.Id).Should().BeEquivalentTo(new[] { 10, 11 });
            primeiraLeitura.Lookups.Contratos.Should().ContainSingle(c => c.Id == 5);

            // Mutação externa (ex: EquipamentoBackupController liberou backupTerceiro imediatamente)
            backupTerceiro.Disponivel = true;
            await _context.SaveChangesAsync();

            var segundaLeitura = await _lifecycle.ObterDadosEdicaoAsync(200);
            segundaLeitura!.Lookups.EquipamentosBackup.Select(b => b.Id).Should().BeEquivalentTo(new[] { 10, 11, 12 });
        }

        [Fact]
        public async Task ObterOrdemParaImpressaoAsync_IncluiClienteTecnicoResponsavelEPecas()
        {
            var cliente = new Cliente { Id = 81, Nome = "Joana", Cpf = "22334455667", Telefone = "43900001111" };
            var tecnico = new Tecnico { Id = 3, Nome = "Roberto Tec", Ativo = true };
            var peca = new Peca { Id = 4, Nome = "Fonte 65W", QuantidadeEstoque = 5, ValorUnitario = 120m };
            var os = new OrdemServico
            {
                Id = 201,
                ClienteId = 81,
                TecnicoId = 3,
                Equipamento = "Acer Nitro",
                ProblemaRelatado = "Fonte"
            };
            os.PecasUtilizadas.Add(new OrdemServicoPeca { PecaId = 4, Quantidade = 1, ValorVenda = 120m });

            _context.Clientes.Add(cliente);
            _context.Tecnicos.Add(tecnico);
            _context.Pecas.Add(peca);
            _context.OrdensServico.Add(os);
            await _context.SaveChangesAsync();

            var impressao = await _lifecycle.ObterOrdemParaImpressaoAsync(201);

            impressao.Should().NotBeNull();
            impressao!.Cliente.Should().NotBeNull();
            impressao.TecnicoResponsavel.Should().NotBeNull();
            impressao.TecnicoResponsavel!.Nome.Should().Be("Roberto Tec");
            impressao.PecasUtilizadas.Should().ContainSingle(p => p.Peca != null && p.Peca.Nome == "Fonte 65W");
        }

        [Fact]
        public async Task ExportarOrdensCsvAsync_ProcessaMaisDe100RegistrosEmLotesEOrdemDecrescente()
        {
            var cliente = new Cliente { Id = 82, Nome = "Cliente Lote", Cpf = "33445566778", Telefone = "43912345678" };
            _context.Clientes.Add(cliente);
            for (int i = 1; i <= 105; i++)
            {
                _context.OrdensServico.Add(new OrdemServico
                {
                    Id = 1000 + i,
                    ClienteId = 82,
                    Equipamento = $"Equip {i}",
                    ProblemaRelatado = "Teste",
                    Status = WorkflowStatus.Recebido,
                    ValorOrcamento = 50m + i,
                    DataEntrada = new DateTime(2026, 1, 10)
                });
            }
            await _context.SaveChangesAsync();

            using var ms = new MemoryStream();
            await _lifecycle.ExportarOrdensCsvAsync(ms);

            var csv = Encoding.UTF8.GetString(ms.ToArray());
            var linhas = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            linhas.Should().HaveCount(106); // cabeçalho + 105 registros
            linhas[0].Should().Be("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");
            linhas[1].Should().StartWith("1105,\"Cliente Lote\",\"Equip 105\"");
        }

        [Fact]
        public async Task ExcluirOrdemAsync_BloqueiaQuandoPossuiBackupAlocadoOuDependenciasEDeletaQuandoLimpa()
        {
            var cliente = new Cliente { Id = 83, Nome = "Cliente Delete", Cpf = "44556677889", Telefone = "43987654321" };
            var backup = new EquipamentoBackup { Id = 20, Descricao = "Backup Alocado", Disponivel = false };
            var osComBackup = new OrdemServico { Id = 301, ClienteId = 83, Equipamento = "Dell", ProblemaRelatado = "Bateria", EquipamentoBackupId = 20 };
            var osLimpa = new OrdemServico { Id = 302, ClienteId = 83, Equipamento = "HP", ProblemaRelatado = "Tela" };

            _context.Clientes.Add(cliente);
            _context.EquipamentosBackup.Add(backup);
            _context.OrdensServico.AddRange(osComBackup, osLimpa);
            await _context.SaveChangesAsync();

            var resBackup = await _lifecycle.ExcluirOrdemAsync(301);
            resBackup.Sucesso.Should().BeFalse();
            resBackup.Mensagem.Should().Contain("Backup");
            (await _context.OrdensServico.FindAsync(301)).Should().NotBeNull();

            var resLimpa = await _lifecycle.ExcluirOrdemAsync(302);
            resLimpa.Sucesso.Should().BeTrue();
            (await _context.OrdensServico.FindAsync(302)).Should().BeNull();

            var resInexistente = await _lifecycle.ExcluirOrdemAsync(999);
            resInexistente.Sucesso.Should().BeFalse();
            resInexistente.NaoEncontrada.Should().BeTrue();
        }

        [Fact]
        public async Task ExcluirOrdemAsync_BloqueiaQuandoPossuiEvidenciasPecasOuFaturamento()
        {
            var cliente = new Cliente { Id = 84, Nome = "Cliente Dep", Cpf = "55667788990", Telefone = "43911112222" };
            var peca = new Peca { Id = 10, Nome = "SSD", QuantidadeEstoque = 5, ValorUnitario = 200m };

            var osEvidencia = new OrdemServico { Id = 303, ClienteId = 84, Equipamento = "Mac", ProblemaRelatado = "Tela" };
            osEvidencia.Evidencias.Add(new Evidencia { CaminhoArquivo = "/Admin/GetEvidencia?fileName=ev.jpg" });

            var osPeca = new OrdemServico { Id = 304, ClienteId = 84, Equipamento = "Dell", ProblemaRelatado = "Disco" };
            osPeca.PecasUtilizadas.Add(new OrdemServicoPeca { PecaId = 10, Quantidade = 1, ValorVenda = 200m });

            var osFaturada = new OrdemServico { Id = 305, ClienteId = 84, Equipamento = "Lenovo", ProblemaRelatado = "Fonte" };

            _context.Clientes.Add(cliente);
            _context.Pecas.Add(peca);
            _context.OrdensServico.AddRange(osEvidencia, osPeca, osFaturada);
            await _context.SaveChangesAsync();

            _context.Faturamentos.Add(new Faturamento { OrdemServicoId = 305, ValorTotal = 200m, DataVencimento = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            (await _lifecycle.ExcluirOrdemAsync(303)).Sucesso.Should().BeFalse();
            (await _lifecycle.ExcluirOrdemAsync(304)).Sucesso.Should().BeFalse();
            (await _lifecycle.ExcluirOrdemAsync(305)).Sucesso.Should().BeFalse();
        }

        [Fact]
        public async Task ObterClientesParaSelecaoAsync_RetornaListaFormatada()
        {
            _context.Clientes.Add(new Cliente { Id = 85, Nome = "Fernanda", Cpf = "77788899900", Telefone = "43933334444" });
            await _context.SaveChangesAsync();

            var itens = await _lifecycle.ObterClientesParaSelecaoAsync();

            itens.Should().ContainSingle(i => i.Id == 85 && i.Descricao == "Fernanda - CPF: 77788899900 - Tel: 43933334444");
        }

        [Fact]
        public async Task ExportarOrdensCsvAsync_ComBancoVazioOuClienteNulo_NaoFalha()
        {
            using var msVazio = new MemoryStream();
            await _lifecycle.ExportarOrdensCsvAsync(msVazio);
            var csvVazio = Encoding.UTF8.GetString(msVazio.ToArray()).TrimEnd();
            csvVazio.Should().Be("Id,Cliente,Equipamento,Data Entrada,Status,Valor Orçamento");

            _context.OrdensServico.Add(new OrdemServico
            {
                Id = 400,
                ClienteId = 999,
                Equipamento = "Sem Cliente",
                ProblemaRelatado = "Teste",
                Status = WorkflowStatus.Recebido,
                ValorOrcamento = 75m,
                DataEntrada = new DateTime(2026, 2, 1)
            });
            await _context.SaveChangesAsync();

            using var msComNulo = new MemoryStream();
            await _lifecycle.ExportarOrdensCsvAsync(msComNulo);
            var csvComNulo = Encoding.UTF8.GetString(msComNulo.ToArray());
            csvComNulo.Should().Contain("400,\"\",\"Sem Cliente\",01/02/2026,Recebido,75");
        }

        private sealed class FailingSaveDbContext : AppDbContext
        {
            public FailingSaveDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Simulated DB write failure");
        }
    }
}
