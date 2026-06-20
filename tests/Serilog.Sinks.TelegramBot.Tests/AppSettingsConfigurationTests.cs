using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog;
using Xunit;

namespace Serilog.Sinks.TelegramBot.Tests
{
    public class AppSettingsConfigurationTests
    {
        // Verifies that every Args key in the documented appsettings layout binds to a
        // real parameter on WriteTo.TelegramBot. A typo or type mismatch makes
        // CreateLogger throw, so a clean build of the logger is the assertion.
        [Fact]
        public void ReadFromConfiguration_BindsAllArguments()
        {
            var settings = new Dictionary<string, string?>
            {
                ["Serilog:Using:0"] = "Serilog.Sinks.TelegramBot",
                ["Serilog:MinimumLevel"] = "Information",
                ["Serilog:WriteTo:0:Name"] = "TelegramBot",
                ["Serilog:WriteTo:0:Args:botToken"] = "123456:ABC",
                ["Serilog:WriteTo:0:Args:chatId"] = "@my_channel",
                ["Serilog:WriteTo:0:Args:restrictedToMinimumLevel"] = "Warning",
                ["Serilog:WriteTo:0:Args:parseMode"] = "MarkdownV2",
                ["Serilog:WriteTo:0:Args:outputTemplate"] = "{Message:lj}",
                ["Serilog:WriteTo:0:Args:disableNotification"] = "true",
                ["Serilog:WriteTo:0:Args:messageThreadId"] = "42",
                ["Serilog:WriteTo:0:Args:batchSizeLimit"] = "5",
                ["Serilog:WriteTo:0:Args:period"] = "00:00:03",
                ["Serilog:WriteTo:0:Args:queueLimit"] = "500",
                ["Serilog:WriteTo:0:Args:requestTimeout"] = "00:00:10",
                ["Serilog:WriteTo:0:Args:maxSendRetries"] = "2",
                ["Serilog:WriteTo:0:Args:apiBaseUrl"] = "https://api.telegram.org"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            using var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            Assert.NotNull(logger);
        }

        [Fact]
        public void ReadFromConfiguration_BindsMinimalArguments()
        {
            var settings = new Dictionary<string, string?>
            {
                ["Serilog:Using:0"] = "Serilog.Sinks.TelegramBot",
                ["Serilog:WriteTo:0:Name"] = "TelegramBot",
                ["Serilog:WriteTo:0:Args:botToken"] = "123456:ABC",
                ["Serilog:WriteTo:0:Args:chatId"] = "987654321"
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            using var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            Assert.NotNull(logger);
        }
    }
}
