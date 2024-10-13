using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HertaBot
{
    internal class Program
    {
        private readonly DiscordSocketClient client;
        private InteractionService interactionService;
        private static IServiceProvider _serviceProvider;

        public Program()
        {
            _serviceProvider = CreateProvider();
            DiscordSocketConfig config = new DiscordSocketConfig
            {
                GatewayIntents = Discord.GatewayIntents.AllUnprivileged | Discord.GatewayIntents.MessageContent
            };
            this.client = new DiscordSocketClient(config);
        }

        static IServiceProvider CreateProvider()
        {
            var collection = new ServiceCollection();
            //...
            return collection.BuildServiceProvider();
        }

        public async Task StartBotAsync()
        {
            client.Log += message =>
            {
                Console.WriteLine(message);
                return Task.CompletedTask;
            };
            client.Ready += ClientReady;
            await this.client.StartAsync();
            string token = System.IO.File.ReadAllText("C:\\Users\\kuzzz\\source\\repos\\HertaBot\\HertaBot\\token.txt");
            await this.client.LoginAsync(Discord.TokenType.Bot, token);
            await Task.Delay(-1);
        }

        public async Task ClientReady()
        {
            InteractionServiceConfig config = new InteractionServiceConfig()
            {
                AutoServiceScopes = true
            };
            this.interactionService = new InteractionService(client.Rest, config);
            await interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);
            await interactionService.RegisterCommandsGloballyAsync();
            client.InteractionCreated += async interaction =>
            {
                var scope = _serviceProvider.CreateScope();
                var ctx = new SocketInteractionContext(client, interaction);
                await interactionService.ExecuteCommandAsync(ctx, _serviceProvider);
            };
            var messageHandler = new MessageHandler();
            this.client.MessageReceived += messageHandler.Handler;
            this.client.MessageUpdated += messageHandler.EditHandler;
            this.client.ReactionAdded += messageHandler.ReactHandler;
        }

        static async Task Main(string[] args)
        {
            var myBot = new Program();
            await myBot.StartBotAsync();
        }
    }
}
