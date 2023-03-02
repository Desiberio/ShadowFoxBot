using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Discord.Webhook;
using Discord;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Google.Apis.YouTube.v3.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections;
using Discord.Net;

namespace ShadowFoxBotNet
{
    class Program
    {
        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();
        public async Task MainAsync()
        {
            string token = File.ReadAllText("./apiKeys/discordBotToken.txt");
            DiscordBot bot = new DiscordBot();
            bot.Start(token);
            //Block this task until program is closed.
            await Task.Delay(-1);
        }


    }
}
