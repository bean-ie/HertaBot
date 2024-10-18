using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Mail;
using System.Windows;
using System.Xml.Linq;
using Discord.WebSocket;
using System.Windows.Forms;
using static HertaBot.Tools;

namespace HertaBot
{
    public class Commands : InteractionModuleBase
    {
        [SlashCommand("ping", "Check if the bot is on")]
        public async Task Ping()
        {
            await RespondAsync("Pong!", ephemeral: true);
        }

        [SlashCommand("autoreply", "Set if the bot will automatically reply to doll messages for you")]
        public async Task AutoreplySet(bool autoreply)
        {
            if (autoreply)
            {
                if (System.IO.File.ReadAllLines(Tools.autoreplyoptinPath).Contains(Context.User.Id.ToString()))
                {
                    await RespondAsync("Autoreply is already turned on for you!", ephemeral: true);
                }
                else
                {
                    using (StreamWriter file = new StreamWriter(Tools.autoreplyoptinPath, true))
                    {
                        await file.WriteLineAsync(Context.User.Id.ToString());
                        file.Close();
                        await RespondAsync("Autoreply is now turned on for you.", ephemeral: true);
                    }
                }
            }
            else
            {
                if (!System.IO.File.ReadAllLines(Tools.autoreplyoptinPath).Contains(Context.User.Id.ToString()))
                {
                    await RespondAsync("Autoreply is already turned off for you!", ephemeral: true);
                }
                else
                {
                    var newLines = System.IO.File.ReadAllLines(Tools.autoreplyoptinPath).Select(line => Regex.Replace(line, Context.User.Id.ToString(), string.Empty));
                    System.IO.File.WriteAllLines(Tools.autoreplyoptinPath, newLines);
                    await RespondAsync("Autoreply is now turned back off for you.", ephemeral: true);
                }
            }
        }

        [SlashCommand("dollregister", "Register a doll")]
        public async Task DollRegister(string name, IAttachment avatar, string brackets)
        {
            if (avatar.ContentType.Split('/')[0] != "image")
            {
                await RespondAsync("Please put in an image!", ephemeral: true);
                return;
            }
            if (!brackets.Contains("text") || Regex.Matches(brackets, "text").Count != 1)
            {
                await RespondAsync("The pattern must have \"text\" included in it *exactly once.*", ephemeral: true);
                return;
            }
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            if (users.ContainsKey(Context.User.Id))
            {
                if (users[Context.User.Id].Any(x => x.name == name))
                {
                    await RespondAsync("You already have a doll with that name!", ephemeral: true);
                    return;
                }
                if (users[Context.User.Id].Any(x => x.pattern == brackets))
                {
                    await RespondAsync("You already have a doll with these brackets!", ephemeral: true);
                    return;
                }
            }
            try
            {
                if (users == null)
                {
                    users = new Dictionary<ulong, List<Tools.Doll>>();
                }
                if (!users.ContainsKey(Context.User.Id))
                {
                    users.Add(Context.User.Id, new List<Tools.Doll>());
                }
                users[Context.User.Id].Add(new Tools.Doll(name, avatar.Url, brackets));
                string json = JsonSerializer.Serialize(users, options: new JsonSerializerOptions()
                {
                    WriteIndented = true
                });
                File.WriteAllText(Tools.dollsPath, json);
                await RespondAsync($"Doll \"{name}\" with brackets \"{brackets}\" registered!\nImage: {avatar.Url}", ephemeral: true);
            }
            catch (Exception e)
            {
                await RespondAsync(e.ToString(), ephemeral: true);
                Console.WriteLine(e);
            }
        }

