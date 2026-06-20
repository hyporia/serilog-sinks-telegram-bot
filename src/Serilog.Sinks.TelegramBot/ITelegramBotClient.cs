using System.Threading;
using System.Threading.Tasks;

namespace Serilog.Sinks.TelegramBot
{
    /// <summary>
    /// Minimal abstraction over the Telegram Bot API <c>sendMessage</c> method.
    /// Implementations are used by <see cref="TelegramBotSink"/> and can be
    /// substituted in tests.
    /// </summary>
    public interface ITelegramBotClient
    {
        /// <summary>
        /// Sends a single text message to the configured chat.
        /// </summary>
        /// <param name="text">The (already formatted and escaped) message text.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        Task SendMessageAsync(string text, CancellationToken cancellationToken = default);
    }
}
