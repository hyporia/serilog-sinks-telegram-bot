# Serilog.Sinks.TelegramBot

A [Serilog](https://serilog.net) sink that delivers log events to a Telegram chat,
group, or channel through the [Telegram Bot API](https://core.telegram.org/bots/api).

Events are buffered and sent in batches (using Serilog 4's built-in batching)
so a burst of logs does not turn into a burst of Telegram messages.

## Features

- Sends log events to any chat / group / channel a bot can post to
- Periodic batching with configurable batch size, flush period, and queue limit
- Safe encoding for `HTML` / `MarkdownV2` modes, or plain text
- Automatic splitting / truncation to respect Telegram's 4096-character limit
- Honours Telegram's `retry_after` on HTTP 429 (rate limiting)
- Configurable minimum level, output template, silent delivery, and forum topics
- No dependencies beyond Serilog itself; targets `netstandard2.0` and `net10.0`

## Installation

```bash
dotnet add package Serilog.Sinks.TelegramBot
```

## Quick start

Create a bot with [@BotFather](https://t.me/BotFather) to get a token, then find
your chat id (for example by messaging your bot and reading
`https://api.telegram.org/bot<token>/getUpdates`).

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.TelegramBot(
        botToken: "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11",
        chatId: "987654321",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning)
    .CreateLogger();

Log.Warning("Disk space is running low: {FreeSpaceMb} MB left", 128);
Log.Error(new InvalidOperationException("nope"), "Something went wrong");

Log.CloseAndFlush(); // flushes any buffered events
```

`chatId` may be a numeric id or a channel username such as `@my_channel`.

## Full configuration

For complete control, pass `TelegramBotSinkOptions`:

```csharp
using Serilog;
using Serilog.Events;
using Serilog.Sinks.TelegramBot;

Log.Logger = new LoggerConfiguration()
    .WriteTo.TelegramBot(new TelegramBotSinkOptions
    {
        BotToken = "123456:ABC-DEF...",
        ChatId = "@my_channel",
        MinimumLevel = LogEventLevel.Error,
        ParseMode = TelegramParseMode.Html,
        DisableNotification = true,
        MessageThreadId = 42,          // forum topic id (optional)
        BatchSizeLimit = 10,
        Period = TimeSpan.FromSeconds(2),
        OutputTemplate =
            "{Level:u3} {Timestamp:yyyy-MM-dd HH:mm:ss}{NewLine}{Message:lj}{NewLine}{Exception}"
    })
    .CreateLogger();
```

| Option | Default | Description |
| --- | --- | --- |
| `BotToken` | _(required)_ | Bot token from @BotFather |
| `ChatId` | _(required)_ | Numeric chat id or `@username` |
| `MinimumLevel` | `Warning` | Minimum level sent to Telegram |
| `ParseMode` | `Html` | Wire encoding: `None`, `MarkdownV2`, or `Html` (see note below) |
| `OutputTemplate` | level + timestamp + message + exception | Message rendering template |
| `DisableNotification` | `false` | Deliver silently |
| `MessageThreadId` | `null` | Forum (topic) thread id |
| `BatchSizeLimit` | `10` | Max events per outgoing message |
| `Period` | `2s` | Flush interval |
| `QueueLimit` | `10000` | Max events buffered while Telegram is unreachable |
| `RequestTimeout` | `30s` | Per-request HTTP timeout (own `HttpClient` only) |
| `MaxSendRetries` | `3` | Extra attempts on HTTP 429, honouring `retry_after` |
| `ApiBaseUrl` | `https://api.telegram.org` | Override for a local Bot API server |

### A note on `ParseMode`

`ParseMode` controls how the rendered text is **encoded on the wire**, not whether
your template text becomes bold/italic/links. Each event is rendered to plain text
and then escaped so it is shown literally under the selected mode — for example, in
`Html` mode `<`, `>` and `&` are escaped, and in `MarkdownV2` mode the reserved
characters are escaped. Markdown/HTML markup in your `OutputTemplate` is therefore
displayed verbatim rather than interpreted. Use `None` to send text with no
`parse_mode` at all.

## Diagnostics

The sink reports failures through Serilog's `SelfLog`:

```csharp
Serilog.Debugging.SelfLog.Enable(Console.Error);
```

## Building from source

```bash
dotnet build
dotnet test
```

## License

[MIT](LICENSE)
