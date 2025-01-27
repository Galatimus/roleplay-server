﻿using AltV.Net;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Roleplay.DiscordBOT
{
    public class Main
    {
        public static async Task MainAsync()
        {
            try
            {
                using var services = ConfigureServices();
                Global.DiscordClient = services.GetRequiredService<DiscordSocketClient>();

                Global.DiscordClient.Log += LogAsync;

                await services.GetRequiredService<CommandHandlingService>().InitializeAsync();
                await Global.DiscordClient.LoginAsync(TokenType.Bot, Global.DiscordBotToken);
                await Global.DiscordClient.StartAsync();
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {
                Alt.LogError(ex.Message);
            }
        }

        private static Task LogAsync(LogMessage log)
        {
            Alt.LogWarning(log.ToString());
            return Task.CompletedTask;
        }

        private static ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                .AddSingleton<HttpClient>()
                .BuildServiceProvider();
        }
    }
}