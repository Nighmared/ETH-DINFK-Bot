﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DuckSharp;
using ETHDINFKBot.Stats;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations.Schema;
using ETHDINFKBot.Log;
using ETHBot.DataLayer;
using ETHBot.DataLayer.Data.Discord;
using ETHBot.DataLayer.Data;
using ETHBot.DataLayer.Data.Enums;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Net;

namespace ETHDINFKBot
{
    class Program
    {
        public static DiscordSocketClient Client;
        private CommandService commands;

        private IServiceProvider services;
        private static IConfiguration Configuration;
        private static string DiscordToken { get; set; }
        public static ulong Owner { get; set; }

        // TODO one object and somewhere else but im lazy
        public static string RedditAppId { get; set; }
        public static string RedditRefreshToken { get; set; }
        public static string RedditAppSecret { get; set; }
        public static string BasePath { get; set; }

        public static ILoggerFactory Logger { get; set; }


        private static List<BotChannelSetting> BotChannelSettings;


        private static BotStats BotStats = new BotStats()
        {
            DiscordUsers = new List<Stats.DiscordUser>()
        };


        private static GlobalStats GlobalStats = new GlobalStats()
        {
            EmojiInfoUsage = new List<EmojiInfo>(),
            PingInformation = new List<PingInformation>()
        };

        private static List<ReportInfo> BlackList = new List<ReportInfo>();

        private DatabaseManager DatabaseManager = DatabaseManager.Instance();
        private LogManager LogManager = new LogManager(DatabaseManager.Instance());



        private static void CheckDirs()
        {
            //if (!Directory.Exists("Database"))
            //    Directory.CreateDirectory("Database");

            //if (!Directory.Exists("Plugins"))
            //    Directory.CreateDirectory("Plugins");

            //if (!Directory.Exists("Logs"))
            //     Directory.CreateDirectory("Logs");

            //if (!Directory.Exists("Stats"))
            //    Directory.CreateDirectory("Stats");

            //if (!Directory.Exists("Blacklist"))
            //    Directory.CreateDirectory("Blacklist");

            //if (!Directory.Exists("Blacklist\\Backup"))
            //    Directory.CreateDirectory("Blacklist\\Backup");

            //if (!Directory.Exists("Stats\\Backup"))
            //    Directory.CreateDirectory("Stats\\Backup");
        }

        static void Main(string[] args)
        {
            CheckDirs();
            Logger = LoggerFactory.Create(builder => { builder.AddConsole(); });

            /* using (ETHBotDBContext context = new ETHBotDBContext())
             {

                 context.DiscordUsers.Add(new ETHBot.DataLayer.Data.Discord.DiscordUser()
                 {
                     AvatarUrl = "",
                     DiscordUserId = (ulong)new Random().Next(1, 100000),
                     DiscriminatorValue = 1,
                     IsBot = false,
                     IsWebhook = false,
                     JoinedAt = DateTime.Now,
                     Nickname = "test1",
                     Username = "username"
                 }); ;

                 context.SaveChanges();
             }
            */

            //CheckDirs();

            Configuration = new ConfigurationBuilder()
              .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
              .Build();

            DiscordToken = Configuration["DiscordToken"];
            Owner = Convert.ToUInt64(Configuration["Owner"]);
            BasePath = Configuration["BasePath"];

            RedditAppId = Configuration["Reddit:AppId"];
            RedditRefreshToken = Configuration["Reddit:RefreshToken"];
            RedditAppSecret = Configuration["Reddit:AppSecret"];


            BackupDBOnStartup();

            new Program().MainAsync(DiscordToken).GetAwaiter().GetResult();

        }


        private static Assembly LoadPlugin(string relativePath)
        {
            // Navigate up to the solution root
            string root = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(
                    Path.GetDirectoryName(
                        Path.GetDirectoryName(
                            Path.GetDirectoryName(
                                Path.GetDirectoryName(typeof(Program).Assembly.Location)))))));

