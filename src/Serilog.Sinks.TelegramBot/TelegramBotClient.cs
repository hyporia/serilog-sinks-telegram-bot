using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Debugging;

namespace Serilog.Sinks.TelegramBot
{
    /// <summary>
    /// Default <see cref="ITelegramBotClient"/> that talks to the Telegram Bot API
    /// over HTTP using <see cref="HttpClient"/>.
    /// </summary>
    public sealed class TelegramBotClient : ITelegramBotClient, IDisposable
    {
        private readonly TelegramBotSinkOptions _options;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly Uri _endpoint;

        /// <summary>
        /// Creates a client. When <paramref name="httpClient"/> is null an internal
        /// instance is created and disposed with this client.
        /// </summary>
        public TelegramBotClient(TelegramBotSinkOptions options, HttpClient? httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _ownsHttpClient = httpClient is null;
            _httpClient = httpClient ?? new HttpClient();
            _endpoint = new Uri(
                $"{_options.ApiBaseUrl.TrimEnd('/')}/bot{_options.BotToken}/sendMessage");
        }

        /// <inheritdoc />
        public async Task SendMessageAsync(string text, CancellationToken cancellationToken = default)
        {
            var payload = BuildPayload(text);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await _httpClient
                .PostAsync(_endpoint, content, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadBodyAsync(response).ConfigureAwait(false);
                SelfLog.WriteLine(
                    "Serilog.Sinks.TelegramBot: sendMessage failed ({0}): {1}",
                    (int)response.StatusCode, body);
                response.EnsureSuccessStatusCode();
            }
        }

        private string BuildPayload(string text)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"chat_id\":").Append(JsonValue(_options.ChatId)).Append(',');
            sb.Append("\"text\":").Append(JsonString(text));

            var parseMode = ParseModeName(_options.ParseMode);
            if (parseMode != null)
                sb.Append(",\"parse_mode\":").Append(JsonString(parseMode));

            if (_options.MessageThreadId.HasValue)
                sb.Append(",\"message_thread_id\":").Append(_options.MessageThreadId.Value);

            if (_options.DisableNotification)
                sb.Append(",\"disable_notification\":true");

            sb.Append('}');
            return sb.ToString();
        }

        private static string? ParseModeName(TelegramParseMode mode)
        {
            switch (mode)
            {
                case TelegramParseMode.Html: return "HTML";
                case TelegramParseMode.MarkdownV2: return "MarkdownV2";
                default: return null;
            }
        }

        // chat_id may be a number or a "@username" string. Numeric ids are emitted
        // unquoted so the API treats them as integers.
        private static string JsonValue(string chatId)
        {
            return long.TryParse(chatId, out _) ? chatId : JsonString(chatId);
        }

        private static string JsonString(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        private static async Task<string> ReadBodyAsync(HttpResponseMessage response)
        {
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            catch
            {
                return "<unreadable response body>";
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_ownsHttpClient)
                _httpClient.Dispose();
        }
    }
}
