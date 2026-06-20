using System;
using Serilog.Events;

namespace Serilog.Sinks.TelegramBot
{
    /// <summary>
    /// Telegram message parse modes supported by the Bot API.
    /// </summary>
    /// <remarks>
    /// The sink renders each event to plain text and then escapes it so it is
    /// transmitted safely under the selected mode. Formatting characters in your
    /// output template are escaped, not interpreted — choosing a non-<see cref="None"/>
    /// mode does not turn template text into bold/italic/links; it only changes how the
    /// text is encoded on the wire.
    /// </remarks>
    public enum TelegramParseMode
    {
        /// <summary>No <c>parse_mode</c> is sent; text is delivered verbatim.</summary>
        None,

        /// <summary>
        /// Sent with <c>parse_mode=MarkdownV2</c>; the rendered text is escaped so that
        /// MarkdownV2 reserved characters are shown literally rather than interpreted.
        /// </summary>
        MarkdownV2,

        /// <summary>
        /// Sent with <c>parse_mode=HTML</c>; the rendered text is HTML-escaped so that
        /// <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> are shown literally rather than interpreted.
        /// </summary>
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
        /// Maximum number of events buffered in memory while Telegram is unreachable.
        /// Once exceeded, the oldest events are dropped to bound memory use.
        /// </summary>
        public int QueueLimit { get; set; } = 10000;

        /// <summary>
        /// Timeout applied to each Telegram API request. Only used when the sink
        /// creates its own <see cref="System.Net.Http.HttpClient"/>; an injected client
        /// keeps its own timeout.
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Number of additional attempts made when Telegram responds with HTTP 429
        /// (rate limited). Each retry honours the server's <c>retry_after</c> value.
        /// </summary>
        public int MaxSendRetries { get; set; } = 3;

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
            if (string.IsNullOrWhiteSpace(ApiBaseUrl) ||
                !Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
                throw new ArgumentException(
                    $"ApiBaseUrl must be a valid absolute URL (was: '{ApiBaseUrl}').",
                    nameof(ApiBaseUrl));
        }
    }
}
