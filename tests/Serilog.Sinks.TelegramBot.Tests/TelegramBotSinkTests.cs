using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.TelegramBot;
using Xunit;

namespace Serilog.Sinks.TelegramBot.Tests
{
    public class TelegramBotSinkTests
    {
        private sealed class RecordingClient : ITelegramBotClient, IDisposable
        {
            public List<string> Messages { get; } = new();
            public bool Disposed { get; private set; }

            public Task SendMessageAsync(string text, CancellationToken cancellationToken = default)
            {
                Messages.Add(text);
                return Task.CompletedTask;
            }

            public void Dispose() => Disposed = true;
        }

        private static TelegramBotSinkOptions Options() => new()
        {
            BotToken = "token",
            ChatId = "123",
            MinimumLevel = LogEventLevel.Information,
            ParseMode = TelegramParseMode.Html,
            OutputTemplate = "{Message:lj}"
        };

        private static LogEvent Event(LogEventLevel level, string message)
        {
            var parser = new MessageTemplateParser();
            return new LogEvent(
                DateTimeOffset.Now,
                level,
                exception: null,
                parser.Parse(message),
                Enumerable.Empty<LogEventProperty>());
        }

        [Fact]
        public async Task EmitBatchAsync_SendsRenderedMessage()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            await sink.EmitBatchAsync(new[] { Event(LogEventLevel.Warning, "boom") });

            var message = Assert.Single(client.Messages);
            Assert.Equal("boom", message);
        }

        [Fact]
        public async Task EmitBatchAsync_FiltersBelowMinimumLevel()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            await sink.EmitBatchAsync(new[]
            {
                Event(LogEventLevel.Debug, "ignored"),
                Event(LogEventLevel.Information, "kept")
            });

            var message = Assert.Single(client.Messages);
            Assert.Equal("kept", message);
        }

        [Fact]
        public async Task EmitBatchAsync_EscapesHtml()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            await sink.EmitBatchAsync(new[] { Event(LogEventLevel.Error, "<b> & <i>") });

            Assert.Equal("&lt;b&gt; &amp; &lt;i&gt;", Assert.Single(client.Messages));
        }

        [Fact]
        public async Task EmitBatchAsync_CombinesMultipleEventsIntoOneMessage()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            await sink.EmitBatchAsync(new[]
            {
                Event(LogEventLevel.Warning, "one"),
                Event(LogEventLevel.Warning, "two")
            });

            var message = Assert.Single(client.Messages);
            Assert.Equal("one\n\ntwo", message);
        }

        [Fact]
        public async Task EmitBatchAsync_SplitsWhenExceedingMaxLength()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            var big = new string('x', 3000);
            await sink.EmitBatchAsync(new[]
            {
                Event(LogEventLevel.Warning, big),
                Event(LogEventLevel.Warning, big)
            });

            Assert.Equal(2, client.Messages.Count);
        }

        [Fact]
        public async Task EmitBatchAsync_EscapesMarkdownV2EndToEnd()
        {
            var client = new RecordingClient();
            var options = Options();
            options.ParseMode = TelegramParseMode.MarkdownV2;
            var sink = new TelegramBotSink(options, client);

            await sink.EmitBatchAsync(new[] { Event(LogEventLevel.Warning, "a_b.") });

            Assert.Equal("a\\_b\\.", Assert.Single(client.Messages));
        }

        [Fact]
        public async Task EmitBatchAsync_AllFiltered_SendsNothing()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            await sink.EmitBatchAsync(new[]
            {
                Event(LogEventLevel.Debug, "a"),
                Event(LogEventLevel.Verbose, "b")
            });

            Assert.Empty(client.Messages);
        }

        [Fact]
        public void Dispose_DoesNotDisposeInjectedClient()
        {
            var client = new RecordingClient();
            var sink = new TelegramBotSink(Options(), client);

            sink.Dispose();

            Assert.False(client.Disposed);
        }

        [Fact]
        public void Constructor_ThrowsWhenTokenMissing()
        {
            var options = Options();
            options.BotToken = "";
            Assert.Throws<ArgumentException>(() => new TelegramBotSink(options, new RecordingClient()));
        }
    }
}