        public bool EditDoll(string name, Func<Tools.Doll, Tools.Doll> edit)
        {
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            if (users.ContainsKey(Context.User.Id))
            {
                if (users[Context.User.Id].Any(x => x.name == name))
                {
                    try
                    {
                        for (int i = 0; i < users[Context.User.Id].Count; i++)
                        {
                            if (users[Context.User.Id][i].name == name)
                            {
                                Tools.Doll[] newList = users[Context.User.Id].ToArray();
                                newList[i] = edit(newList[i]);
                                users[Context.User.Id] = newList.ToList();
                            }
                        }
                        string json = JsonSerializer.Serialize(users, options: new JsonSerializerOptions()
                        {
                            WriteIndented = true
                        });
                        File.WriteAllText(Tools.dollsPath, json);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    return true;
                }
            }
            return false;
        }

        [SlashCommand("dollremove", "Remove a doll")]
        public async Task DollRemove(string name)
        {
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            if (users.ContainsKey(Context.User.Id))
            {
                if (users[Context.User.Id].Any(x => x.name == name))
                {
                    try
                    {
                        users[Context.User.Id].Remove(users[Context.User.Id].First(x => x.name == name));
                        string json = JsonSerializer.Serialize(users, options: new JsonSerializerOptions()
                        {
                            WriteIndented = true
                        });
                        File.WriteAllText(Tools.dollsPath, json);
                        await RespondAsync($"Doll {name} has been removed!", ephemeral: true);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        await RespondAsync(e.ToString());
                    }
                    return;
                }
            }
            await RespondAsync("You don't have a doll with that name!", ephemeral: true);
        }

        [SlashCommand("dollslist", "Show all your dolls")]
        public async Task DollList(bool showToOthers = false)
        {
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            List<Tools.Doll> userDolls = null;
            if (users.ContainsKey(Context.User.Id))
            {
                userDolls = users[Context.User.Id];
            }
            else
            {
                await RespondAsync("You don't have any dolls!", ephemeral: true);
            }
            EmbedBuilder listEmbed = new EmbedBuilder();
            listEmbed.Title = $"{Context.User.Username}'s dolls";
            Random random = new Random();
            listEmbed.Color = new Color(random.Next(255), random.Next(255), random.Next(255));
            string list = string.Empty;
            if (userDolls != null)
            {
                for (int i = 0; i < userDolls.Count; i++)
                {
                    Tools.Doll doll = userDolls[i];
                    list += $"{i + 1}) **{doll.name}** ({doll.pattern})\n";
                }
                listEmbed.Description = list;
                await RespondAsync(embed: listEmbed.Build(), ephemeral: !showToOthers);
            }
            else
            {
                await RespondAsync("You don't have any dolls!", ephemeral: true);
            }
        }

        [SlashCommand("dolleditname", "Edit the name of an existing doll")]
        public async Task DollEditName(string oldName, string newName)
        {
            if (EditDoll(oldName, x =>
            {
                x.name = newName;
                return x;
            }))
            {
                await RespondAsync($"Doll {oldName} has been renamed to {newName}!", ephemeral: true);
            }
            else
            {
                await RespondAsync("You don't have a doll with that name!", ephemeral: true);
            }
        }

        [SlashCommand("dolleditbrackets", "Edit the brackets used to send a doll message")]
        public async Task DollEditBrackets(string name, string newBrackets)
        {
            if (!newBrackets.Contains("text") || Regex.Matches(newBrackets, "text").Count != 1)
            {
                await RespondAsync("The pattern must have \"text\" included in it *exactly once.*", ephemeral: true);
                return;
            }
            if (EditDoll(name, x =>
            {
                x.pattern = newBrackets;
                return x;
            }))
            {
                await RespondAsync($"Doll {name}'s brackets have been changed to \"{newBrackets}\"", ephemeral: true);
            }
            else
            {
                await RespondAsync("You don't have a doll with that name!", ephemeral: true);
            }
        }

        [SlashCommand("dollavatar", "See or change the avatar of a doll")]
        public async Task DollAvatar(string name, IAttachment newAvatar = null, bool showToOthers = false)
        {
            if (newAvatar == null)
            {
                Tools.Doll doll;
                if (Tools.DetermineDollFromName(name, Context.User.Id, out doll))
                {
                    await RespondAsync(doll.imageUrl, ephemeral: !showToOthers);
                }
                else
                {
                    await RespondAsync("You don't have a doll with that name!", ephemeral: true);
                }
            }
            else
            {
                if (newAvatar.ContentType.Split('/')[0] != "image")
                {
                    await RespondAsync("Please put in an image!", ephemeral: true);
                    return;
                }
                else if (EditDoll(name, x =>
                {
                    x.imageUrl = newAvatar.Url;
                    return x;
                }))
                {
                    await RespondAsync($"Doll {name}'s avatar has been changed to: {newAvatar.Url}", ephemeral: !showToOthers);
                }
                else
                {
                    await RespondAsync("You don't have a doll with that name!", ephemeral: true);
                }
            }
        }

        [SlashCommand("channeldisabletoggle", "Toggles the bot in a certain channel")]
        public async Task ChannelToggle(ITextChannel channel)
        {
            if ((Context.User as SocketGuildUser).GuildPermissions.ManageChannels)
            {
                string disablePath = "..\\channeldisable.txt";
                string[] disableLines = File.ReadAllLines(disablePath);
                if (disableLines.Contains(channel.Id.ToString()))
                {
                    for (int i = 0; i < disableLines.Length; i++)
                    {
                        if (disableLines[i] == channel.Id.ToString())
                        {
                            disableLines[i] = string.Empty;
                        }
                    }
                    File.WriteAllLines(disablePath, disableLines);
                    await RespondAsync($"Bot re-enabled in {channel.Mention}", ephemeral: true);
                }
                else
                {
                    using (StreamWriter writer = new StreamWriter(disablePath))
                    {
                        writer.WriteLine(channel.Id.ToString());
                        writer.Close();
                    }
                    await RespondAsync($"Bot disabled in {channel.Mention}", ephemeral: true);
                }
            }
            else
            {
                await RespondAsync("You don't have permission to do that.", ephemeral: true);
            }
        }

        [SlashCommand("autoproxytoggle", "Automatically send messages as a doll")]
        public async Task AutoProxyToggle(string name, bool toggle, ITextChannel channel = null)
        {
            Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>> autoProxies = null; 
            if (File.ReadAllText(Tools.autoProxiesPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.autoProxiesPath);
                autoProxies = JsonSerializer.Deserialize<Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>>>(jsonText);
            }

            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }

            List<Tools.Doll> userDolls = null;
            if (users.ContainsKey(Context.User.Id))
            {
                userDolls = users[Context.User.Id];
            }

            if (!userDolls.Any(x => x.name == name))
            {
                await RespondAsync("You don't have a doll with that name!", ephemeral: true);
                return;
            }

            if (!toggle)
            {
                if (autoProxies == null)
                {
                    await RespondAsync("You don't have any active autoproxies!", ephemeral: true);
                    return;
                }
                else if (!autoProxies.ContainsKey(Context.Guild.Id))
                {
                    await RespondAsync("You don't have any active autoproxies!", ephemeral: true);
                    return;
                } 
                else if (!autoProxies[Context.Guild.Id].ContainsKey(Context.User.Id))
                {
                    await RespondAsync("You don't have any active autoproxies!", ephemeral: true);
                    return;
                }
            }

            if (autoProxies == null)
            {
                autoProxies = new Dictionary<ulong, Dictionary<ulong, List<AutoProxy>>>();
            }

            if (!autoProxies.ContainsKey(Context.Guild.Id))
            {
                autoProxies.Add(Context.Guild.Id, new Dictionary<ulong, List<Tools.AutoProxy>>());
            }

            if (!autoProxies[Context.Guild.Id].ContainsKey(Context.User.Id))
            {
                autoProxies[Context.Guild.Id].Add(Context.User.Id, new List<Tools.AutoProxy>());
            }

            if (!toggle)
            {
                if (!autoProxies[Context.Guild.Id][Context.User.Id].Any(x => x.dollName == name))
                {
                    if (channel == null)
                        await RespondAsync($"You don't have an autoproxy for {name} in the server!", ephemeral: true);
                    else
                        await RespondAsync($"You don't have an autoproxy for {name} in {channel.Mention}!", ephemeral: true);
                }
            }

            for (int i = 0; i < autoProxies[Context.Guild.Id][Context.User.Id].Count; i++)
            {
                Tools.AutoProxy proxy = autoProxies[Context.Guild.Id][Context.User.Id][i];
                if (proxy.channelId == channel?.Id)
                {
                    if (toggle)
                    {
                        await RespondAsync("You already have an active autoproxy in this channel!", ephemeral: true);
                        return;
                    }
                    else if (proxy.dollName == name)
                    {
                        autoProxies[Context.Guild.Id][Context.User.Id].RemoveAt(i);
                        await RespondAsync($"Autoproxy for {name} in {channel.Mention} disabled.", ephemeral: true);
                    }
                }
                else if (proxy.channelId == 0 && channel == null)
                {
                    if (toggle)
                    {
                        await RespondAsync("You already have an active autoproxy in this server!", ephemeral: true);
                        return;
                    }
                    else if (proxy.dollName == name)
                    {
                        autoProxies[Context.Guild.Id][Context.User.Id].RemoveAt(i);
                        await RespondAsync($"Autoproxy for {name} in this server disabled.", ephemeral: true);
                    }
                }
            }

            if (toggle)
            {
                Tools.AutoProxy newProxy = new Tools.AutoProxy(name, 0);
                if (channel != null)
                    newProxy.channelId = channel.Id;
                autoProxies[Context.Guild.Id][Context.User.Id].Add(newProxy);
                if (channel == null)
                    await RespondAsync($"Autoproxy for {name} in this server enabled.", ephemeral: true);
                else
                    await RespondAsync($"Autoproxy for {name} in {channel.Mention} enabled.", ephemeral: true);
            }

            string json = JsonSerializer.Serialize(autoProxies, options: new JsonSerializerOptions()
            {
                WriteIndented = true
            });
            File.WriteAllText(Tools.autoProxiesPath, json);
        }

