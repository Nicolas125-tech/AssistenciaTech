using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class ConsultaControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly ConsultaController _controller;

        public ConsultaControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _controller = new ConsultaController(_context);
        }

        [Fact]
        public void Index_DeveRetornarViewResult()
        {
            // Arrange

            // Act
            var result = _controller.Index(null);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Which;
            viewResult.ViewName.Should().BeNull(); // Returns default view
        }

        [Fact]
        public async Task Status_ComCpfMuitoLongo_DeveRetornarErroDeValidacao_PrevenindoDoS()
        {
            // Arrange
            int numeroOS = 1;
            // Cria um CPF intencionalmente longo para testar a validação de limite de tamanho
            string cpfLongo = new string('9', 100);

            // Act
            var result = await _controller.Status(numeroOS, cpfLongo);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            string erro = _controller.ViewBag.Erro;
            erro.Should().Be("Por favor, preencha o número da OS e o CPF.");
        }

        [Fact]
        public async Task Status_ComOrdemInexistente_DeveRetornarErro()
        {
            // Arrange
            int numeroOS = 999;
            string cpf = "12345678901";

            // Act
            var result = await _controller.Status(numeroOS, cpf);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            string erro = _controller.ViewBag.Erro;
            erro.Should().Be("Ordem de Serviço não encontrada ou CPF inválido.");
        }


        [Fact]
        public async Task Status_ComNumeroOSInvalido_DeveRetornarErroDeValidacao()
        {
            // Arrange
            int numeroOS = 0;
            string cpf = "12345678901";

            // Act
            var result = await _controller.Status(numeroOS, cpf);

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            viewResult.ViewName.Should().Be("Index");
            string erro = _controller.ViewBag.Erro;
            erro.Should().Be("Por favor, preencha o número da OS e o CPF.");
        }


        [Fact]
        public async Task MeusEquipamentos_SemEmail_DeveRedirecionarParaIndex()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            // Act
            var result = await _controller.MeusEquipamentos();

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }

        [Fact]
        public async Task MeusEquipamentos_ComEmail_DeveRetornarViewComOrdensDoCliente()
        {
            // Arrange
            var userEmail = "cliente@teste.com";

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Email, userEmail)
                    }))
                }
            };

            var clienteLogado = new Cliente
            {
                Nome = "Cliente Logado",
                Cpf = "11111111111",
                Telefone = "11999999999",
                Email = userEmail
            };

            var outroCliente = new Cliente
            {
                Nome = "Outro Cliente",
                Cpf = "22222222222",
                Telefone = "11888888888",
                Email = "outro@teste.com"
            };

            _context.Clientes.AddRange(clienteLogado, outroCliente);
            await _context.SaveChangesAsync();

            var os1 = new OrdemServico { ClienteId = clienteLogado.Id, Equipamento = "PC", ProblemaRelatado = "P1", DataEntrada = System.DateTime.UtcNow.AddDays(-2), Status = "Recebido" };
            var os2 = new OrdemServico { ClienteId = clienteLogado.Id, Equipamento = "Note", ProblemaRelatado = "P2", DataEntrada = System.DateTime.UtcNow.AddDays(-1), Status = "Recebido" };
            var os3 = new OrdemServico { ClienteId = outroCliente.Id, Equipamento = "Tablet", ProblemaRelatado = "P3", DataEntrada = System.DateTime.UtcNow, Status = "Recebido" };

            _context.OrdensServico.AddRange(os1, os2, os3);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.MeusEquipamentos();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var ordens = viewResult.Model.Should().BeAssignableTo<IEnumerable<OrdemServico>>().Subject.ToList();

            ordens.Should().HaveCount(2);
            ordens.Should().Contain(o => o.Id == os1.Id);
            ordens.Should().Contain(o => o.Id == os2.Id);

            // Verifica a ordenação (o mais recente primeiro)
            ordens[0].Id.Should().Be(os2.Id);
            ordens[1].Id.Should().Be(os1.Id);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
