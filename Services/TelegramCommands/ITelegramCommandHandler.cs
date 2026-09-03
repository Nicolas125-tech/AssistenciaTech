using System.Threading.Tasks;

namespace AssistenciaTech.Services.TelegramCommands
{
    public interface ITelegramCommandHandler
    {
        Task HandleCommandAsync(string text, string chatId);
    }
}
