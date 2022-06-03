using AntiDirtyWordBot;
using AntiDirtyWordBot.Configurations;
using AntiDirtyWordBot.Services;
using AntiDirtyWordBot.Services.Implementation;

using Serilog;

using Telegram.Bot;

IConfiguration? Configuration = null;

var host =
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostContext) =>
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        })
        .ConfigureAppConfiguration((hostContext, builder) =>
        {
            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                builder.AddUserSecrets<Program>();
            }
        })
        .ConfigureLogging((hostContext, builder) =>
        {
            builder.AddConsole();

            if (hostContext.HostingEnvironment.IsDevelopment())
            {
                var logger = new LoggerConfiguration()
                          .ReadFrom.Configuration(hostContext.Configuration)
                          .Enrich.FromLogContext()
                          .CreateLogger();
                builder.ClearProviders();
                builder.AddSerilog(logger);
            }
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddSingleton<ITelegramBotClient>(sc =>
            {
                var botConfig = hostContext.Configuration.GetSection("BotConfig").Get<BotConfig>();
                return new TelegramBotClient(botConfig.Token);
            });

            services.Configure<ObsceneWordsOption>(hostContext.Configuration.GetSection(nameof(ObsceneWordsOption)));
            services.AddSingleton<IUpdateHandlerService, UpdateHandlerService>();
            services.AddHostedService<Worker>();
        })
        .Build();

await host.RunAsync();