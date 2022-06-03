﻿using System.Net.Http.Headers;
using System.Web;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using Newtonsoft.Json;
using Princess.Bot.Services;
using Princess.Data;
using Princess.Models;
using Princess.Services;

namespace Princess.Bot.Commands
{
    public class TeacherCommands : BaseCommandModule
    {

        [Command("attendanceQuestion")] //rename command? presenceQuiz?
        [Description("Initiates an Presence-check, the only one who can do it is users with the 'Teacher' role. " +
                     "Students answer to a question and you will get who was present.")] 
        [RequireRoles(RoleCheckMode.Any, "Teacher")]
        public async Task AttendanceQuestion(CommandContext cmdCtx,
            [Description("ex 10s or 10m or 10h")] TimeSpan reactionDuration)
        {
            await using (var scope = cmdCtx.Services.CreateAsyncScope())
            {
                var triviaQuestions = scope.ServiceProvider.GetRequiredService<TriviaQuestions>();
                var triviaQuizList = await triviaQuestions.GetAttendanceQuestions();

                 var stringReplace = String.Empty;

                     stringReplace += triviaQuizList[0].QuestionString
                         .Replace("&quot;", "\"")
                         .Replace("&#039;", "'")
                         .Replace("&deg;", "°")
                         .Replace("&amp;", "&")
                         .Replace("&pi;", "π")
                         .Replace("&rdquo;", "\"")
                         .Replace("&ldquo;", "\"");

                     var quizReplaceEmbed = new DiscordEmbedBuilder
                     {
                         Title = $"\n{stringReplace} ",
                         Description = $"\n1." +
                                       $" {triviaQuizList[0].CorrectAnswer}, " +   // [0] first item in the TriviaQuizList
                                       $"2. {triviaQuizList[0].IncorrectAnswers[0]}, " + 
                                       $"3. {triviaQuizList[0].IncorrectAnswers[1]}, " +
                                       $"4. {triviaQuizList[0].IncorrectAnswers[2]} ",
                         Author = new DiscordEmbedBuilder.EmbedAuthor
                         {
                             IconUrl = cmdCtx.User.AvatarUrl,
                             Name = cmdCtx.User.Username,
                         },
 
                         Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                         {
                             Url = cmdCtx.Client.CurrentUser.AvatarUrl
                         },
                         Footer = new DiscordEmbedBuilder.EmbedFooter()
                         {
                             Text = $"Category: {triviaQuizList[0].Category}, Difficulty: {triviaQuizList[0].Difficulty}"
                         },
                         Color = DiscordColor.Gold,
                     };

                     var quizMessage = await cmdCtx.Channel.SendMessageAsync(embed: quizReplaceEmbed);
                     
                // Creates Discord-Emojis for the question 
                var answerOne = DiscordEmoji.FromName(cmdCtx.Client, ":one:");
                var answerTwo = DiscordEmoji.FromName(cmdCtx.Client, ":two:");
                var answerThree = DiscordEmoji.FromName(cmdCtx.Client, ":three:");
                var answerFour = DiscordEmoji.FromName(cmdCtx.Client, ":four:");

                // Places the Discord-Emojis on the message
                await quizMessage.CreateReactionAsync(answerOne);
                await quizMessage.CreateReactionAsync(answerTwo);
                await quizMessage.CreateReactionAsync(answerThree);
                await quizMessage.CreateReactionAsync(answerFour);

                // Working with interactivity
                var interactivity = cmdCtx.Client.GetInteractivity();

                var quizAnswers = await interactivity.CollectReactionsAsync(quizMessage, reactionDuration);

                var userNameWhoReacted = new List<string>();
                var everyOnesReaction = new List<DiscordUser>();

                int totalFirstAnswers = 0;
                int totalSecondAnswers = 0;
                int totalThirdAnswers = 0;
                int totalFourthAnswers = 0;

                foreach (var answer in quizAnswers)
                {
                    if (answer.Emoji == answerOne)
                    {
                        foreach (var user in answer.Users)
                        {
                            var member = cmdCtx.Guild.Members.Values.FirstOrDefault(x => x.Id == user.Id);
                            if (member != null && !user.IsBot)
                            {
                                totalFirstAnswers++;
                                userNameWhoReacted.Add(member.Nickname ?? member.Username);
                            }
                        }
                    }
                    if (answer.Emoji == answerTwo)
                    {
                        foreach (var user in answer.Users)
                        {
                            var member = cmdCtx.Guild.Members.Values.FirstOrDefault(x => x.Id == user.Id);
                            if (member != null && !user.IsBot)
                            {
                                totalSecondAnswers++;
                                userNameWhoReacted.Add(member.Nickname ?? member.Username);
                            }
                        }
                    }
                    if (answer.Emoji == answerThree)
                    {
                        foreach (var user in answer.Users)
                        {
                            var member = cmdCtx.Guild.Members.Values.FirstOrDefault(x => x.Id == user.Id);
                            if (member != null && !user.IsBot)
                            {
                                totalThirdAnswers++;
                                userNameWhoReacted.Add(member.Nickname ?? member.Username);
                            }
                        }
                    }
                    if (answer.Emoji == answerFour)
                    {
                        foreach (var user in answer.Users)
                        {
                            var member = cmdCtx.Guild.Members.Values.FirstOrDefault(x => x.Id == user.Id);
                            if (member != null && !user.IsBot)
                            {
                                totalFourthAnswers++;
                                userNameWhoReacted.Add(member.Nickname ?? member.Username);
                            }
                        }
                    }
                }

                // Collecting all answers in a list, just one answer per user
                var containsEmojis = quizAnswers.Any(x =>
                    x.Emoji == answerOne || x.Emoji == answerTwo || x.Emoji == answerThree || x.Emoji == answerFour);

                foreach (var result in quizAnswers)
                {
                    if (containsEmojis)
                    {
                        var isBot = result.Users.Any(x => x.IsBot);

                        foreach (var user in result.Users)
                        {
                            if (!isBot && !everyOnesReaction.Contains(user)) everyOnesReaction.Add(user);
                        }
                    }
                }

                if (totalFirstAnswers > 0 || totalSecondAnswers > 0 ||
                    totalThirdAnswers > 0 ||
                    totalFourthAnswers > 0)
                {

                    await cmdCtx.Channel.SendMessageAsync($"Result of the question:");
                    await cmdCtx.Channel.SendMessageAsync($":one: {totalFirstAnswers}");
                    await cmdCtx.Channel.SendMessageAsync($":two: {totalSecondAnswers}");
                    await cmdCtx.Channel.SendMessageAsync($":three: {totalThirdAnswers}");
                    await cmdCtx.Channel.SendMessageAsync($":four: {totalFourthAnswers}");

                    foreach (var user in everyOnesReaction)
                    {
                        var username = user.Username;

                        await cmdCtx.Channel.SendMessageAsync(string.Join("\n", username));
                        // or userNameWhoReacted 
                    }
                    await cmdCtx.Channel.SendMessageAsync(
                        $"The correct answer: {triviaQuizList[0].CorrectAnswer}");
                }
                else
                {
                    await cmdCtx.Channel.SendMessageAsync($"None answered...none is present today!");
                }
            }
        }
        


