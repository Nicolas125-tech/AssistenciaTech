using System;
using System.IO;
using System.Threading.Tasks;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class EvidenciaStorageTests : IDisposable
    {
        private readonly string _tempRoot;
        private readonly LocalEvidenciaStorage _storage;

        public EvidenciaStorageTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "AssistenciaTech_EvidenciaTests_" + Guid.NewGuid());
            _storage = new LocalEvidenciaStorage(_tempRoot, new Mock<ILogger<LocalEvidenciaStorage>>().Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        private static IFormFile CreateFormFile(string fileName, byte[] content)
        {
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, content.Length, "fotos", fileName);
        }

        [Fact]
        public async Task SalvarAsync_ComPngValido_SalvaNoDiscoERetornaUrlCanonicaGetEvidencia()
        {
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };
            var file = CreateFormFile("foto.png", pngBytes);

            var result = await _storage.SalvarAsync(file);

            result.Sucesso.Should().BeTrue();
            result.FileName.Should().NotBeNullOrWhiteSpace();
            result.FileName.Should().EndWith(".png");
            result.CaminhoUrl.Should().Be($"/Admin/GetEvidencia?fileName={result.FileName}");

            var leitura = await _storage.AbrirLeituraAsync(result.FileName);
            leitura.Status.Should().Be(EvidenciaReadStatus.Ok);
            leitura.ContentType.Should().Be("image/png");
            await using var stream = leitura.Conteudo!;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.ToArray().Should().Equal(pngBytes);
        }

        [Fact]
        public async Task SalvarAsync_ComAssinaturaFalsificada_RejeitaSemGravarNoDisco()
        {
            var fakeJpg = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // cabeçalho MZ exe disfarçado de .jpg
            var file = CreateFormFile("malware.jpg", fakeJpg);

            var result = await _storage.SalvarAsync(file);

            result.Sucesso.Should().BeFalse();
            result.FileName.Should().BeNull();
            result.CaminhoUrl.Should().BeNull();
        }

        [Theory]
        [InlineData("script.exe")]
        [InlineData("pagina.html")]
        public async Task SalvarAsync_ComExtensaoNaoPermitida_Rejeita(string fileName)
        {
            var file = CreateFormFile(fileName, new byte[] { 0x01, 0x02, 0x03, 0x04 });

            var result = await _storage.SalvarAsync(file);

            result.Sucesso.Should().BeFalse();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("../secret.jpg")]
        [InlineData("..\\secret.jpg")]
        [InlineData("sub/foto.jpg")]
        [InlineData("arquivo.txt")]
        public async Task AbrirLeituraAsync_ComNomeInvalidoOuPathTraversal_RetornaInvalido(string? fileName)
        {
            var result = await _storage.AbrirLeituraAsync(fileName);

            result.Status.Should().Be(EvidenciaReadStatus.Invalido);
            result.Conteudo.Should().BeNull();
        }

        [Fact]
        public async Task AbrirLeituraAsync_ComArquivoInexistente_RetornaNaoEncontrado()
        {
            var result = await _storage.AbrirLeituraAsync("inexistente.jpg");

            result.Status.Should().Be(EvidenciaReadStatus.NaoEncontrado);
        }

        [Fact]
        public async Task RemoverAsync_RemoveArquivoExistenteEIgnoraInexistenteOuTraversal()
        {
            var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
            var save = await _storage.SalvarAsync(CreateFormFile("laudo.pdf", pdfBytes));
            save.Sucesso.Should().BeTrue();

            await _storage.RemoverAsync(save.FileName);

            var aposRemocao = await _storage.AbrirLeituraAsync(save.FileName);
            aposRemocao.Status.Should().Be(EvidenciaReadStatus.NaoEncontrado);

            await _storage.RemoverAsync("../tentativa_traversal.pdf");
            await _storage.RemoverAsync("inexistente.pdf");
        }

        [Theory]
        [InlineData("foto.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
        [InlineData("foto.jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, "image/jpeg")]
        [InlineData("anim.gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, "image/gif")]
        [InlineData("doc.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 }, "application/pdf")]
        public async Task SalvarEAbrirLeitura_SuportaTodasAsExtensoesEContentTypesPermitidos(string originalName, byte[] bytes, string expectedContentType)
        {
            var save = await _storage.SalvarAsync(CreateFormFile(originalName, bytes));

            save.Sucesso.Should().BeTrue();
            var readResult = await _storage.AbrirLeituraAsync(save.FileName);
            readResult.Status.Should().Be(EvidenciaReadStatus.Ok);
            readResult.ContentType.Should().Be(expectedContentType);
            readResult.Conteudo.Should().NotBeNull();
            await readResult.Conteudo!.DisposeAsync();
        }
    }
}
