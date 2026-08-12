using System;
using System.Threading.Tasks;
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

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
