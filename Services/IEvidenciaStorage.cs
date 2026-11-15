using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AssistenciaTech.Services
{
    public enum EvidenciaReadStatus
    {
        Ok,
        Invalido,
        NaoEncontrado
    }

    public class EvidenciaSaveResult
    {
        public bool Sucesso { get; init; }
        public string? FileName { get; init; }
        public string? CaminhoUrl { get; init; }
        public string? Erro { get; init; }

        public static EvidenciaSaveResult Ok(string fileName, string caminhoUrl) =>
            new() { Sucesso = true, FileName = fileName, CaminhoUrl = caminhoUrl };

        public static EvidenciaSaveResult Falha(string erro) =>
            new() { Sucesso = false, Erro = erro };
    }

    public class EvidenciaReadResult
    {
        public EvidenciaReadStatus Status { get; init; }
        public Stream? Conteudo { get; init; }
        public string? ContentType { get; init; }

        public static EvidenciaReadResult Ok(Stream conteudo, string contentType) =>
            new()
            {
                Status = EvidenciaReadStatus.Ok,
                Conteudo = conteudo,
                ContentType = contentType
            };

        public static EvidenciaReadResult Invalido() =>
            new() { Status = EvidenciaReadStatus.Invalido };

        public static EvidenciaReadResult NaoEncontrado() =>
            new() { Status = EvidenciaReadStatus.NaoEncontrado };
    }

    /// <summary>
    /// Seam profundo para armazenamento seguro, validação binária e leitura de arquivos de evidência de OS.
    /// </summary>
    public interface IEvidenciaStorage
    {
        Task<EvidenciaSaveResult> SalvarAsync(IFormFile arquivo, CancellationToken ct = default);
        Task<EvidenciaReadResult> AbrirLeituraAsync(string? fileName);
        Task RemoverAsync(string? fileName);
    }
}
