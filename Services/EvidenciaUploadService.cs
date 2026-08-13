using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public class EvidenciaUploadService : IEvidenciaUploadService
    {
        private readonly IWebHostEnvironment _env;

        public EvidenciaUploadService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<List<Evidencia>> ProcessUploadsAsync(IFormFileCollection fotos)
        {
            var evidencias = new List<Evidencia>();

            if (fotos != null && fotos.Count > 0)
            {
                string uploadsFolder = Path.Combine(_env.ContentRootPath, "SecureUploads", "Evidencias");
                Directory.CreateDirectory(uploadsFolder); // Garante que a pasta existe
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };

                var uploadTasks = new List<Task>();

                foreach (var foto in fotos)
                {
                    if (foto.Length > 0)
                    {
                        var extension = Path.GetExtension(foto.FileName).ToLowerInvariant();
                        if (!allowedExtensions.Contains(extension))
                        {
                            continue;
                        }

                        if (!await IsValidFileSignatureAsync(foto, extension))
                        {
                            continue;
                        }

                        string uniqueFileName = $"{Guid.NewGuid()}{extension}";
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        var currentFoto = foto;
                        async Task SaveFileAsync()
                        {
                            await using (var fileStream = new FileStream(filePath, FileMode.Create))
                            {
                                await currentFoto.CopyToAsync(fileStream);
                            }
                        }

                        uploadTasks.Add(SaveFileAsync());

                        evidencias.Add(new Evidencia
                        {
                            CaminhoArquivo = $"/Admin/GetEvidencia?fileName={uniqueFileName}",
                            DataUpload = DateTime.Now
                        });
                    }
                }

                await Task.WhenAll(uploadTasks);
            }

            return evidencias;
        }

        private static async Task<bool> IsValidFileSignatureAsync(IFormFile file, string extension)
        {
            if (file == null || file.Length == 0) return false;

            await using var stream = file.OpenReadStream();
            var signatures = new Dictionary<string, List<byte[]>>
            {
                { ".jpg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
                { ".jpeg", new List<byte[]> { new byte[] { 0xFF, 0xD8, 0xFF } } },
                { ".png", new List<byte[]> { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
                { ".gif", new List<byte[]> { new byte[] { 0x47, 0x49, 0x46, 0x38 } } },
                { ".pdf", new List<byte[]> { new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D } } }
            };

            if (!signatures.TryGetValue(extension, out var expectedSignatures))
                return false;

            var maxSignatureLength = expectedSignatures.Max(s => s.Length);
            var headerBytes = new byte[maxSignatureLength];

            int bytesRead = await stream.ReadAsync(headerBytes, 0, maxSignatureLength);
            if (bytesRead < maxSignatureLength && bytesRead < expectedSignatures.Min(s => s.Length))
            {
                return false;
            }

            return expectedSignatures.Any(signature =>
                headerBytes.Take(signature.Length).SequenceEqual(signature));
        }
    }
}
