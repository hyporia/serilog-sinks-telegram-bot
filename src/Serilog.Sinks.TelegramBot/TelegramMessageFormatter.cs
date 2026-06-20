using System.Text;

namespace Serilog.Sinks.TelegramBot
{
    /// <summary>
    /// Helpers for escaping text so it is safe to send under a given Telegram parse mode.
    /// </summary>
    internal static class TelegramMessageFormatter
    {
        // Characters that must be escaped in MarkdownV2, per the Telegram Bot API.
        private const string MarkdownV2Reserved = "_*[]()~`>#+-=|{}.!";

        /// <summary>
        /// The hard limit on a single Telegram message, in UTF-16 code units.
        /// </summary>
        public const int MaxMessageLength = 4096;

        public static string Escape(string text, TelegramParseMode parseMode)
        {
            switch (parseMode)
            {
                case TelegramParseMode.Html:
                    return EscapeHtml(text);
                case TelegramParseMode.MarkdownV2:
                    return EscapeMarkdownV2(text);
                default:
                    return text;
            }
        }

        /// <summary>
        /// Truncates a message to <see cref="MaxMessageLength"/>, appending an ellipsis
        /// marker when content was dropped.
        /// </summary>
        public static string Truncate(string text)
        {
            if (text.Length <= MaxMessageLength)
                return text;

            const string marker = "\n…";
            return text.Substring(0, MaxMessageLength - marker.Length) + marker;
        }

        private static string EscapeHtml(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '&': sb.Append("&amp;"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        private static string EscapeMarkdownV2(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                if (MarkdownV2Reserved.IndexOf(c) >= 0)
                    sb.Append('\\');
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
