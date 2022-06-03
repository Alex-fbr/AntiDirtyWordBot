using System.Text;
using System.Xml.Serialization;

using AntiDirtyWordBot.Common;
using AntiDirtyWordBot.Configurations;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using Newtonsoft.Json.Linq;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using static AntiDirtyWordBot.Common.CommandsSettingsXml;

namespace AntiDirtyWordBot.Services.Implementation
{
    public class UpdateHandlerService : IUpdateHandlerService
    {
        private const string Aswer = "Напишите матное слово";

        private readonly ILogger<Worker> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly CommandsSettingsXml _settings;
        private ObsceneWordsOption _obsceneWordsOption;

        private readonly ReplyKeyboardMarkup replyStartMarkup = new(new KeyboardButton("/start"))
        {
            OneTimeKeyboard = true,
            ResizeKeyboard = true
        };

        public IConfiguration Configuration { get; }

        public UpdateHandlerService(
            ILogger<Worker> logger,
            ITelegramBotClient telegramBotClient,
            IOptionsMonitor<ObsceneWordsOption> appSettings,
            IConfiguration configuration)
        {
            Configuration = configuration;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _botClient = telegramBotClient ?? throw new ArgumentNullException(nameof(telegramBotClient));
            _obsceneWordsOption = appSettings.CurrentValue ?? throw new ArgumentNullException(nameof(appSettings));
            var settingsFilePath = Path.Combine(Environment.CurrentDirectory, "settings.xml");

            if (System.IO.File.Exists(settingsFilePath))
            {
                var ser = new XmlSerializer(typeof(CommandsSettingsXml));
                using var reader = new StreamReader(settingsFilePath);
                _settings = ser.Deserialize(reader) as CommandsSettingsXml;
                reader.Close();
            }
            else
            {
                _logger.LogError($"{settingsFilePath} DOESN'T EXISTS");
                throw new ArgumentNullException(nameof(_settings));
            }

            ChangeToken.OnChange(() => Configuration.GetReloadToken(), () =>
            {
                _obsceneWordsOption = Configuration.GetSection(nameof(ObsceneWordsOption)).Get<ObsceneWordsOption>();
            });
        }


        public Task GetHandler(Update update) => update.Type switch
        {
            UpdateType.Message => BotOnMessageReceived(update.Message),
            UpdateType.EditedMessage => BotOnMessageReceived(update.Message),
            UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery),
            UpdateType.ChannelPost => Task.Run(() => _logger.LogInformation("ChannelPost")),
            UpdateType.EditedChannelPost => Task.Run(() => _logger.LogInformation("EditedChannelPost")),
            UpdateType.ShippingQuery => Task.Run(() => _logger.LogInformation("ShippingQuery")),
            UpdateType.PreCheckoutQuery => Task.Run(() => _logger.LogInformation("PreCheckoutQuery")),
            UpdateType.Poll => Task.Run(() => _logger.LogInformation("Poll")),  // Poll - опрос 
            UpdateType.PollAnswer => Task.Run(() => _logger.LogInformation("PollAnswer")),
            _ => throw new NotImplementedException($"Странный UpdateType {update.Type}")
        };

        async Task BotOnMessageReceived(Message message)
        {
            _logger.LogInformation("Recieve <--- {@message}", message);

            if (message.Type != MessageType.Text)
            {
                await _botClient.SendTextMessageAsync(chatId: message.Chat.Id, text: Properties.Resources.DoNotUnderstand, replyMarkup: replyStartMarkup);
                return;
            }

            if (message.ReplyToMessage != null && !string.IsNullOrEmpty(message.ReplyToMessage.Text))
            {
                switch (message.ReplyToMessage.Text)
                {
                    case Aswer:
                        var word = message.Text?.ToLower()?.Trim();

                        if (_obsceneWordsOption.ObsceneWords.Contains(word))
                        {
                            await SendTextMessage(message.Chat.Id, "Такое слово уже есть в словаре", message.MessageId);
                        }
                        else
                        {
                            var words = new List<string>(_obsceneWordsOption.ObsceneWords)
                            {
                                word
                            };
                            var array = new JArray(words.ToArray());
                            AppSetting.AddOrUpdateAppSetting("ObsceneWordsOption:ObsceneWords", array);
                            await SendTextMessage(message.Chat.Id, "Успешное сохранение", message.MessageId);
                        }

                        return;
                }
            }

            var code = message.Text.Split(' ').First();
            var botCommand = _settings.BotCommandList.FirstOrDefault(x => x.Code == code);

            await (botCommand == null
                ? SendTextMessage(message.Chat.Id, CreateMessageForClient(message), message.MessageId)
                : botCommand.Type switch
                {
                    CommandType.InlineKeyboards => InlineKeyboardsCommandHandler(message.Chat.Id, botCommand.KeyboardButtonList, botCommand.Description),
                    CommandType.Message => SendTextMessage(message.Chat.Id, botCommand.Description),
                    CommandType.MessageWithResponse => MessageWithResponseCommandHandler(message.Chat.Id, botCommand.Description),
                    _ => throw new NotImplementedException($"Странная команда {botCommand.Type}")
                });
        }

