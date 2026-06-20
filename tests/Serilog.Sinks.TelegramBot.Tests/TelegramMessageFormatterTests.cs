using Serilog.Sinks.TelegramBot;
using Xunit;

namespace Serilog.Sinks.TelegramBot.Tests
{
    public class TelegramMessageFormatterTests
    {
        [Fact]
        public void EscapeHtml_EscapesReservedCharacters()
        {
            var result = TelegramMessageFormatter.Escape("<a> & </a>", TelegramParseMode.Html);
            Assert.Equal("&lt;a&gt; &amp; &lt;/a&gt;", result);
        }

        [Fact]
        public void EscapeMarkdownV2_EscapesReservedCharacters()
        {
            var result = TelegramMessageFormatter.Escape("a_b*c.", TelegramParseMode.MarkdownV2);
            Assert.Equal("a\\_b\\*c\\.", result);
        }

        [Fact]
        public void Escape_None_ReturnsInputUnchanged()
        {
            const string input = "<a> & _b_";
            Assert.Equal(input, TelegramMessageFormatter.Escape(input, TelegramParseMode.None));
        }

        [Fact]
        public void Truncate_LeavesShortMessageUntouched()
        {
            const string input = "short";
            Assert.Equal(input, TelegramMessageFormatter.Truncate(input, TelegramParseMode.None));
        }

        [Fact]
        public void Truncate_ClampsToMaxLength()
        {
            var input = new string('x', TelegramMessageFormatter.MaxMessageLength + 100);
            var result = TelegramMessageFormatter.Truncate(input, TelegramParseMode.None);
            Assert.Equal(TelegramMessageFormatter.MaxMessageLength, result.Length);
            Assert.EndsWith("…", result);
        }

        [Fact]
        public void Truncate_DoesNotSplitSurrogatePair()
        {
            // Each "😀" is two UTF-16 code units; fill past the limit.
            var input = string.Concat(System.Linq.Enumerable.Repeat("😀", TelegramMessageFormatter.MaxMessageLength));
            var result = TelegramMessageFormatter.Truncate(input, TelegramParseMode.None);

            Assert.True(result.Length <= TelegramMessageFormatter.MaxMessageLength);
            // The character before the trailing marker must not be a lone high surrogate.
            var beforeMarker = result.Substring(0, result.Length - 2);
            Assert.False(char.IsHighSurrogate(beforeMarker[beforeMarker.Length - 1]));
        }

        [Fact]
        public void Truncate_Html_DoesNotSplitEntity()
        {
            // "&" escapes to "&amp;" (5 chars), so the escaped form overflows.
            var raw = new string('&', TelegramMessageFormatter.MaxMessageLength);
            var escaped = TelegramMessageFormatter.Escape(raw, TelegramParseMode.Html);
            var result = TelegramMessageFormatter.Truncate(escaped, TelegramParseMode.Html);

            Assert.True(result.Length <= TelegramMessageFormatter.MaxMessageLength);
            // Every "&" kept must be a complete "&amp;" entity (no dangling "&am").
            var content = result.Substring(0, result.Length - 2); // drop "\n…"
            var lastAmp = content.LastIndexOf('&');
            if (lastAmp >= 0)
                Assert.True(content.IndexOf(';', lastAmp) >= 0, "truncation left a partial HTML entity");
        }

        [Fact]
        public void Truncate_MarkdownV2_DoesNotLeaveDanglingBackslash()
        {
            // "." escapes to "\." so the escaped form overflows with backslash pairs.
            var raw = new string('.', TelegramMessageFormatter.MaxMessageLength);
            var escaped = TelegramMessageFormatter.Escape(raw, TelegramParseMode.MarkdownV2);
            var result = TelegramMessageFormatter.Truncate(escaped, TelegramParseMode.MarkdownV2);

            Assert.True(result.Length <= TelegramMessageFormatter.MaxMessageLength);
            var content = result.Substring(0, result.Length - 2); // drop "\n…"
            // Count trailing backslashes; an even count means no dangling escape.
            var trailing = 0;
            for (var i = content.Length - 1; i >= 0 && content[i] == '\\'; i--)
                trailing++;
            Assert.True((trailing & 1) == 0, "truncation left a dangling MarkdownV2 escape");
        }
    }
}
