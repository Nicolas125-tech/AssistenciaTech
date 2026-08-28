using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using FluentAssertions;
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
        public async Task MeusEquipamentos_UserSemEmail_DeveRedirecionarParaIndex()
        {
            // Arrange
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                // No email claim
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            // Act
            var result = await _controller.MeusEquipamentos();

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
        }


        [Fact]
        public async Task MeusEquipamentos_UserComEmail_DeveRetornarViewComOrdensDoCliente()
        {
            // Arrange
            string email = "test@example.com";
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim(ClaimTypes.Email, email)
            }, "mock"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            var cliente = new AssistenciaTech.Models.Cliente
            {
                Nome = "Test Client",
                Email = email,
                Cpf = "12345678901",
                Telefone = "11999999999"
            };
            _context.Clientes.Add(cliente);

            var clienteOutro = new AssistenciaTech.Models.Cliente
            {
                Nome = "Other Client",
                Email = "other@example.com",
                Cpf = "98765432109",
                Telefone = "11888888888"
            };
            _context.Clientes.Add(clienteOutro);

            _context.OrdensServico.AddRange(
                new AssistenciaTech.Models.OrdemServico { Cliente = cliente, Equipamento = "Note 1", ProblemaRelatado = "P1", DataEntrada = new DateTime(2023, 1, 10, 0, 0, 0, DateTimeKind.Utc) },
                new AssistenciaTech.Models.OrdemServico { Cliente = cliente, Equipamento = "Note 2", ProblemaRelatado = "P2", DataEntrada = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
                new AssistenciaTech.Models.OrdemServico { Cliente = clienteOutro, Equipamento = "Note 3", ProblemaRelatado = "P3", DataEntrada = new DateTime(2023, 1, 12, 0, 0, 0, DateTimeKind.Utc) }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.MeusEquipamentos();

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            var model = viewResult.Model.Should().BeAssignableTo<System.Collections.Generic.List<AssistenciaTech.Models.OrdemServico>>().Subject;

            model.Should().HaveCount(2);
            // Verify ordering (descending by DataEntrada)
            model[0].Equipamento.Should().Be("Note 2");
            model[1].Equipamento.Should().Be("Note 1");
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