        [SlashCommand("autoproxylist", "See all your active autoproxies")]
        public async Task AutoProxyList()
        {
            Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>> autoProxies = null;
            if (File.ReadAllText(Tools.autoProxiesPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.autoProxiesPath);
                autoProxies = JsonSerializer.Deserialize<Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>>>(jsonText);
            }
            if (autoProxies != null)
            {
                if (autoProxies.ContainsKey(Context.Guild.Id))
                {
                    if (autoProxies[Context.Guild.Id].ContainsKey(Context.User.Id))
                    {
                        if (autoProxies[Context.Guild.Id][Context.User.Id].Count > 0)
                        {
                            string response = string.Empty;
                            foreach (var autoProxy in autoProxies[Context.Guild.Id][Context.User.Id])
                            {
                                if (autoProxy.channelId == 0)
                                {
                                    response += $"{autoProxy.dollName} server-wide\n";
                                }
                                else
                                {
                                    response += $"{autoProxy.dollName} in <#{autoProxy.channelId}>\n";
                                }
                            }
                            await RespondAsync(response, ephemeral: true);
                            return;
                        }
                    }
                }
            }
            await RespondAsync("You have no active autoproxies in this server!", ephemeral: true);
        }

        [SlashCommand("autoproxyclear", "Removes all your autoproxies")]
        public async Task AutoProxyClear()
        {
            Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>> autoProxies = null;
            if (File.ReadAllText(Tools.autoProxiesPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.autoProxiesPath);
                autoProxies = JsonSerializer.Deserialize<Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>>>(jsonText);
            }

            if (autoProxies != null)
            {
                if (autoProxies.ContainsKey(Context.Guild.Id))
                {
                    if (autoProxies[Context.Guild.Id].ContainsKey(Context.User.Id))
                    {
                        autoProxies[Context.Guild.Id][Context.User.Id] = new List<AutoProxy>();
                        string json = JsonSerializer.Serialize(autoProxies, options: new JsonSerializerOptions()
                        {
                            WriteIndented = true
                        });
                        File.WriteAllText(Tools.autoProxiesPath, json);
                        await RespondAsync("Cleared all your autoproxies.", ephemeral: true);
                        return;
                    }
                }
            }
            await RespondAsync("You don't have any autoproxies in this server!", ephemeral: true);
        }