        async Task BotOnCallbackQueryReceived(CallbackQuery callbackQuery)
        {
            _logger.LogInformation("Recieve <--- {@callbackQuery}", callbackQuery);

            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id);
            await _botClient.SendChatActionAsync(callbackQuery.Message.Chat.Id, ChatAction.Typing);

            var chatId = callbackQuery.Message.Chat.Id;
            var botCommand = _settings.BotCommandList.FirstOrDefault(x => x.Code == callbackQuery.Data);

            await (botCommand.Type switch
            {
                CommandType.InlineKeyboards => InlineKeyboardsCommandHandler(chatId, botCommand.KeyboardButtonList, botCommand.Description),
                CommandType.Message => SendTextMessage(chatId, botCommand.Description),
                CommandType.MessageWithResponse => MessageWithResponseCommandHandler(chatId, botCommand.Description),
                _ => throw new NotImplementedException($"Странная команда {botCommand.Type}")
            });
        }

        async Task MessageWithResponseCommandHandler(long chatId, string text, int? replyToMessageId = null)
        {
            _logger.LogInformation($"Send    ---> '{text}'");

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                replyMarkup: new ForceReplyMarkup { Selective = true });
        }

        async Task InlineKeyboardsCommandHandler(long chatId, List<List<Keyboard>> keyboards, string text = null)
        {
            await _botClient.SendChatActionAsync(chatId, ChatAction.Typing);
            var inlineKeyboard = new List<List<InlineKeyboardButton>>();

            foreach (var (inlineKeyboardRow, list) in from inlineKeyboardRow in keyboards
                                                      let list = new List<InlineKeyboardButton>()
                                                      select (inlineKeyboardRow, list))
            {
                list.AddRange(inlineKeyboardRow.Select(ik => InlineKeyboardButton.WithCallbackData(ik.DisplayToUser, ik.CallbackData)));
                inlineKeyboard.Add(list);
            }

            await SendTextMessage(chatId, text ?? Properties.Resources.Choose, null, new InlineKeyboardMarkup(inlineKeyboard));
        }

        async Task SendTextMessage(long chatId, string text, int? replyToMessageId = null, IReplyMarkup replyMarkup = null)
        {
            _logger.LogInformation($"Send    ---> '{text}'");

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                replyToMessageId: replyToMessageId,
                replyMarkup: replyMarkup ?? replyStartMarkup);
        }

        private string CreateMessageForClient(Message message)
        {
            var (result, fragment, word) = Check(message.Text);
            var log = result ? $"Тест содержит мат. Фрагмент '{fragment}' похож на '{word}'" : "Текст не содержит мат";
            return log;
        }

        // TODO: вынести в отдельный сервис InspectorService
        private (bool result, string? fragment, string? word) Check(string originalText)
        {
            var inputStr = new StringBuilder();
            var phrase = PreparePhrase(originalText);

            foreach (var word in _obsceneWordsOption?.ObsceneWords)
            {
                for (int part = 0; part < phrase.Length; part++)
                {
                    inputStr.Clear();
                    inputStr.Append(phrase.AsSpan(part, phrase.Length - part > word.Length ? word.Length : phrase.Length - part));
                    var fragment = inputStr.ToString();

                    var distance = ObsceneWordsInspector.GetDistance(fragment, word);
                    var normilize = word.Length <= 4 ? 1.0 : word.Length * 0.20;

                    if (distance <= normilize)
                    {
                        if (fragment.Trim().Equals(word))
                        {
                            var exceptions = _obsceneWordsOption?.ExceptionWords.FirstOrDefault(x => x.Key == word)?.Value;

                            if (exceptions == null)
                            {
                                return (true, fragment, word);
                            }

                            if (exceptions.All(x => !phrase.Contains(x)))
                            {
                                return (true, fragment, word);
                            }
                        }
                    }
                }
            }

            return (false, null, null);
        }

        private string PreparePhrase(string originalText)
        {
            var inputStringBuilder = new StringBuilder(originalText.ToLower().Replace(" ", ""));

            foreach (var item in _obsceneWordsOption?.Alphabet)
            {
                foreach (var letter in item.Value)
                {
                    foreach (var phr in inputStringBuilder.ToString().ToCharArray())
                    {
                        if (letter == phr.ToString())
                        {
                            inputStringBuilder.Replace(phr, item.Key);
                        }
                    }
                }
            }

            return inputStringBuilder.ToString();
        }
    }
}
