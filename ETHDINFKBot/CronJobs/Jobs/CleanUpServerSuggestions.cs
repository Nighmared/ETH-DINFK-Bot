﻿using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ETHDINFKBot.CronJobs.Jobs
{ 
    public class CleanUpServerSuggestions : CronJobService
    {
        private readonly ulong ServerSuggestion = 816776685407043614; // todo config?
        private readonly ILogger<CleanUpServerSuggestions> _logger;
        private readonly string Name = "CleanUpServerSuggestions";

        public CleanUpServerSuggestions(IScheduleConfig<CleanUpServerSuggestions> config, ILogger<CleanUpServerSuggestions> logger)
            : base(config.CronExpression, config.TimeZoneInfo)
        {
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("{Name} starts.");
            return base.StartAsync(cancellationToken);
        }

        private async void CleanUpOldMessages(SocketTextChannel channel, TimeSpan toDeleteOlderThan)
        {
            DateTime oneWeekAgo = DateTime.Now.Add(toDeleteOlderThan);
            ulong oneWeekAgoSnowflake = SnowflakeUtils.ToSnowflake(oneWeekAgo);
            var oldMessages = await channel.GetMessagesAsync(oneWeekAgoSnowflake, Direction.Before, 100/*100 should be enought for a while*/).FlattenAsync();
            await channel.DeleteMessagesAsync(oldMessages);

            //var messageDelete = await channel.SendMessageAsync($"Deleting {oldMessages.Count()} messages"); // enable when this message is correct
            //Task.Delay(TimeSpan.FromMinutes(5));
            //messageDelete.DeleteAsync();
        }

        public override Task DoWork(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{DateTime.Now:hh:mm:ss} {Name} is working.");

            try
            {
                foreach (var item in Program.Client.Guilds)
                {
                    var channel = item.GetTextChannel(ServerSuggestion);
                    if (channel != null)
                    {
                        CleanUpOldMessages(channel, TimeSpan.FromDays(-7));
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError("Error cleaning up suggestions", ex);
            }

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"{Name}  is stopping.");
            return base.StopAsync(cancellationToken);
        }
    }
}
