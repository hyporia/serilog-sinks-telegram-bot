using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace Serilog.Sinks.TelegramBot
{
    /// <summary>
    /// Batched Serilog sink that renders log events and posts them to a Telegram chat.
    /// </summary>
    public sealed class TelegramBotSink : IBatchedLogEventSink, IDisposable
    {
        private readonly TelegramBotSinkOptions _options;
        private readonly ITelegramBotClient _client;
        private readonly MessageTemplateTextFormatter _formatter;
        private readonly bool _ownsClient;

        /// <summary>
        /// Creates the sink. When <paramref name="client"/> is null a default
        /// <see cref="TelegramBotClient"/> is created and owned by this sink.
        /// </summary>
        public TelegramBotSink(TelegramBotSinkOptions options, ITelegramBotClient? client = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();

            _ownsClient = client is null;
            _client = client ?? new TelegramBotClient(_options);
            _formatter = new MessageTemplateTextFormatter(_options.OutputTemplate, null);
        }

        /// <inheritdoc />
        public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> batch)
        {
            if (batch is null)
                return;

            foreach (var message in BuildMessages(batch))
                await _client.SendMessageAsync(message).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public Task OnEmptyBatchAsync() => Task.CompletedTask;

        internal IEnumerable<string> BuildMessages(IEnumerable<LogEvent> batch)
        {
            var current = new StringBuilder();

            foreach (var logEvent in batch)
            {
                if (logEvent.Level < _options.MinimumLevel)
                    continue;

                var rendered = Render(logEvent);
                if (rendered.Length == 0)
                    continue;

                var separator = current.Length == 0 ? string.Empty : "\n\n";

                // If appending this event would overflow a Telegram message, flush first.
                if (current.Length > 0 &&
                    current.Length + separator.Length + rendered.Length > TelegramMessageFormatter.MaxMessageLength)
                {
                    yield return current.ToString();
                    current.Clear();
                    separator = string.Empty;
                }

                current.Append(separator).Append(rendered);
            }

            if (current.Length > 0)
                yield return current.ToString();
        }

        private string Render(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            var text = writer.ToString().TrimEnd();

            var escaped = TelegramMessageFormatter.Escape(text, _options.ParseMode);
            return TelegramMessageFormatter.Truncate(escaped, _options.ParseMode);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_ownsClient && _client is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
