using System;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;

// Surface any delivery problems to the console.
SelfLog.Enable(Console.Error);

// Provide these via environment variables so secrets stay out of source.
var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN")
    ?? throw new InvalidOperationException("Set TELEGRAM_BOT_TOKEN.");
var chatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID")
    ?? throw new InvalidOperationException("Set TELEGRAM_CHAT_ID.");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.TelegramBot(
        botToken: botToken,
        chatId: chatId,
        restrictedToMinimumLevel: LogEventLevel.Warning)
    .CreateLogger();

Log.Information("This goes to the console only (below Telegram minimum).");
Log.Warning("Disk space is running low: {FreeSpaceMb} MB left", 128);

try
{
    throw new InvalidOperationException("boom");
}
catch (Exception ex)
{
    Log.Error(ex, "Something went wrong while processing {OrderId}", 1234);
}

// Ensure buffered events are flushed before the process exits.
await Log.CloseAndFlushAsync();
