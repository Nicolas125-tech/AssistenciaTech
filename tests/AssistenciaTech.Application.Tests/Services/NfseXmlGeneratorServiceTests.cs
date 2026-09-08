using System;
using System.Text;
using System.Xml.Linq;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class NfseXmlGeneratorServiceTests
    {
        private readonly NfseXmlGeneratorService _sut;

        public NfseXmlGeneratorServiceTests()
        {
            _sut = new NfseXmlGeneratorService();
        }

        [Fact]
        public void GerarXml_WithValidData_ReturnsValidXmlBytes()
        {
            // Arrange
            var cliente = new Cliente
            {
                Nome = "João Silva",
                Cpf = "123.456.789-00",
                Telefone = "11999999999",
                Email = "joao@example.com"
            };

            var ordemServico = new OrdemServico
            {
                Id = 123,
                Equipamento = "Notebook Dell",
                CustoMaoDeObra = 150.50m,
                Cliente = cliente
            };

            var faturamento = new Faturamento
            {
                OrdemServico = ordemServico,
                ValorISS = 7.52m,
                BaseCalculoISS = 150.50m,
                AliquotaISS = 0.05m
            };

            // Act
            var result = _sut.GerarXml(faturamento);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();

            // Verificar se é um XML válido
            var xmlString = Encoding.UTF8.GetString(result);
            var xDoc = XDocument.Parse(xmlString);

            // Assertions no XML
            XNamespace ns = "http://www.abrasf.org.br/nfse.xsd";
            var root = xDoc.Element(ns + "GerarNfseEnvio");
            root.Should().NotBeNull();

            var descriminacao = root.Descendants(ns + "Discriminacao").First().Value;
            descriminacao.Should().Contain("123");
            descriminacao.Should().Contain("Notebook Dell");

            var cpf = root.Descendants(ns + "Cpf").First().Value;
            cpf.Should().Be("12345678900"); // Sem formatação

            var razaoSocial = root.Descendants(ns + "RazaoSocial").First().Value;
            razaoSocial.Should().Be("João Silva");

            var valorServicos = root.Descendants(ns + "ValorServicos").First().Value;
            valorServicos.Should().Be("150.50");

            var valorIss = root.Descendants(ns + "ValorIss").First().Value;
            valorIss.Should().Be("7.52");

            var baseCalculo = root.Descendants(ns + "BaseCalculo").First().Value;
            baseCalculo.Should().Be("150.50");

            var aliquota = root.Descendants(ns + "Aliquota").First().Value;
            aliquota.Should().Be("5.00");
        }

        [Fact]
        public void GerarXml_WithNullCpf_ReplacesWithZeros()
        {
            // Arrange
            var cliente = new Cliente
            {
                Nome = "Empresa Sem CPF",
                Cpf = null // Embora o modelo marque como Required, testamos para resiliência na geração do XML
            };

            var faturamento = new Faturamento
            {
                OrdemServico = new OrdemServico { Cliente = cliente }
            };

            // Act
            var result = _sut.GerarXml(faturamento);
            var xmlString = Encoding.UTF8.GetString(result);
            var xDoc = XDocument.Parse(xmlString);

            XNamespace ns = "http://www.abrasf.org.br/nfse.xsd";
            var cpf = xDoc.Descendants(ns + "Cpf").First().Value;

            // Assert
            cpf.Should().Be("00000000000");
        }

        [Fact]
        public void GerarXml_WithNullFaturamento_ThrowsArgumentException()
        {
            // Arrange
            Faturamento? faturamento = null;

            // Act
            Action act = () => _sut.GerarXml(faturamento!);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Dados incompletos para geração de NFS-e (Faturamento, OS ou Cliente nulos).");
        }

        [Fact]
        public void GerarXml_WithNullOrdemServico_ThrowsArgumentException()
        {
            // Arrange
            var faturamento = new Faturamento
            {
                OrdemServico = null
            };

            // Act
            Action act = () => _sut.GerarXml(faturamento);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Dados incompletos para geração de NFS-e (Faturamento, OS ou Cliente nulos).");
        }

        [Fact]
        public void GerarXml_WithNullCliente_ThrowsArgumentException()
        {
            // Arrange
            var faturamento = new Faturamento
            {
                OrdemServico = new OrdemServico { Cliente = null }
            };

            // Act
            Action act = () => _sut.GerarXml(faturamento);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("Dados incompletos para geração de NFS-e (Faturamento, OS ou Cliente nulos).");
        }
    }
}
