using System;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.Telegram.Bot;

// ReSharper disable once CheckNamespace -- conventional namespace for Serilog sinks.
namespace Serilog
{
    /// <summary>
    /// Adds the <c>WriteTo.TelegramBot(...)</c> configuration methods.
    /// </summary>
    public static class LoggerConfigurationTelegramBotExtensions
    {
        /// <summary>
        /// Writes log events to a Telegram chat via the Telegram Bot API. Every option
        /// is exposed as a named parameter so the sink can also be configured from
        /// <c>appsettings.json</c> via <c>Serilog.Settings.Configuration</c>.
        /// </summary>
        /// <param name="sinkConfiguration">The sink configuration.</param>
        /// <param name="botToken">Bot token issued by @BotFather.</param>
        /// <param name="chatId">Target chat id or <c>@channel</c> username.</param>
        /// <param name="restrictedToMinimumLevel">Minimum level for events sent to Telegram.</param>
        /// <param name="parseMode">Wire encoding mode (<c>None</c>, <c>MarkdownV2</c>, or <c>Html</c>).</param>
        /// <param name="outputTemplate">Optional output template override.</param>
        /// <param name="disableNotification">Deliver messages silently.</param>
        /// <param name="messageThreadId">Optional forum (topic) thread id.</param>
        /// <param name="batchSizeLimit">Maximum events per outgoing Telegram message.</param>
        /// <param name="period">Flush interval; omit to keep the default of 2 seconds.</param>
        /// <param name="queueLimit">Maximum events buffered while Telegram is unreachable.</param>
        /// <param name="requestTimeout">Per-request HTTP timeout; omit to keep the default of 30 seconds.</param>
        /// <param name="maxSendRetries">Extra attempts on HTTP 429, honouring <c>retry_after</c>.</param>
        /// <param name="apiBaseUrl">Base address of the Telegram Bot API.</param>
        public static LoggerConfiguration TelegramBot(
            this LoggerSinkConfiguration sinkConfiguration,
            string botToken,
            string chatId,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Warning,
            TelegramParseMode parseMode = TelegramParseMode.Html,
            string? outputTemplate = null,
            bool disableNotification = false,
            int? messageThreadId = null,
            int? batchSizeLimit = null,
            TimeSpan period = default,
            int? queueLimit = null,
            TimeSpan requestTimeout = default,
            int? maxSendRetries = null,
            string? apiBaseUrl = null)
        {
            if (sinkConfiguration is null)
                throw new ArgumentNullException(nameof(sinkConfiguration));

            // Start from the option defaults and override only what the caller supplied,
            // so default values live in one place (TelegramBotSinkOptions).
            var options = new TelegramBotSinkOptions
            {
                BotToken = botToken,
                ChatId = chatId,
                MinimumLevel = restrictedToMinimumLevel,
                ParseMode = parseMode,
                DisableNotification = disableNotification,
                MessageThreadId = messageThreadId
            };

            if (!string.IsNullOrWhiteSpace(outputTemplate))
                options.OutputTemplate = outputTemplate!;
            if (batchSizeLimit.HasValue)
                options.BatchSizeLimit = batchSizeLimit.Value;
            if (period != default)
                options.Period = period;
            if (queueLimit.HasValue)
                options.QueueLimit = queueLimit.Value;
            if (requestTimeout != default)
                options.RequestTimeout = requestTimeout;
            if (maxSendRetries.HasValue)
                options.MaxSendRetries = maxSendRetries.Value;
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
                options.ApiBaseUrl = apiBaseUrl!;

            return sinkConfiguration.TelegramBot(options);
        }

        /// <summary>
        /// Writes log events to a Telegram chat using fully specified
        /// <see cref="TelegramBotSinkOptions"/>.
        /// </summary>
        public static LoggerConfiguration TelegramBot(
            this LoggerSinkConfiguration sinkConfiguration,
            TelegramBotSinkOptions options,
            ITelegramBotClient? client = null)
        {
            if (sinkConfiguration is null)
                throw new ArgumentNullException(nameof(sinkConfiguration));
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            options.Validate();

            var sink = new TelegramBotSink(options, client);

            // Serilog 4.x batches in core; it owns and disposes the wrapped sink.
            var batchingOptions = new BatchingOptions
            {
                BatchSizeLimit = options.BatchSizeLimit,
                BufferingTimeLimit = options.Period,
                QueueLimit = options.QueueLimit,
                EagerlyEmitFirstEvent = true
            };

            return sinkConfiguration.Sink(sink, batchingOptions, options.MinimumLevel);
        }
    }
}
