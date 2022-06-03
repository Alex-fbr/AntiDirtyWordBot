using System.Threading.Tasks;

using Telegram.Bot.Types;

namespace AntiDirtyWordBot.Services
{
    public interface IUpdateHandlerService
    {
        Task GetHandler(Update update);
    }
}
