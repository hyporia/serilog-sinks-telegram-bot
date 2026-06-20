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
        /// Truncates an already-escaped message to <see cref="MaxMessageLength"/>,
        /// appending an ellipsis marker when content was dropped. The cut point is
        /// chosen so it never splits a UTF-16 surrogate pair or a partial escape
        /// sequence (an HTML entity in <see cref="TelegramParseMode.Html"/> mode or a
        /// backslash escape in <see cref="TelegramParseMode.MarkdownV2"/> mode), either
        /// of which Telegram would reject as unparseable.
        /// </summary>
        public static string Truncate(string text, TelegramParseMode parseMode)
        {
            if (text.Length <= MaxMessageLength)
                return text;

            const string marker = "\n…";
            var cut = MaxMessageLength - marker.Length;

            // Don't split a UTF-16 surrogate pair.
            if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
                cut--;

            cut = BackUpPastPartialEscape(text, cut, parseMode);

            return text.Substring(0, cut) + marker;
        }

        private static int BackUpPastPartialEscape(string text, int cut, TelegramParseMode parseMode)
        {
            if (cut <= 0)
                return cut;

            switch (parseMode)
            {
                case TelegramParseMode.Html:
                    // If the tail contains an unterminated "&...;" entity, drop it.
                    var amp = text.LastIndexOf('&', cut - 1);
                    if (amp >= 0 && text.IndexOf(';', amp, cut - amp) < 0)
                        return amp;
                    break;

                case TelegramParseMode.MarkdownV2:
                    // A trailing run of backslashes with odd length leaves a dangling
                    // escape (the character it escaped was truncated away).
                    var backslashes = 0;
                    var i = cut - 1;
                    while (i >= 0 && text[i] == '\\')
                    {
                        backslashes++;
                        i--;
                    }

                    if ((backslashes & 1) == 1)
                        return cut - 1;
                    break;
            }

            return cut;
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
