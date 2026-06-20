using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Debugging;

namespace Serilog.Sinks.Telegram.Bot
{
    /// <summary>
    /// Default <see cref="ITelegramBotClient"/> that talks to the Telegram Bot API
    /// over HTTP using <see cref="HttpClient"/>.
    /// </summary>
    public sealed class TelegramBotClient : ITelegramBotClient, IDisposable
    {
        // Upper bound on how long we honour a server-supplied retry_after, so a large
        // value can't stall shutdown indefinitely.
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

        private static readonly Regex RetryAfterRegex =
            new Regex("\"retry_after\"\\s*:\\s*(\\d+)", RegexOptions.Compiled);

        private readonly TelegramBotSinkOptions _options;
        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly Uri _endpoint;

        /// <summary>
        /// Creates a client. When <paramref name="httpClient"/> is null an internal
        /// instance is created (with <see cref="TelegramBotSinkOptions.RequestTimeout"/>)
        /// and disposed with this client. An injected client keeps its own timeout.
        /// </summary>
        public TelegramBotClient(TelegramBotSinkOptions options, HttpClient? httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _ownsHttpClient = httpClient is null;
            _httpClient = httpClient ?? new HttpClient { Timeout = _options.RequestTimeout };
            _endpoint = new Uri(
                $"{_options.ApiBaseUrl.TrimEnd('/')}/bot{_options.BotToken}/sendMessage");
        }

        /// <inheritdoc />
        public async Task SendMessageAsync(string text, CancellationToken cancellationToken = default)
        {
            var payload = BuildPayload(text);
            var maxAttempts = Math.Max(0, _options.MaxSendRetries) + 1;

            for (var attempt = 1; ; attempt++)
            {
                // StringContent is single-use once posted, so build a fresh one per attempt.
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await _httpClient
                    .PostAsync(_endpoint, content, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return;

                var body = await ReadBodyAsync(response).ConfigureAwait(false);

                if (response.StatusCode == (HttpStatusCode)429 && attempt < maxAttempts)
                {
                    var delay = ResolveRetryDelay(response, body);
                    SelfLog.WriteLine(
                        "Serilog.Sinks.Telegram.Bot: rate limited (429), retrying in {0}s (attempt {1}/{2})",
                        delay.TotalSeconds, attempt, maxAttempts);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                SelfLog.WriteLine(
                    "Serilog.Sinks.Telegram.Bot: sendMessage failed ({0}): {1}",
                    (int)response.StatusCode, body);
                response.EnsureSuccessStatusCode();
                return; // unreachable; EnsureSuccessStatusCode throws on failure.
            }
        }

        // Determines how long to wait before retrying a 429, preferring the Bot API's
        // parameters.retry_after, then the Retry-After header, then a 1s fallback.
        private static TimeSpan ResolveRetryDelay(HttpResponseMessage response, string body)
        {
            var seconds = ParseRetryAfterFromBody(body);

            if (seconds is null)
            {
                var header = response.Headers.RetryAfter;
                if (header?.Delta is TimeSpan delta)
                    seconds = delta.TotalSeconds;
                else if (header?.Date is DateTimeOffset date)
                {
                    var diff = date - DateTimeOffset.UtcNow;
                    if (diff > TimeSpan.Zero)
                        seconds = diff.TotalSeconds;
                }
            }

            var delay = TimeSpan.FromSeconds(seconds ?? 1);
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;
            return delay > MaxRetryDelay ? MaxRetryDelay : delay;
        }

        private static double? ParseRetryAfterFromBody(string body)
        {
            if (string.IsNullOrEmpty(body))
                return null;

            var match = RetryAfterRegex.Match(body);
            return match.Success &&
                   double.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s)
                ? s
                : (double?)null;
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