        [SlashCommand("help", "Shows a small guide on the bot!")]
        public async Task Help()
        {
            EmbedBuilder helpEmbed = new EmbedBuilder();
            helpEmbed.Title = "Welcome to Herta!";
            helpEmbed.Color = new Color(125, 25, 168);
            helpEmbed.AddField("***Creating, editing and deleting dolls***", "Use **/dollregister** to create a new doll.\nUse **/dollremove** to remove an existing doll.\n" +
                "Use **/dolleditname**, **/dolleditbrackets** and **/dollavatar** to edit a specific part of your doll!\nYou can also use **/dollavatar** to display a doll's current avatar.\n" +
                "Use **/dollslist** to list all your current dolls!");
            helpEmbed.AddField("***Autoreply***", "If a non-doll replies to a doll message, Herta can automatically convert the reply to a doll-like message, allowing the non-doll to ping the original message's sender.\n" +
                "Use **/autoreply** to set it to either True or False!");
            helpEmbed.AddField("***Autoproxy***", "You can send messages as a specific doll automatically by setting up an autoproxy.\n" +
                "Use **/autoproxytoggle** to toggle a certain's doll autoproxy on or off. Leave the *channel* field empty to make the autoproxy server-wide!\n" +
                "Use **/autoproxyclear** to remove all of your autoproxies on this server.\n" +
                "Use **/autoproxylist** to list all your current autoproxies on this server.");
            helpEmbed.AddField("***Doll messages***", "To delete a doll message, simply react to it with ❌.\n" +
                "To edit a doll message, reply to it with **doll!edit** and input the new message.\n" +
                "Just using **doll!edit** without a reply will edit your last sent doll message!\n" +
                "To see who sent a specific doll message, simply react to it with ❓.");
            await RespondAsync(embed: helpEmbed.Build(), ephemeral: true);
        }
    }
}
