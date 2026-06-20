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
            Assert.Equal(input, TelegramMessageFormatter.Truncate(input));
        }

        [Fact]
        public void Truncate_ClampsToMaxLength()
        {
            var input = new string('x', TelegramMessageFormatter.MaxMessageLength + 100);
            var result = TelegramMessageFormatter.Truncate(input);
            Assert.Equal(TelegramMessageFormatter.MaxMessageLength, result.Length);
            Assert.EndsWith("…", result);
        }
    }
}