            string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            Console.WriteLine($"Loading commands from: {pluginLocation}");
            PluginLoadContext loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(new AssemblyName(Path.GetFileNameWithoutExtension(pluginLocation)));
        }

        public static void BackupDBOnStartup()
        {
            var path = Path.Combine(BasePath, "Database", "ETHBot.db");
            if (File.Exists(path))
            {
                var path2 = Path.Combine(BasePath, "Database", "Backup", $"ETHBot_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.db");
                File.Copy(path, path2);
            }
        }


        public static void BackUpStats()
        {
            File.Copy("Stats\\stats.json", $"Stats\\Backup\\stats_{DateTime.Now.ToString("yyyyMMdd_hhmmss")}.json");
        }

        public static void BackUpGlobalStats()
        {
            File.Copy("Stats\\global_stats.json", $"Stats\\Backup\\global_stats_{DateTime.Now.ToString("yyyyMMdd_hhmmss")}.json");
        }
        public static void BackUpBlackList()
        {
            File.Copy("Blacklist\\blacklist.json", $"Blacklist\\Backup\\blacklist_{DateTime.Now.ToString("yyyyMMdd_hhmmss")}.json");
        }

        public static void LoadStats()
        {
            Console.WriteLine("LoadStats");
            Console.WriteLine(Environment.CurrentDirectory);

            if (File.Exists(Path.Combine(BasePath, "Stats", "stats.json")))
            {
                //BackUpStats();
                string content = File.ReadAllText(Path.Combine(BasePath, "Stats", "stats.json"));
                BotStats = JsonConvert.DeserializeObject<BotStats>(content);
                Console.WriteLine("LoadStats found" + BotStats.DiscordUsers.Count);
            }
        }

        public static void LoadBlacklist()
        {
            Console.WriteLine("LoadBlacklist");
            if (File.Exists(Path.Combine(BasePath, "Blacklist", "blacklist.json")))
            {
                Console.WriteLine("LoadBlacklist found");
                //BackUpBlackList();
                string content = File.ReadAllText(Path.Combine(BasePath, "Blacklist", "blacklist.json"));
                BlackList = JsonConvert.DeserializeObject<List<ReportInfo>>(content);

                // Remove duplicates
                BlackList = BlackList.GroupBy(i => i.ImageUrl).Select(i => i.First()).ToList();

                Console.WriteLine("LoadBlacklist found" + BlackList.Count);
            }
        }

        public static void LoadGlobalStats()
        {
            Console.WriteLine("LoadGlobalStats");
            if (File.Exists(Path.Combine(BasePath, "Stats", "global_stats.json")))
            {
                //BackUpGlobalStats();
                string content = File.ReadAllText(Path.Combine(BasePath, "Stats", "global_stats.json"));
                GlobalStats = JsonConvert.DeserializeObject<GlobalStats>(content);
                Console.WriteLine("LoadGlobalStats found" + GlobalStats.EmojiInfoUsage.Count);
            }
        }

        public static void SaveStats()
        {
            string content = JsonConvert.SerializeObject(BotStats);
            File.WriteAllText("Stats\\stats.json", content);
        }

        public static void SaveGlobalStats()
        {
            string content = JsonConvert.SerializeObject(GlobalStats);
            File.WriteAllText("Stats\\global_stats.json", content);
        }

        public static void SaveBlacklist()
        {
            string content = JsonConvert.SerializeObject(BlackList);
            File.WriteAllText("Blacklist\\blacklist.json", content);
        }

        private static void MigrateToSqliteDb()
        {


            using (ETHBotDBContext context = new ETHBotDBContext())
            {
                if (context.CommandTypes.Count() == 0)
                {
                    // TODO check for updates
                    var count = Enum.GetValues(typeof(ETHBot.DataLayer.Data.Enums.BotMessageType)).Length;
                    var types = new List<CommandType>();
                    for (var i = 0; i < count; i++)
                    {
                        types.Add(new CommandType
                        {
                            CommandTypeId = i,
                            Name = ((ETHBot.DataLayer.Data.Enums.BotMessageType)i).ToString()
                        });
                    }
                    context.CommandTypes.AddRange(types);
                    context.SaveChanges();
                }
                return; // no longer needed


                if (context.CommandStatistics.Count() == 0)
                {
                    foreach (var item in BotStats.DiscordUsers)
                    {
                        var user = context.DiscordUsers.SingleOrDefault(i => i.DiscordUserId == item.DiscordId);
                        if (user == null)
                        {
                            context.DiscordUsers.Add(new ETHBot.DataLayer.Data.Discord.DiscordUser()
                            {
                                DiscordUserId = item.DiscordId,
                                DiscriminatorValue = item.DiscordDiscriminator,
                                //AvatarUrl = item.ReportedBy.,
                                IsBot = false,
                                IsWebhook = false,
                                Nickname = item.ServerUserName,
                                Username = item.DiscordName//,
                                //JoinedAt = null
                            });
                            context.SaveChanges();
                        }

                        user = context.DiscordUsers.SingleOrDefault(i => i.DiscordUserId == item.DiscordId);



                        if (item.Stats.TotalNeko > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 1);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalNeko
                            });
                        }

                        if (item.Stats.TotalSearch > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 2);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalSearch
                            });
                        }

                        if (item.Stats.TotalNekoGif > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 3);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalNekoGif
                            });
                        }

                        if (item.Stats.TotalHolo > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 4);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalHolo
                            });
                        }


                        if (item.Stats.TotalWaifu > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 5);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalWaifu
                            });
                        }

                        if (item.Stats.TotalBaka > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 6);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalBaka
                            });
                        }

                        if (item.Stats.TotalSmug > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 7);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalSmug
                            });
                        }

                        if (item.Stats.TotalFox > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 8);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalFox
                            });
                        }

                        if (item.Stats.TotalAvatar > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 9);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalAvatar
                            });
                        }


                        if (item.Stats.TotalNekoAvatar > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 10);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalNekoAvatar
                            });
                        }


                        if (item.Stats.TotalWallpaper > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 11);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalWallpaper
                            });
                        }


                        if (item.Stats.TotalAnimalears > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 12);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalAnimalears
                            });
                        }

                        if (item.Stats.TotalFoxgirl > 0)
                        {
                            var type = context.CommandTypes.Single(i => i.CommandTypeId == 13);
                            context.CommandStatistics.Add(new CommandStatistic()
                            {
                                Type = type,
                                DiscordUser = user,
                                Count = item.Stats.TotalFoxgirl
                            });
                        }



                        context.SaveChanges();

                    }




                }

                if (context.PingStatistics.Count() == 0)
                {
                    foreach (var item in GlobalStats.PingInformation)
                    {
                        var user = context.DiscordUsers.SingleOrDefault(i => i.DiscordUserId == item.DiscordUser.DiscordId);
                        if (user == null)
                        {
                            context.DiscordUsers.Add(new ETHBot.DataLayer.Data.Discord.DiscordUser()
                            {
                                DiscordUserId = item.DiscordUser.DiscordId,
                                DiscriminatorValue = item.DiscordUser.DiscordDiscriminator,
                                //AvatarUrl = item.ReportedBy.,
                                IsBot = false,
                                IsWebhook = false,
                                Nickname = item.DiscordUser.ServerUserName,
                                Username = item.DiscordUser.DiscordName//,
                                //JoinedAt = null
                            });
                            context.SaveChanges();
                        }

                        user = context.DiscordUsers.SingleOrDefault(i => i.DiscordUserId == item.DiscordUser.DiscordId);


                        context.PingStatistics.Add(new PingStatistic()
                        {
                            DiscordUser = user,
                            PingCount = item.PingCount,
                            PingCountOnce = item.PingCountOnce,
                            PingCountBot = 0
                        });


                    }
                }

                context.SaveChanges();

                if (context.EmojiHistory.Count() == 0)
                {
                    foreach (var item in GlobalStats.EmojiInfoUsage)
                    {
                        context.EmojiStatistics.Add(new EmojiStatistic()
                        {
                            Animated = item.Animated,
                            CreatedAt = item.CreatedAt,
                            EmojiId = item.EmojiId,
                            EmojiName = item.EmojiName,
                            Url = item.Url,
                            UsedAsReaction = item.UsedAsReaction,
                            UsedInText = item.UsedInText,
                            UsedInTextOnce = item.UsedInTextOnce
                        });

                        context.SaveChanges();

                        var stat = context.EmojiStatistics.Single(i => i.EmojiId == item.EmojiId);

                        for (int i = 0; i < item.UsedInTextOnce; i++)
                        {
                            context.EmojiHistory.Add(new EmojiHistory()
                            {
                                Count = 1,
                                IsReaction = false,
                                DateTimePosted = DateTime.Now,
                                EmojiStatistic = stat
                            });
                        }

                        context.EmojiHistory.Add(new EmojiHistory()
                        {
                            Count = item.UsedInText - item.UsedInTextOnce,
                            IsReaction = false,
                            DateTimePosted = DateTime.Now,
                            EmojiStatistic = stat
                        });

                        for (int i = 0; i < item.UsedAsReaction; i++)
                        {
                            context.EmojiHistory.Add(new EmojiHistory()
                            {
                                Count = 1,
                                IsReaction = true,
                                DateTimePosted = DateTime.Now,
                                EmojiStatistic = stat
                            });
                        }

                        context.SaveChanges();
                    }
                }

                context.SaveChanges();

                Console.WriteLine("LoadBlacklist CHECK " + BlackList.Count);
                if (context.BannedLinks.Count() == 0)
                {
                    // not migrated yet

                    Console.WriteLine("LoadBlacklist MIGRATION " + BlackList.Count);

                    foreach (var item in BlackList)
                    {
                        var user = context.DiscordUsers.SingleOrDefault(i => i.DiscordUserId == item.ReportedBy.DiscordId);
                        if (user == null)
                        {
                            context.DiscordUsers.Add(new ETHBot.DataLayer.Data.Discord.DiscordUser()
                            {
                                DiscordUserId = item.ReportedBy.DiscordId,
                                DiscriminatorValue = item.ReportedBy.DiscordDiscriminator,
                                //AvatarUrl = item.ReportedBy.,
                                IsBot = false,
                                IsWebhook = false,
                                Nickname = item.ReportedBy.ServerUserName,
                                Username = item.ReportedBy.DiscordName//,
                                                                      //JoinedAt = null
                            });
                            context.SaveChanges();
                        }

                        user = context.DiscordUsers.SingleOrDefault(i => i.DiscordUserId == item.ReportedBy.DiscordId);

                        if (item.ImageUrl.Contains("discordapp") || !item.ImageUrl.StartsWith("https://") || context.BannedLinks.Any(i => i.Link == item.ImageUrl))
                        {
                            continue; // clean up wrong blocks
                        }

                        context.BannedLinks.Add(new ETHBot.DataLayer.Data.Discord.BannedLink()
                        {
                            ByUser = user,
                            Link = item.ImageUrl,
                            ReportTime = item.ReportedAt == DateTime.MinValue ? DateTime.Now : item.ReportedAt
                        });

                    }

                    context.SaveChanges();
                }


            }
        }

        public static void LoadChannelSettings()
        {
            BotChannelSettings = DatabaseManager.Instance().GetAllChannelSettings();
        }

        public async Task MainAsync(string token)
        {
            DatabaseManager.Instance().SetAllSubredditsStatus();
            LoadChannelSettings();

            //LoadStats();
            //LoadGlobalStats();
            //LoadBlacklist();
            //MigrateToSqliteDb();

            var config = new DiscordSocketConfig { MessageCacheSize = 250 };
            Client = new DiscordSocketClient(config);

            Client.MessageReceived += HandleCommandAsync;
            Client.ReactionAdded += Client_ReactionAdded;
            Client.ReactionRemoved += Client_ReactionRemoved;

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();

#if DEBUG
            await Client.SetGameAsync($"DEV MODE");
#else
            await Client.SetGameAsync($"with a neko");
#endif

            services = new ServiceCollection()
                .AddSingleton(Client)
                .AddSingleton<InteractiveService>()
                .BuildServiceProvider();

            commands = new CommandService();
            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);



            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (((SocketGuildUser)arg3.User).IsBot == true)
                return Task.CompletedTask;

            Console.ForegroundColor = ConsoleColor.Red;
            //Console.WriteLine($"Removed emote {arg3.Emote.Name} by {arg3.User.Value.Username}");
            LogManager.RemoveReaction((Emote)arg3.Emote, ((SocketGuildUser)arg3.User).IsBot);

            if (((Emote)arg3.Emote).Id == 780179874656419880)
            {
                // TODO Remove the post from saved
            }

            return Task.CompletedTask;
        }

        private bool AllowedToRun(ulong channelId, BotPermissionType type)
        {
            var channelSettings = DatabaseManager.GetChannelSetting(channelId);
            if (((BotPermissionType)channelSettings?.ChannelPermissionFlags).HasFlag(type))
            {
                return true;
            }

            return false;
        }


        private Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            try
            {
                /*if ( == true)
                    return Task.CompletedTask;*/

                Console.ForegroundColor = ConsoleColor.Green;
                //Console.WriteLine($"Added emote {arg3.Emote.Name} by {arg3.User.Value.Username}");
                LogManager.AddReaction((Emote)arg3.Emote, ((SocketGuildUser)arg3.User).IsBot);

                if (((Emote)arg3.Emote).Id == 780179874656419880 && !arg3.User.Value.IsBot)
                {
                    // Save the post link

                    /*          var user = DatabaseManager.GetDiscordUserById(arg1.Value.Author.Id); // Verify the user is created but should actually be available by this poitn
                    var saveBy = DatabaseManager.GetDiscordUserById(arg3.User.Value.Id); // Verify the user is created but should actually be available by this poitn
                    */

                    if (DatabaseManager.IsSaveMessage(arg1.Value.Id, arg3.User.Value.Id))
                    {
                        // dont allow double saves
                        return Task.CompletedTask;
                    }

                    var guildChannel = (SocketGuildChannel)arg1.Value.Channel;
                    var link = $"https://discord.com/channels/{guildChannel.Guild.Id}/{guildChannel.Id}/{arg1.Value.Id}";
                    if (!string.IsNullOrWhiteSpace(arg1.Value.Content))
                    {
                        DatabaseManager.SaveMessage(arg1.Value.Id, arg1.Value.Author.Id, arg3.User.Value.Id, link, arg1.Value.Content);
                        // TODO parse to guild user

                        arg3.User.Value.SendMessageAsync($"Saved post from {arg1.Value.Author.Username}: " +
                            $"{Environment.NewLine} {arg1.Value.Content} {Environment.NewLine}Direct link: [{guildChannel.Guild.Name}/{guildChannel.Name}/by {arg1.Value.Author.Username}] <{link}>");
                    }

                    foreach (var item in arg1.Value.Embeds)
                    {
                        DatabaseManager.SaveMessage(arg1.Value.Id, arg1.Value.Author.Id, arg3.User.Value.Id, link, "Embed: " + ((Embed)item).ToString());
                        arg3.User.Value.SendMessageAsync("", false, ((Embed)item));
                    }


                    if (arg1.Value.Attachments.Count > 0)
                    {
                        //return Task.CompletedTask;

                        foreach (var item in arg1.Value.Attachments)
                        {
                            DatabaseManager.SaveMessage(arg1.Value.Id, arg1.Value.Author.Id, arg3.User.Value.Id, link, item.Url);

                            /*DatabaseManager.SaveMessage(new SavedMessage()
                            {
                                DirectLink = link,
                                SendInDM = false,
                                Content = item.Url, // todo attachment save to disk
                                MessageId = arg1.Value.Id,
                                ByDiscordUserId = user.DiscordUserId,
                                ByDiscordUser = user,
                                SavedByDiscordUserId = saveBy.DiscordUserId,
                                SavedByDiscordUser = saveBy
                            });*/
                            // TODO markdown

                            arg3.User.Value.SendMessageAsync($"Saved post (Attachment) from {arg1.Value.Author.Username}: " +
                                $"{Environment.NewLine} {item.Url}{Environment.NewLine}Direct link: [{guildChannel.Guild.Name}/{guildChannel.Name}/by {arg1.Value.Author.Username}] <{link}>");
                        }

                    }

                    if (AllowedToRun(arg1.Value.Channel.Id, BotPermissionType.SaveMessage))
                    {
                        EmbedBuilder builder = new EmbedBuilder();

                        builder.WithTitle($"{arg3.User.Value.Username} saved a message");
                        //builder.WithUrl("https://github.com/BattleRush/ETH-DINFK-Bot");
                        //builder.WithDescription($@"");
                        builder.WithColor(0, 0, 255);

                        //builder.WithThumbnailUrl("https://cdn.discordapp.com/avatars/774276700557148170/62279315dd469126ca4e5ab89a5e802a.png");

                        builder.WithCurrentTimestamp();
                        builder.AddField("Message Link", $"[Message]({link})", true);

                        builder.AddField("Message Author", $"{arg1.Value.Author.Username}", true);

                        // TODO More stats

                        arg1.Value.Channel.SendMessageAsync("", false, builder.Build());
                    }
                    // TODO markdown -> guild user also
                    //arg1.Value.Channel.SendMessageAsync($"{arg3.User.Value.Username} saves <{link}> by {arg1.Value.Author.Username}");
                }

            }
            catch (Exception ex)
            {

            }

            return Task.CompletedTask;
        }

        private static Dictionary<ulong, DateTime> SpamCache = new Dictionary<ulong, DateTime>();
        public async Task HandleCommandAsync(SocketMessage m)
        {
            if (!(m is SocketUserMessage msg)) return;

            await LogManager.ProcessEmojisAndPings(m.Tags, m.Author.Id, ((SocketGuildUser)m.Author).IsBot);
            // TODO private channels

            var dbManager = DatabaseManager.Instance();

            DiscordServer discordServer = null;
            DiscordChannel discordChannel = null;
            BotChannelSetting channelSettings = null;
            if (msg.Channel is SocketGuildChannel guildChannel)
            {
                channelSettings = BotChannelSettings?.SingleOrDefault(i => i.DiscordChannelId == guildChannel.Id);

                discordServer = dbManager.GetDiscordServerById(guildChannel.Guild.Id);
                if (discordServer == null)
                {
                    discordServer = dbManager.CreateDiscordServer(new ETHBot.DataLayer.Data.Discord.DiscordServer()
                    {
                        DiscordServerId = guildChannel.Guild.Id,
                        ServerName = guildChannel.Guild.Name
                    });
                }

                discordChannel = dbManager.GetDiscordChannel(guildChannel.Id);
                if (discordChannel == null)
                {
                    discordChannel = dbManager.CreateDiscordChannel(new ETHBot.DataLayer.Data.Discord.DiscordChannel()
                    {
                        DiscordChannelId = guildChannel.Id,
                        ChannelName = guildChannel.Name,
                        DiscordServerId = discordServer.DiscordServerId
                    });
                }
            }
            else
            {
                // NO DM Tracking
                return;
            }

            if (channelSettings == null && m.Author.Id != Owner)
            {
                // No perms for this channel
                return;
            }

            var dbAuthor = dbManager.GetDiscordUserById(msg.Author.Id);
            // todo check for update
            if (dbAuthor == null)
            {
                var user = (SocketGuildUser)msg.Author; // todo check non socket user

                dbAuthor = dbManager.CreateUser(new ETHBot.DataLayer.Data.Discord.DiscordUser()
                {
                    DiscordUserId = user.Id,
                    DiscriminatorValue = user.DiscriminatorValue,
                    //AvatarUrl = item.ReportedBy.,
                    IsBot = user.IsBot,
                    IsWebhook = user.IsWebhook,
                    Nickname = user.Nickname,
                    Username = user.Username,
                    JoinedAt = user.JoinedAt
                });

                dbAuthor = dbManager.GetDiscordUserById(msg.Author.Id);
            }
            else
            {
                // TODO Update user
            }


            if (m.Author.Id != Owner && !((BotPermissionType)channelSettings?.ChannelPermissionFlags).HasFlag(BotPermissionType.Read))
            {
                // Cant read
                return;
            }
            /*
                        if (channelSettings.DiscordChannelId == 747754931905364000 || channelSettings.DiscordChannelId == 747768907992924192 || channelSettings.DiscordChannelId == 774322847812157450 || channelSettings.DiscordChannelId == 774322031688679454
                            || channelSettings.DiscordChannelId == 773914288913514546)
                        {
                            // staff / bot / adminlog / modlog / teachingassistants
                            // TODO settings better
                        }*/

            if (channelSettings != null && ((BotPermissionType)channelSettings?.ChannelPermissionFlags).HasFlag(BotPermissionType.Read))
            {
                dbManager.CreateDiscordMessage(new ETHBot.DataLayer.Data.Discord.DiscordMessage()
                {
                    //Channel = discordChannel,
                    DiscordChannelId = discordChannel.DiscordChannelId,
                    //DiscordUser = dbAuthor,
                    DiscordUserId = dbAuthor.DiscordUserId,
                    MessageId = msg.Id,
                    Content = msg.Content
                });
            }

            var message = msg.Content;
            var randVal = msg.Author.DiscriminatorValue % 10;

            // TODO Different color for defcom bot
            switch (randVal)
            {
                case 0:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
                case 1:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case 2:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case 3:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case 4:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case 5:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case 6:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case 7:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                case 8:
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    break;
                case 9:
                    Console.ForegroundColor = ConsoleColor.DarkMagenta;
                    break;
                default:
                    break;
            }

            //Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}] {msg.Author} wrote: {msg.Content}");
            //File.AppendAllText($"Logs\\ETHDINFK_{DateTime.Now:yyyy_MM_dd}_spam.txt", $"[{DateTime.Now:yyyy.MM.dd HH:mm:ss}] " + msg.Author + " wrote: " + msg.Content + Environment.NewLine);

            if (m.Channel.Id == 747758757395562557 || m.Channel.Id == 758293511514226718 || m.Channel.Id == 747758770393972807 ||
            m.Channel.Id == 774286694794919989)
            {
                // TODO Channel ID as config
                await m.AddReactionAsync(Emote.Parse("<:this:747783377662378004>"));
                await m.AddReactionAsync(Emote.Parse("<:that:758262252699779073>"));
            }


            if (m.Author.IsBot)
                return;

            if (msg.Content.StartsWith("."))
            {
                // check if the emoji exists and if the emojis is animated
                string name = msg.Content.Substring(1, msg.Content.Length - 1);

                var emoji = DatabaseManager.GetEmojiByName(name);

                if (emoji != null)
                {
                    var guildUser = msg.Author as SocketGuildUser;

                    msg.DeleteAsync();

                    var emoteString = $"<{(emoji.Animated ? "a" : "")}:{emoji.EmojiName}:{emoji.EmojiId}>";

                    if (guildChannel.Guild.Emotes.Any(i => i.Id == emoji.EmojiId))
                    {
                        // we can post the emote as it will be rendered out
                        await msg.Channel.SendMessageAsync(emoteString);
                    }
                    else
                    {
                        // TODO store resized images in db for faster reuse
                        if (emoji.Animated)
                        {
                            // TODO gif resize
                            await msg.Channel.SendMessageAsync(emoji.Url);
                        }
                        else
                        {
                            using (WebClient client = new WebClient())
                            {
                                var imageBytes = client.DownloadData(emoji.Url);

                                using (var ms = new MemoryStream(imageBytes))
                                {
                                    var image = System.Drawing.Image.FromStream(ms);
                                    var resImage = ResizeImage(image, 48, 48);

                                    var stream = new MemoryStream();

                                    resImage.Save(stream, resImage.RawFormat);
                                    stream.Position = 0;


                                    await msg.Channel.SendFileAsync(stream, $"{emoji.EmojiName}.png");
                                }
                            }
                        }
                        // we need to send the image as the current server doesnt have access
                        
                    }

                    await msg.Channel.SendMessageAsync($"by {guildUser.Username}");

                    return;
                }
            }


            int argPos = 0;
            if (!(msg.HasStringPrefix(".", ref argPos)))
            {
                return;
            }

            if (m.Author.Id != Owner)
            {
                if (SpamCache.ContainsKey(m.Author.Id))
                {
                    if (SpamCache[m.Author.Id] > DateTime.Now.AddMilliseconds(-500))
                    {
                        SpamCache[m.Author.Id] = SpamCache[m.Author.Id].AddMilliseconds(750);

                        // TODO save last no spam message time
                        if (new Random().Next(0, 20) == 0)
                        {
                            // Ignore the user than to reply takes 1 message away from the rate limit
                            m.Channel.SendMessageAsync($"Stop spamming <@{m.Author.Id}> your current timeout is {SpamCache[m.Author.Id]}ms");
                        }

                        return;
                    }

                    SpamCache[m.Author.Id] = DateTime.Now;
                }
                else
                {
                    SpamCache.Add(m.Author.Id, DateTime.Now);
                }
            }

            Console.ResetColor();


            var context = new SocketCommandContext(Client, msg);
            await commands.ExecuteAsync(context, argPos, services);
        }



        // source https://stackoverflow.com/questions/1922040/how-to-resize-an-image-c-sharp

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(System.Drawing.Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
        private async static void Test()
        {

        }
    }
}
