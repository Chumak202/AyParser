using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class TrackObject
{
    public delegate void MessageCallback(string message);

    private readonly MessageCallback callback;
    public AyParser parser { get; }
    public uint id { get; }

    private static volatile uint id_counter = 0;

    private CancellationToken token;
    private readonly CancellationTokenSource tokenSource;

    public TrackObject(AyParser parser, MessageCallback callback)
    {
        this.parser = parser;
        this.callback = callback;
        this.id = Interlocked.Increment(ref id_counter);
        tokenSource = new CancellationTokenSource();
        _ = StartTracking(new CancellationTokenSource().Token);
    }

    private async Task StartTracking(CancellationToken token)
    {
        this.token = token;
        await Task.Run(parseCycle, token);
    }

    public void StopTracking()
    {
        tokenSource.Cancel();
    }

    private async Task parseCycle()
    {
        while (!token.IsCancellationRequested)
        {
            await parser.parseAsync();
            getNewItems();
            Thread.Sleep(1000 * 6);
        }
    }

    private void getNewItems()
    {
        StringBuilder sb = new();
        int i = 0;
        while (parser.newItems.Count > 0 && i++ < 20)
        {
            var item = parser.newItems.Dequeue();
            sb.Append(item.ToString());
            sb.Append('\n');
        }
        if (sb.Length > 0)
        {
            callback(sb.ToString());
        }
    }
}

class TrackChat
{
    public long id { get; }

    public List<TrackObject> trackObjects { get; }

    public readonly TrackObject.MessageCallback callback;

    public TrackChat(long id, TrackObject.MessageCallback callback)
    {
        this.id = id;
        this.callback = callback;
        trackObjects = new();
    }
}

class AyBot
{
    private TelegramBotClient client;
    System.Threading.CancellationTokenSource cancellationTokenSource;

    Dictionary<long, TrackChat> chats;

    private static readonly string helpMessage =
        @"track price url - добавляет товар в список отслеживаемых, где 
            price - максимальная цена товара;
            url - ссылка для отслеживания определённой категории товаров";

    public AyBot(string token)
    {
        client = new TelegramBotClient(token);
        chats = new();
    }
    public async Task Start()
    {
        if (cancellationTokenSource != null && cancellationTokenSource.Token.CanBeCanceled)
        {
            return;
        }
        Stop();
        cancellationTokenSource = new CancellationTokenSource();
        // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
        ReceiverOptions receiverOptions = new ReceiverOptions()
        {
            AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
        };

        client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cancellationTokenSource.Token);

        var me = await client.GetMeAsync();

        Console.WriteLine($"Start listening for @{me.Username}");
    }

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        // Only process Message updates: https://core.telegram.org/bots/api#message
        if (update.Message is not { } message)
            return;
        // Only process text messages
        if (message.Text is not { } messageText)
            return;

        var chatId = message.Chat.Id;

        Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

        if (!chats.ContainsKey(chatId))
        {
            chats[chatId] = new TrackChat(chatId, async (message) =>
            {
                try
                {
                    Message sentMessage = await botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: message);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                }
            });
        }

        if (messageText.StartsWith("track"))
        {
            string[] splitted = messageText.Split(" ");
            float maxPrice = 0;
            if (splitted.Length != 3 || !float.TryParse(splitted[1], out maxPrice))
            {
                return; // TODO: send error message
            }

            chats[chatId].trackObjects.Add(new TrackObject(
                new AyParser(splitted[2], maxPrice),
                chats[chatId].callback));
        }
        else if (messageText.StartsWith("rm"))
        {

        }
        else if (messageText.StartsWith("ls"))
        {
            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: ls(chatId),
                cancellationToken: cancellationToken);

        }
        else
        {
            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: helpMessage,
                cancellationToken: cancellationToken);

        }
    }

    private string ls(long chatId)
    {
        StringBuilder lsMessage = new StringBuilder();
        TrackChat trackChat = chats[chatId];
        List<TrackObject> trackObjects = trackChat.trackObjects;
        foreach (TrackObject trackObject in trackObjects)
        {
            lsMessage.Append(trackObject.id);
            lsMessage.Append(" ");
            lsMessage.Append(trackObject.parser.url);
            lsMessage.Append("\n");
        }
        return lsMessage.ToString();
    }

    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }
}
