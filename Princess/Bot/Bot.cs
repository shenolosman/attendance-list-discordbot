﻿using System.Text;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Princess.Bot.Commands;
using Princess.Data;
using Princess.Models;

namespace Princess.Bot;

public class Bot
{
    public Bot(IServiceProvider services)
    {
        _Services = services;
    }

    public DiscordClient Client { get; private set; }
    public CommandsNextExtension Commands { get; private set; }
    public IServiceProvider _Services { get; }

    public async Task RunAsync()
    {
        var json = string.Empty;

        using (var fs = File.OpenRead("BotConfig.json"))
        using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
        {
            json = await sr.ReadToEndAsync();
        }

        var configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

        var config = new DiscordConfiguration
        {
            Token = configJson.Token,
            TokenType = TokenType.Bot,
            AutoReconnect = true,
            MinimumLogLevel = LogLevel.Debug
        };

        Client = new DiscordClient(config);

        Client.Ready += OnClientReady;

        Client.UseInteractivity(new InteractivityConfiguration
        {
            // DeleteEmojis after a poll is default. is ok to change
            PollBehaviour = PollBehaviour.DeleteEmojis,
            // Timeout is how long time you will have to react to a command if nothing else is said in a command.
            Timeout = TimeSpan.FromMinutes(10)
        });

        var commandConfig = new CommandsNextConfiguration
        {
            StringPrefixes = new[] {configJson.Prefix},
            EnableDms = true,
            EnableMentionPrefix = true,
            DmHelp = true,
            // Set CaseSensitive to true if we want to make commands case sensitive!
            CaseSensitive = false,

            Services = _Services
        };

        Commands = Client.UseCommandsNext(commandConfig);

        // Add Command classes here for them to work
        Commands.RegisterCommands<AdminCommands>();

        Commands.RegisterCommands<TeacherCommands>();

        Commands.RegisterCommands<StudentCommands>();

        await Client.ConnectAsync();

        await Task.Delay(-1);
    }

    // When bot starts it will check if guild(class) exists in DB - If StudentRole & TeacherRole doesn't exist, create the roles on the server.
    private async Task<Task> OnClientReady(DiscordClient sender, ReadyEventArgs e)
    {
        var listOfGuilds = new List<DiscordGuild>();

        // Only contains ID of guilds
        var botGuilds = sender.Guilds.Values;

        foreach (var guild in botGuilds)
        {
            // Fetches the rest of the information about the guild
            var fetchedGuild = await sender.GetGuildAsync(guild.Id, true);
            listOfGuilds.Add(fetchedGuild);
        }

        await using (var scope = Commands.Services.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<PresenceDbContext>();

            foreach (var guild in listOfGuilds)
            {
                var guildInDB = await ctx.Classes.AnyAsync(c => c.Id == guild.Id);

                // Adds guild to class in DB if it doesnt exist
                if (!guildInDB)
                {
                    var schoolClass = new Class
                    {
                        Id = guild.Id,
                        Name = guild.Name
                    };
                    try
                    {
                        await ctx.Classes.AddAsync(schoolClass);
                        await ctx.SaveChangesAsync();
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        throw;
                    }
                }

                // Check for teacher and student roles and create them if they doesn't exist
                var guildRoles = guild.Roles;

                if (guildRoles == null)
                {
                    await guild.CreateRoleAsync("Teacher", Permissions.Administrator, DiscordColor.Goldenrod, true,
                        true);
                    await guild.CreateRoleAsync("Student",
                        Permissions.SendMessages |
                        Permissions.ChangeNickname |
                        Permissions.AttachFiles |
                        Permissions.Speak |
                        Permissions.Stream |
                        Permissions.UseVoice |
                        Permissions.AccessChannels,
                        DiscordColor.CornflowerBlue, null, true,
                        "This role is needed to send a presence check to all students in guild");

                    var noticeRoleCreationEmbed = new DiscordEmbedBuilder
                    {
                        Title = "Creation of Roles",
                        Description =
                            "A Teacher and Student Role has been created.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            IconUrl = sender.CurrentUser.AvatarUrl,
                            Name = sender.CurrentUser.Username
                        },

                        Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                        {
                            Url = sender.CurrentUser.AvatarUrl
                        },
                        Color = DiscordColor.Gold
                    };

                    var guildChannels = guild.Channels.Values;

                    var channelToMessage = guildChannels.FirstOrDefault(channel => channel.Type == ChannelType.Text);

                    try
                    {
                        await sender.SendMessageAsync(channelToMessage, noticeRoleCreationEmbed);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        throw;
                    }
                }

                var teacherRoleExists = guildRoles.Values.Any(r => r.Name.ToLower() == "teacher");
                var studentRoleExists = guildRoles.Values.Any(r => r.Name.ToLower() == "student");

                if (!teacherRoleExists)
                {
                    await guild.CreateRoleAsync("Teacher", Permissions.Administrator, DiscordColor.Goldenrod, true,
                        true);

                    var noticeRoleCreationEmbed = new DiscordEmbedBuilder
                    {
                        Title = "Creation Of Teacher Role",
                        Description =
                            "A Teacher Role has been created. For the bot to work as intended please use !RegisterTeacher when you want to add them to the role.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            IconUrl = sender.CurrentUser.AvatarUrl,
                            Name = sender.CurrentUser.Username
                        },

                        Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                        {
                            Url = sender.CurrentUser.AvatarUrl
                        },
                        Color = DiscordColor.Gold
                    };

                    var guildChannels = guild.Channels.Values;

                    var channelToMessage = guildChannels.FirstOrDefault(channel => channel.Type == ChannelType.Text);

                    try
                    {
                        await sender.SendMessageAsync(channelToMessage, noticeRoleCreationEmbed);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        throw;
                    }
                }

                if (!studentRoleExists)
                {
                    await guild.CreateRoleAsync("Student",
                        Permissions.SendMessages |
                        Permissions.ChangeNickname |
                        Permissions.AttachFiles |
                        Permissions.Speak |
                        Permissions.Stream |
                        Permissions.UseVoice |
                        Permissions.AccessChannels,
                        DiscordColor.CornflowerBlue, null, true,
                        "This role is needed to send a presence check to all students in guild");

                    var noticeRoleCreationEmbed = new DiscordEmbedBuilder
                    {
                        Title = "Creation Of Student Role",
                        Description =
                            "A Student Role has been created. The role will be distributed after a !presence command is used.",
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            IconUrl = sender.CurrentUser.AvatarUrl,
                            Name = sender.CurrentUser.Username
                        },

                        Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                        {
                            Url = sender.CurrentUser.AvatarUrl
                        },
                        Color = DiscordColor.Gold
                    };

                    var guildChannels = guild.Channels.Values;

                    var channelToMessage = guildChannels.FirstOrDefault(channel => channel.Type == ChannelType.Text);

                    try
                    {
                        await sender.SendMessageAsync(channelToMessage, noticeRoleCreationEmbed);
                    }
                    catch (Exception exception)
                    {
                        Console.WriteLine(exception);
                        throw;
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}