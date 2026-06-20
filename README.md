# Serilog.Sinks.Telegram.Bot

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
dotnet add package Serilog.Sinks.Telegram.Bot
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

### Finding your `chatId`

`chatId` is a **chat** id, not a personal Telegram handle. It may be:

- A **numeric id** — e.g. `987654321` for a user/group, or a negative id such as
  `-1001234567890` for a supergroup/channel. This is the canonical form and works for
  any chat. Find it by messaging your bot and reading `chat.id` from
  `https://api.telegram.org/bot<token>/getUpdates`.
- An **`@username`** — e.g. `@my_channel`. This only works for **public**
  channels/groups that have a username; it won't work for private chats, private
  groups, or direct messages to a user.

Your own `@username` can't be used to make the bot DM you — use the numeric chat id
from the conversation you started with the bot.

## Full configuration

For complete control, pass `TelegramBotSinkOptions`:

```csharp
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Telegram.Bot;

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
| `ChatId` | _(required)_ | Numeric chat id (any chat) or a **public channel/group** `@username` (not a personal handle) — see [Finding your `chatId`](#finding-your-chatid) |
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

## Configuration via `appsettings.json`

The sink can be configured from `IConfiguration` using
[Serilog.Settings.Configuration](https://github.com/serilog/serilog-settings-configuration):

```bash
dotnet add package Serilog.Settings.Configuration
```

A minimal configuration only needs the token and chat id:

```jsonc
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Telegram.Bot" ],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "TelegramBot",
        "Args": {
          "botToken": "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11",
          "chatId": "987654321"
        }
      }
    ]
  }
}
```

Every option is bindable. `TimeSpan` values use the `hh:mm:ss` format:

```jsonc
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Telegram.Bot" ],
    "WriteTo": [
      {
        "Name": "TelegramBot",
        "Args": {
          "botToken": "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11",
          "chatId": "@my_channel",
          "restrictedToMinimumLevel": "Warning",
          "parseMode": "Html",
          "outputTemplate": "{Level:u3} {Timestamp:yyyy-MM-dd HH:mm:ss}{NewLine}{Message:lj}{NewLine}{Exception}",
          "disableNotification": true,
          "messageThreadId": 42,
          "batchSizeLimit": 10,
          "period": "00:00:02",
          "queueLimit": 10000,
          "requestTimeout": "00:00:30",
          "maxSendRetries": 3,
          "apiBaseUrl": "https://api.telegram.org"
        }
      }
    ]
  }
}
```

Wire it up at startup by reading the configuration:

```csharp
using Microsoft.Extensions.Configuration;
using Serilog;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

In ASP.NET Core, pass the host configuration instead:

```csharp
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(context.Configuration));
```

> [!NOTE]
> The `Using` array (or an assembly-scanning setup) is what lets Serilog discover
> the `TelegramBot` sink. Each `Args` key maps to a parameter of the same name on
> `WriteTo.TelegramBot(...)`, and any subset may be supplied — omitted keys fall
> back to their defaults. Keep secrets such as `botToken` out of source control —
> use user secrets, environment variables, or a secrets manager.

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
