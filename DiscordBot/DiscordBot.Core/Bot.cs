﻿namespace DiscordBot.Core
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;

    using DiscordBot.Interfaces;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public class Bot : IHostedService
    {
        private readonly ICommandService commandService;

        private readonly IConfiguration configuration;

        private readonly IHostEnvironment environment;

        private readonly ILogger<Bot> logger;

        private DiscordSocketClient client;

        public Bot(IConfiguration configuration, ICommandService commandService, ILogger<Bot> logger,
                   IHostEnvironment environment)
        {
            this.configuration = configuration;
            this.commandService = commandService;
            this.logger = logger;
            this.environment = environment;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                LogStartup();
                await commandService.AddModulesAsync();
                client = new DiscordSocketClient();
                string token = GetToken();
                await client.LoginAsync(TokenType.Bot, token);
                await client.StartAsync();

                client.MessageReceived += HandleMessageReceived;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "There was an unhandled exception during startup.");
                await StopAsync(cancellationToken);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await client.StopAsync();
            client.Dispose();
            client = null;
        }

        private string GetToken()
        {
            return configuration.GetSection("AppSettings")["Token"];
        }

        private async Task HandleMessageReceived(SocketMessage rawMessage)
        {
            // Ignore system messages and messages from bots
            if (!(rawMessage is SocketUserMessage message))
            {
                return;
            }

            if (message.Source != MessageSource.User)
            {
                return;
            }

            var commandContext = new SocketCommandContext(client, message);

            var argumentPosition = 0;
            if (!message.HasMentionPrefix(client.CurrentUser, ref argumentPosition))
            {
                IResult defaultResponseResult =
                    await commandService.ExecuteDefaultResponse(commandContext, argumentPosition);
                await LogErrorResultAsync(commandContext, defaultResponseResult);
                return;
            }

            IResult result = await commandService.ExecuteAsync(commandContext, argumentPosition);
            await LogErrorResultAsync(commandContext, result);
        }

        private async Task LogErrorResultAsync(SocketCommandContext commandContext, IResult result)
        {
            if (result.Error.HasValue && result.Error.Value != CommandError.UnknownCommand)
            {
                await commandContext.Channel.SendMessageAsync(result.ToString());
            }
        }

        private void LogStartup()
        {
            logger.LogInformation("Environment: {environment}", environment.EnvironmentName);
            logger.LogInformation("PID: {pid}", Process.GetCurrentProcess().Id);
        }
    }
}