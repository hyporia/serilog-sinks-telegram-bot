using System;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using Serilog.Sinks.TelegramBot;

// ReSharper disable once CheckNamespace -- conventional namespace for Serilog sinks.
namespace Serilog
{
    /// <summary>
    /// Adds the <c>WriteTo.TelegramBot(...)</c> configuration methods.
    /// </summary>
    public static class LoggerConfigurationTelegramBotExtensions
    {
        /// <summary>
        /// Writes log events to a Telegram chat via the Telegram Bot API.
        /// </summary>
        /// <param name="sinkConfiguration">The sink configuration.</param>
        /// <param name="botToken">Bot token issued by @BotFather.</param>
        /// <param name="chatId">Target chat id or <c>@channel</c> username.</param>
        /// <param name="restrictedToMinimumLevel">Minimum level for events sent to Telegram.</param>
        /// <param name="parseMode">Telegram formatting mode.</param>
        /// <param name="outputTemplate">Optional output template override.</param>
        public static LoggerConfiguration TelegramBot(
            this LoggerSinkConfiguration sinkConfiguration,
            string botToken,
            string chatId,
            LogEventLevel restrictedToMinimumLevel = LogEventLevel.Warning,
            TelegramParseMode parseMode = TelegramParseMode.Html,
            string? outputTemplate = null)
        {
            if (sinkConfiguration is null)
                throw new ArgumentNullException(nameof(sinkConfiguration));

            var options = new TelegramBotSinkOptions
            {
                BotToken = botToken,
                ChatId = chatId,
                MinimumLevel = restrictedToMinimumLevel,
                ParseMode = parseMode
            };

            if (!string.IsNullOrWhiteSpace(outputTemplate))
                options.OutputTemplate = outputTemplate!;

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
            var batchOptions = new PeriodicBatchingSinkOptions
            {
                BatchSizeLimit = options.BatchSizeLimit,
                Period = options.Period,
                EagerlyEmitFirstEvent = true
            };

            var batchingSink = new PeriodicBatchingSink(sink, batchOptions);
            return sinkConfiguration.Sink(batchingSink, options.MinimumLevel);
        }
    }
}
