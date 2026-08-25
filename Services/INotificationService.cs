using AssistenciaTech.Models;
using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    /// <summary>
    /// Interface para o serviço de notificações (WhatsApp/Email).
    /// Dispara notificações automáticas quando o status de uma OS muda.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Envia uma notificação ao cliente informando a mudança de status da OS.
        /// </summary>
        Task EnviarNotificacaoStatusAsync(Cliente cliente, OrdemServico os, string statusAnterior);
    }
}
