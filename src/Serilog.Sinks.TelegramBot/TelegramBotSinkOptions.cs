using System;
using Serilog.Events;

namespace Serilog.Sinks.TelegramBot
{
    /// <summary>
    /// Telegram message parse modes supported by the Bot API.
    /// </summary>
    public enum TelegramParseMode
    {
        /// <summary>No formatting; text is sent verbatim.</summary>
        None,

        /// <summary>MarkdownV2 formatting. Reserved characters are escaped automatically.</summary>
        MarkdownV2,

        /// <summary>HTML formatting. Reserved characters are escaped automatically.</summary>
        Html
    }

    /// <summary>
    /// Configuration for <see cref="TelegramBotSink"/>.
    /// </summary>
    public sealed class TelegramBotSinkOptions
    {
        /// <summary>
        /// The bot token issued by @BotFather, e.g. <c>123456:ABC-DEF...</c>.
        /// </summary>
        public string BotToken { get; set; } = string.Empty;

        /// <summary>
        /// The target chat id. May be a numeric id (user/group) or a channel
        /// username such as <c>@my_channel</c>.
        /// </summary>
        public string ChatId { get; set; } = string.Empty;

        /// <summary>
        /// Optional message thread (topic) id for forum-style groups.
        /// </summary>
        public int? MessageThreadId { get; set; }

        /// <summary>
        /// Minimum level for events written to Telegram.
        /// </summary>
        public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Warning;

        /// <summary>
        /// Output template used to render each event. The default includes the
        /// level, message and exception.
        /// </summary>
        public string OutputTemplate { get; set; } =
            "{Level:u3} {Timestamp:yyyy-MM-dd HH:mm:ss}{NewLine}{Message:lj}{NewLine}{Exception}";

        /// <summary>
        /// Formatting mode applied to the rendered message.
        /// </summary>
        public TelegramParseMode ParseMode { get; set; } = TelegramParseMode.Html;

        /// <summary>
        /// When true, Telegram delivers the message silently (no notification sound).
        /// </summary>
        public bool DisableNotification { get; set; }

        /// <summary>
        /// Maximum number of events sent per outgoing Telegram message.
        /// </summary>
        public int BatchSizeLimit { get; set; } = 10;

        /// <summary>
        /// How often queued events are flushed to Telegram.
        /// </summary>
        public TimeSpan Period { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Base address of the Telegram Bot API. Override for a local Bot API server.
        /// </summary>
        public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(BotToken))
                throw new ArgumentException("BotToken must be provided.", nameof(BotToken));
            if (string.IsNullOrWhiteSpace(ChatId))
                throw new ArgumentException("ChatId must be provided.", nameof(ChatId));
        }
    }
}
