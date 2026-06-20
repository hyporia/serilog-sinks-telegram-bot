using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Sinks.TelegramBot;
using Xunit;

namespace Serilog.Sinks.TelegramBot.Tests
{
    public class TelegramBotClientTests
    {
        private sealed class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly Queue<HttpResponseMessage> _responses;

            public FakeHttpMessageHandler(params HttpResponseMessage[] responses)
            {
                _responses = new Queue<HttpResponseMessage>(responses);
            }

            public List<string> RequestBodies { get; } = new();
            public bool Disposed { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestBodies.Add(await request.Content!.ReadAsStringAsync().ConfigureAwait(false));
                return _responses.Count > 0
                    ? _responses.Dequeue()
                    : new HttpResponseMessage(HttpStatusCode.OK);
            }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }

        private static HttpResponseMessage Ok() =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };

        private static TelegramBotSinkOptions Options() => new()
        {
            BotToken = "token",
            ChatId = "123",
            ParseMode = TelegramParseMode.Html
        };

        private static (TelegramBotClient client, FakeHttpMessageHandler handler) CreateClient(
            TelegramBotSinkOptions options, params HttpResponseMessage[] responses)
        {
            var handler = new FakeHttpMessageHandler(responses);
            var httpClient = new HttpClient(handler);
            return (new TelegramBotClient(options, httpClient), handler);
        }

        [Fact]
        public async Task Payload_NumericChatId_IsUnquoted()
        {
            var (client, handler) = CreateClient(Options(), Ok());
            await client.SendMessageAsync("hi");

            Assert.Contains("\"chat_id\":123", handler.RequestBodies[0]);
            Assert.DoesNotContain("\"chat_id\":\"123\"", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Payload_UsernameChatId_IsQuoted()
        {
            var options = Options();
            options.ChatId = "@my_channel";
            var (client, handler) = CreateClient(options, Ok());

            await client.SendMessageAsync("hi");

            Assert.Contains("\"chat_id\":\"@my_channel\"", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Payload_IncludesParseMode_ForHtml()
        {
            var (client, handler) = CreateClient(Options(), Ok());
            await client.SendMessageAsync("hi");

            Assert.Contains("\"parse_mode\":\"HTML\"", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Payload_OmitsParseMode_ForNone()
        {
            var options = Options();
            options.ParseMode = TelegramParseMode.None;
            var (client, handler) = CreateClient(options, Ok());

            await client.SendMessageAsync("hi");

            Assert.DoesNotContain("parse_mode", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Payload_IncludesThreadAndSilentFlags()
        {
            var options = Options();
            options.MessageThreadId = 42;
            options.DisableNotification = true;
            var (client, handler) = CreateClient(options, Ok());

            await client.SendMessageAsync("hi");

            Assert.Contains("\"message_thread_id\":42", handler.RequestBodies[0]);
            Assert.Contains("\"disable_notification\":true", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Payload_EscapesTextForJson()
        {
            var (client, handler) = CreateClient(Options(), Ok());
            await client.SendMessageAsync("line1\nwith \"quote\" and \\slash");

            Assert.Contains("\"text\":\"line1\\nwith \\\"quote\\\" and \\\\slash\"", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task SendMessage_RetriesOn429_ThenSucceeds()
        {
            var tooMany = new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{\"ok\":false,\"error_code\":429,\"parameters\":{\"retry_after\":0}}")
            };
            var options = Options();
            options.MaxSendRetries = 2;
            var (client, handler) = CreateClient(options, tooMany, Ok());

            await client.SendMessageAsync("hi");

            Assert.Equal(2, handler.RequestBodies.Count); // first 429, retry OK
        }

        [Fact]
        public async Task SendMessage_ThrowsWhen429RetriesExhausted()
        {
            HttpResponseMessage Throttled() => new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{\"parameters\":{\"retry_after\":0}}")
            };

            var options = Options();
            options.MaxSendRetries = 1;
            var (client, handler) = CreateClient(options, Throttled(), Throttled(), Throttled());

            await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMessageAsync("hi"));
            Assert.Equal(2, handler.RequestBodies.Count); // initial + 1 retry
        }

        [Fact]
        public async Task SendMessage_ThrowsOnNonRetryableError()
        {
            var bad = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"ok\":false,\"description\":\"bad\"}")
            };
            var (client, handler) = CreateClient(Options(), bad);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.SendMessageAsync("hi"));
            Assert.Single(handler.RequestBodies); // no retry on 400
        }

        [Fact]
        public void Dispose_DoesNotDisposeInjectedHttpClient()
        {
            var handler = new FakeHttpMessageHandler();
            var httpClient = new HttpClient(handler);
            var client = new TelegramBotClient(Options(), httpClient);

            client.Dispose();

            Assert.False(handler.Disposed); // injected client (and its handler) untouched
        }
    }
}
