using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Services
{
    public class LocalEvidenciaStorage : IEvidenciaStorage
    {
        private sealed record EvidenciaFormatSpec(string ContentType, byte[][] Signatures);

        private static readonly Dictionary<string, EvidenciaFormatSpec> _supportedFormats = new(StringComparer.OrdinalIgnoreCase)
        {
            { ".jpg", new("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }) },
            { ".jpeg", new("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }) },
            { ".png", new("image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }) },
            { ".gif", new("image/gif", new[] { new byte[] { 0x47, 0x49, 0x46, 0x38 } }) },
            { ".pdf", new("application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } }) }
        };

        private readonly string _uploadsFolder;
        private readonly string _uploadsFolderPrefix;
        private readonly ILogger<LocalEvidenciaStorage> _logger;

        [ActivatorUtilitiesConstructor]
        public LocalEvidenciaStorage(IWebHostEnvironment env, ILogger<LocalEvidenciaStorage> logger)
            : this(env?.ContentRootPath ?? AppDomain.CurrentDomain.BaseDirectory, logger)
        {
        }

        public LocalEvidenciaStorage(string contentRootPath, ILogger<LocalEvidenciaStorage> logger)
        {
            var root = string.IsNullOrWhiteSpace(contentRootPath)
                ? AppDomain.CurrentDomain.BaseDirectory
                : contentRootPath;
            _uploadsFolder = Path.GetFullPath(Path.Combine(root, "SecureUploads", "Evidencias"));
            _uploadsFolderPrefix = _uploadsFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            _logger = logger;
        }

        public async Task<EvidenciaSaveResult> SalvarAsync(IFormFile arquivo, CancellationToken ct = default)
        {
            if (arquivo == null || arquivo.Length == 0)
            {
                return EvidenciaSaveResult.Falha("Arquivo vazio ou não informado.");
            }

            var extension = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (!_supportedFormats.TryGetValue(extension, out var spec))
            {
                return EvidenciaSaveResult.Falha($"Extensão de arquivo não permitida: {extension}.");
            }

            if (!await IsValidFileSignatureAsync(arquivo, spec.Signatures, ct))
            {
                return EvidenciaSaveResult.Falha("Assinatura binária incompatível com a extensão declarada.");
            }

            Directory.CreateDirectory(_uploadsFolder);

            string uniqueFileName = $"{Guid.NewGuid()}{extension}";
            string filePath = Path.GetFullPath(Path.Combine(_uploadsFolder, uniqueFileName));

            await using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await arquivo.CopyToAsync(fileStream, ct);
            }

            return EvidenciaSaveResult.Ok(uniqueFileName, $"/Admin/GetEvidencia?fileName={uniqueFileName}");
        }

        public Task<EvidenciaReadResult> AbrirLeituraAsync(string? fileName)
        {
            if (!TryResolveSafePath(fileName, out var fullPath, out var spec))
            {
                return Task.FromResult(EvidenciaReadResult.Invalido());
            }

            if (!File.Exists(fullPath))
            {
                return Task.FromResult(EvidenciaReadResult.NaoEncontrado());
            }

            Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return Task.FromResult(EvidenciaReadResult.Ok(stream, spec.ContentType));
        }

        public Task RemoverAsync(string? fileName)
        {
            if (!TryResolveSafePath(fileName, out var fullPath, out _))
            {
                return Task.CompletedTask;
            }

            try
            {
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Falha ao remover arquivo de evidência {FileName}.", fileName);
            }

            return Task.CompletedTask;
        }

        private bool TryResolveSafePath(string? fileName, out string fullPath, out EvidenciaFormatSpec spec)
        {
            fullPath = string.Empty;
            spec = null!;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            var safeFileName = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(safeFileName) || !string.Equals(safeFileName, fileName, StringComparison.Ordinal))
            {
                return false;
            }

            var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
            if (!_supportedFormats.TryGetValue(extension, out var matchedSpec))
            {
                return false;
            }

            spec = matchedSpec;
            fullPath = Path.GetFullPath(Path.Combine(_uploadsFolder, safeFileName));
            return fullPath.StartsWith(_uploadsFolderPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, byte[][] expectedSignatures, CancellationToken ct)
        {
            var maxSignatureLength = expectedSignatures.Max(s => s.Length);
            var minSignatureLength = expectedSignatures.Min(s => s.Length);
            var headerBytes = new byte[maxSignatureLength];

            await using var stream = file.OpenReadStream();
            int totalRead = 0;
            while (totalRead < maxSignatureLength)
            {
                int read = await stream.ReadAsync(headerBytes.AsMemory(totalRead, maxSignatureLength - totalRead), ct);
                if (read == 0) break;
                totalRead += read;
            }

            if (totalRead < minSignatureLength)
            {
                return false;
            }

            return expectedSignatures.Any(signature =>
                totalRead >= signature.Length &&
                headerBytes.AsSpan(0, signature.Length).SequenceEqual(signature));
        }
    }
}
