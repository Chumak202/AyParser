using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
            Thread.Sleep(1000 * 20);
        }
    }

    private void getNewItems()
    {
        StringBuilder sb = new();
        int i = 0;
        while (parser.newItems.Count > 0 && i++ < 20)
        {
            var item = parser.newItems.Dequeue();
            sb.Append(item.ToString() + '\t' +item.link);
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
        @"1) Что-бы отследить товар необходимо ввести: track минимальная цена максимальная цена ссылка (пример: track 35 55 ссылка)" +
         "\n" + "\n" + @"2) ls - выводит список всех отслеживаемых ссылок;" + "\n" + "\n" + @"3) rm id (пример: rm 4) - удаляет отслеживаемую ссылку по id, полученному из списка ls";

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
            track(messageText, chatId);
            Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Ссылка добавлена для отслеживания",
                    cancellationToken: cancellationToken);
        }
        else if (messageText.StartsWith("rm"))
        {
            if (rm(messageText, chatId))
            {
                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Выбранная ссылка удалена из списка",
                    cancellationToken: cancellationToken);
            }
        }
        else if (messageText.StartsWith("ls"))
        {
            string msg = ls(chatId);
            if (msg.Length > 0)
            {
                Message sentMessage = await botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: msg,
                    cancellationToken: cancellationToken);
            }

        }
        else
        {
            Message sentMessage = await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: helpMessage,
                cancellationToken: cancellationToken);

        }
    }

    private void track(string messageText, long chatId)
    {
        string[] splitted = messageText.Split(" ");
        float maxPrice; float minPrice;
        if (splitted.Length != 4 || !float.TryParse(splitted[2], out maxPrice) || !float.TryParse(splitted[1], out minPrice))
        {
            return; // TODO: send error message
        }

        chats[chatId].trackObjects.Add(new TrackObject(
            new AyParser(splitted[3], maxPrice, minPrice),
            chats[chatId].callback));
    }

    private bool rm(string request, long chatId)
    {
        string[] splitted = request.Split(" ");
        int deleteId;
        if (splitted.Length != 2 || !int.TryParse(splitted[1], out deleteId))
        {
            return false; // TODO: send error message
        }

        TrackChat trackChat = chats[chatId];
        List<TrackObject> trackObjects = trackChat.trackObjects;
        TrackObject deleteObject = null;
        foreach (TrackObject trackObject in trackObjects)
        {
            if (trackObject.id == deleteId)
            {
                deleteObject = trackObject;
                break;
            }
        }
        if (deleteObject != null)
        {
            deleteObject.StopTracking();
            trackObjects.Remove(deleteObject);
            return true;
        }
        return false;
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
