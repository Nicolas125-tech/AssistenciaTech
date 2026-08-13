using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public interface IEvidenciaUploadService
    {
        Task<List<Evidencia>> ProcessUploadsAsync(IFormFileCollection fotos);
    }
}
