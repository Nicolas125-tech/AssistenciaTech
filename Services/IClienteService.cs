using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AssistenciaTech.Services
{
    public interface IClienteService
    {
        Task<IEnumerable<SelectListItem>> GetClientesSelectListAsync(int? selectedId = null);
    }
}