        [Command("PresenceCheck")]
        [Description("Initiates an Presence-check, the only one who can do it is users with the 'Teacher' role. Students react with an thumbs up Emoji and you will get who was present.")]
        [RequireRoles(RoleCheckMode.Any, "Teacher")]
        public async Task PresenceCheck(CommandContext cmdCtx, [Description("ex 10s or 10m or 10h")]TimeSpan reactionDuration)
        {
            var discordGuildRoles = cmdCtx.Guild.Roles;

            var guildRoles = discordGuildRoles.ToList();

            bool studentRoleExists = false;
            foreach (var role in guildRoles)
            {
                if (role.Value.Name.ToLower() == "student")
                {
                    studentRoleExists = true;
                    break;
                }
            }

            if (!studentRoleExists)
            {
                try
                {
                    // Add or Delete Permissions as done in the params below if needed. This will change permissions for the "student-role" When and if its created.
                    await cmdCtx.Guild.CreateRoleAsync("Student", Permissions.SendMessages | Permissions.ChangeNickname | Permissions.AttachFiles | Permissions.Speak | Permissions.Stream | Permissions.UseVoice | Permissions.AccessChannels, DiscordColor.CornflowerBlue, null, true, "This role is needed to send a presence check to all students in guild");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            
            discordGuildRoles = cmdCtx.Guild.Roles;

            var studentRole = discordGuildRoles.FirstOrDefault(role => role.Value.Name.ToLower() == "student");

            string mentionStudent = "";

            if (studentRole.Value != null)
                mentionStudent = studentRole.Value.Mention;
            else
                mentionStudent = "Students";

            // Do changes in here to make changes on first message sent when AttendenceCheck command is ran.
            var presenceEmbed = new DiscordEmbedBuilder
            {
                Title = "Attendence",
                Description = $"{mentionStudent} - This is a Presence-check. In the future there will be an question for you to answer here to see if you are present. But for now you only need to :+1:, to answer that you are present",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = cmdCtx.User.AvatarUrl,
                    Name = cmdCtx.User.Username,
                },

                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                {
                   Url = cmdCtx.Client.CurrentUser.AvatarUrl
                },
                Color = DiscordColor.Gold,
            };

            await cmdCtx.Message.DeleteAsync();

            var dmEmbed = new DiscordEmbedBuilder
            {
                Title = "Attendence",
                Description = $"Your teacher in \"{cmdCtx.Guild.Name}\" has made an presence-check in the <#{cmdCtx.Channel.Id}> channel. You have 15 minutes to thumb up that message, otherwise you will be set as absent to that lecture",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = cmdCtx.User.AvatarUrl,
                    Name = cmdCtx.User.Username,
                },

                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                {
                    Url = cmdCtx.Client.CurrentUser.AvatarUrl
                },
                Color = DiscordColor.Gold,
            };

            // Sends the embeded message from above in the channel the command was initiated.
            var presenceMessage = await cmdCtx.Channel.SendMessageAsync(embed: presenceEmbed);

            // This is the part where all who is active on channel gets a DM that an presence-check is started. Doesnt work as intented yet.
            foreach (var user in cmdCtx.Channel.Users)
            {
                try
                {
                    if (!user.IsBot)
                        await user.SendMessageAsync(embed: dmEmbed);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }

            // Creates an Discord-Emoji
            var thumbsUpEmoji = DiscordEmoji.FromName(cmdCtx.Client, ":+1:");

            // Puts the Discord-Emoji on the message
            await presenceMessage.CreateReactionAsync(thumbsUpEmoji);
            
            // This code makes it possible for messages to be reacted to, use interactivity to interact.
            var interactivity = cmdCtx.Client.GetInteractivity();
            
            var testResult = await interactivity.CollectReactionsAsync(presenceMessage, reactionDuration);

            var allWhoThumbedUpUsernames = new List<string>();
            var allWhoThumbedUp = new List<DiscordUser>();

            int totalThumbsUp = 0;

            foreach (var result in testResult)
            {
                if (result.Emoji == thumbsUpEmoji)
                {
                    foreach (var user in result.Users)
                    {
                        if (user.IsBot)
                        {
                            totalThumbsUp--;
                        }
                        else
                        {
                            var member = cmdCtx.Guild.Members.Values.FirstOrDefault(x => x.Id == user.Id);
                            if (member != null)
                            {
                                totalThumbsUp++;
                                allWhoThumbedUpUsernames.Add(member.Nickname ?? member.Username);
                            }
                        }
                    }
                }
            }

            if (totalThumbsUp > 0)
            {
                await cmdCtx.Channel.SendMessageAsync($"Here is a list of how many and who was present");
                await cmdCtx.Channel.SendMessageAsync($":+1:: {totalThumbsUp}");
                await cmdCtx.Channel.SendMessageAsync(string.Join("\n", allWhoThumbedUpUsernames));
            }
            else
            {
                await cmdCtx.Channel.SendMessageAsync($"None was present, no :+1::s today :slight_frown:");
            }

            foreach (var result in testResult)
            {
                if (result.Emoji == thumbsUpEmoji)
                {
                    foreach (var user in result.Users)
                    {
                        if (!user.IsBot)
                        {
                           allWhoThumbedUp.Add(user);
                        }
                    }
                }
            }

            var teacherDm = new DiscordEmbedBuilder
            {
                Title = "Gathered Presence Check Info",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    IconUrl = cmdCtx.Client.CurrentUser.AvatarUrl,
                    Name = cmdCtx.Client.CurrentUser.Username,
                },
                Color = DiscordColor.Gold,
                Description = $"Here is the gathered info from the presence-check you made in {cmdCtx.Channel.Mention}.\nPresent: {totalThumbsUp}\nAbsent: XX\nTotal students in {cmdCtx.Guild.Name}: XX\nTo see further information and to be able to export the presence-check use this link:\n https://localhost:8000",
                Timestamp = cmdCtx.Message.Timestamp,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail()
                {
                    Url = cmdCtx.Client.CurrentUser.AvatarUrl,
                },
                Url = "https://localhost:8000",
            };

            try
            {

                await cmdCtx.Member.SendMessageAsync(embed: teacherDm);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            // TODO Save all variables needed to be sent into database, Make checks (is the teacher already registered in DB? Then dont create a new teacher just update, and so on)

            // IMPORTANT, schoolClass is and should be temporary, right now we dont do any checks if there is an class already made. Move students from the class in db to new lecture
            // that is created. So we have a full list of students in lecture. Otherwise there will be only those who where present.
            var schoolClass = new List<Class>()
            {
                new Class()
                {
                    Name = cmdCtx.Guild.Name,
                },
            };

            var teacher = new Teacher()
            {
                Name = cmdCtx.Member.Nickname ?? cmdCtx.Member.Username,
                //Classes = schoolClass,
            };

            var students = new List<Student>()
            {
            };

            foreach (var result in allWhoThumbedUp)
            {
                var member = cmdCtx.Guild.Members.Values.FirstOrDefault(x => x.Id == result.Id);
                if (member != null)
                {
                    students.Add(new Student()
                    {
                        Id = result.Id,
                        Name = member.Nickname ?? member.Username,
                        Classes = schoolClass,
                    });
                }
            }
            var presences = new List<Presence>
            {
            };

            var lecture = new Lecture()
            {
                Date = cmdCtx.Message.CreationTimestamp.DateTime,
                Class = schoolClass[0],
                Teacher = teacher,
                Students = students,
            };

            foreach (var student in students)
            {
                presences.Add(new Presence()
                {
                    Attended = true,
                    Student = student,
                    Lecture = lecture,
                });
            }

            // In this scope you can use services, example: var variableName = scope.ServiceProvider.GetRequiredService<WhatEverServiceYouWant>();
            await using (var scope = cmdCtx.Services.CreateAsyncScope())
            {
               
            }
        }
    }
}
