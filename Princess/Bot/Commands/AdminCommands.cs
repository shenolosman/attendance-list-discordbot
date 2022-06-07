﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Princess.Services;

namespace Princess.Bot.Commands;

public class AdminCommands : BaseCommandModule
{
    [Command("Registerteacher")]
    [Description("Gives a user teacher role, can only be used by owner/admin. " +
                 "For the bot to work as intended please use !RegisterTeacher when you want to add them to the role. " +
                 "(The teacher role has admin privileges)")]
    public async Task RegisterTeacher(CommandContext cmdCtx,
        [Description("The user to give teacher role to")]
        DiscordMember newTeacher)
    {
        if (cmdCtx.Member.IsOwner || (cmdCtx.Member.Permissions & Permissions.Administrator) != 0)
        {
            await using var scope = cmdCtx.Services.CreateAsyncScope();
            var presenceHandler = scope.ServiceProvider.GetRequiredService<PresenceHandler>();
            var classToAdd = await presenceHandler.GetClass(cmdCtx.Guild.Id);

            //return and send message if the teacher already exists in db
            if (await presenceHandler.TeacherExists(newTeacher.Id, classToAdd))
            {
                var failedEmbed = new DiscordEmbedBuilder
                {
                    Title = "Teacher exists",
                    Description = "The member is already a teacher",
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        IconUrl = cmdCtx.User.AvatarUrl,
                        Name = cmdCtx.User.Username
                    },

                    Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                    {
                        Url = cmdCtx.Client.CurrentUser.AvatarUrl
                    },
                    Color = DiscordColor.Gold
                };
                await cmdCtx.Message.Channel.SendMessageAsync(failedEmbed);
                return;
            }

            var serverRoles = cmdCtx.Guild.Roles;

            //deletes student role if exists
            if (await presenceHandler.StudentExists(newTeacher.Id, classToAdd))
            {
                foreach (var role in serverRoles)
                    if (role.Value.Name == "Student")
                        await newTeacher.RevokeRoleAsync(role.Value);

                await presenceHandler.RemoveStudentFromClassInDb(newTeacher.Id, classToAdd);
            }

            //sets the teacher role
            foreach (var role in serverRoles)
                if (role.Value.Name == "Teacher")
                    await newTeacher.GrantRoleAsync(role.Value);

            var newTeacherEmbed = new DiscordEmbedBuilder
            {
                Title = "New Teacher!",
                Description = $"{newTeacher.Nickname ?? newTeacher.Username} is now a teacher!",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = cmdCtx.User.AvatarUrl,
                    Name = cmdCtx.Member.Nickname ?? cmdCtx.Member.Username
                },

                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Url = cmdCtx.Client.CurrentUser.AvatarUrl
                },
                Color = DiscordColor.Gold
            };

            await presenceHandler.RegisterTeacherToDatabase(newTeacher, classToAdd);

            await cmdCtx.Message.Channel.SendMessageAsync(newTeacherEmbed);
        }
    }
}