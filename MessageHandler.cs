using Discord;
using Discord.Rest;
using Discord.Utils;
using Discord.Webhook;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using static HertaBot.Tools;

namespace HertaBot
{
    internal class MessageHandler
    {
        //Dictionary<ulong, ulong> dollMessageSenderDict = new Dictionary<ulong, ulong>();
        struct DollMessage
        {
            public string name;
            public ulong messageId;
            public ulong senderId;
            public IMessage replyMessage;
            public DateTimeOffset createdAt;
            public bool isRealPerson;

            public DollMessage(string name, ulong messageId, ulong senderId, DateTimeOffset createdAt, IMessage replyMessage = null, bool isRealPerson = false)
            {
                this.name = name;
                this.messageId = messageId;
                this.senderId = senderId;
                this.createdAt = createdAt;
                this.replyMessage = replyMessage;
                this.isRealPerson = isRealPerson;
            }
        }
        DollMessage[] dollMessages = new DollMessage[100];
        int currentDollMessageCounter = 0;

        Dictionary<ulong, IEnumerable<RestWebhook>> fetchedWebhooksCache = new Dictionary<ulong, IEnumerable<RestWebhook>>();
        public IWebhook[] dollWebhooks = new IWebhook[100];
        DiscordWebhookClient[] dollWebhookClients = new DiscordWebhookClient[100];
        Random random = new Random();

        private bool IsDollMessage(ulong messageId, out DollMessage outDollMessage)
        {
            outDollMessage = new DollMessage();
            foreach (DollMessage dollMessage in dollMessages)
            {
                if (dollMessage.messageId == messageId)
                {
                    outDollMessage = dollMessage;
                    return true;
                }
            }
            return false;
        }

        public async Task<int> CreateWebhookAsync(SocketMessage message)
        {
            ulong guildId = (message.Channel as SocketGuildChannel).Guild.Id;
            IWebhook newDollWebhook = null;
            if (!fetchedWebhooksCache.ContainsKey(guildId))
            {
                fetchedWebhooksCache.Add(guildId, (await (message.Channel as SocketGuildChannel).Guild.GetWebhooksAsync()).Where(x => x.Creator.Id == 1289724721922965589));
            }
            SocketGuildChannel actualChannel = message.Channel as SocketGuildChannel;
            if (message.Channel.GetChannelType() == ChannelType.PublicThread || message.Channel.GetChannelType() == ChannelType.PrivateThread || message.Channel.GetChannelType() == ChannelType.NewsThread)
            {
                actualChannel = (message.Channel as SocketThreadChannel).ParentChannel;
            }
            if (dollWebhooks.Any(x => x != null))
            {
                if (!dollWebhooks.Any(x => x?.ChannelId == actualChannel.Id))
                {
                    if (fetchedWebhooksCache[guildId].Any(x => x?.ChannelId == actualChannel.Id))
                    {
                        newDollWebhook = fetchedWebhooksCache[guildId].First(x => x.ChannelId == actualChannel.Id);
                    }
                    else
                    {
                        newDollWebhook = await (actualChannel as IIntegrationChannel).CreateWebhookAsync("Herta #" + message.Channel.Name);
                    }
                }
            }
            else if (fetchedWebhooksCache[guildId].Any(x => x.ChannelId == actualChannel.Id))
            {
                newDollWebhook = fetchedWebhooksCache[guildId].First(x => x.ChannelId == actualChannel.Id);
            }
            else
            {
                newDollWebhook = await (actualChannel as IIntegrationChannel).CreateWebhookAsync("Herta #" + message.Channel.Name);
            }

            if (newDollWebhook != null)
            {
                if (!dollWebhooks.Any(x => x == null))
                {
                    dollWebhooks[0] = newDollWebhook;
                }
                else
                {
                    for (int i = 0; i < dollWebhooks.Length; i++)
                    {
                        if (dollWebhooks[i] == null)
                        {
                            dollWebhooks[i] = newDollWebhook;
                            dollWebhookClients[i] = new DiscordWebhookClient(dollWebhooks[i]);
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < dollWebhooks.Length; i++)
            {
                if (dollWebhooks[i] != null)
                {
                    if (dollWebhooks[i].ChannelId == actualChannel.Id)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        public async Task CreateAndSendDollMessageFromMessage(SocketMessage message, int dollWebhookIndex, Tools.Doll doll, bool isRealPerson = false)
        {

        }

        public async Task CreateAndSendDollMessage(string messageText, SocketMessage originalMessage, int dollWebhookIndex, Tools.Doll doll, bool isRealPerson = false, bool useReplyAndAttachments = true)
        {
            Console.WriteLine($"Sending message in #{originalMessage.Channel.Name} by {originalMessage.Author.Username}");
            string[] split = Regex.Escape(doll.pattern).Split(new string[] { "text" }, StringSplitOptions.None);
            string finalResult = messageText;
            foreach (string part in split)
            {
                finalResult = Regex.Replace(finalResult, part, string.Empty);
            }
            finalResult = finalResult.Trim();

            IMessage replyMessage = null;
            if (originalMessage.Reference != null && useReplyAndAttachments)
            {
                if (originalMessage.Reference.ReferenceType.Value == MessageReferenceType.Default)
                {
                    if (originalMessage.Reference.MessageId.IsSpecified)
                    {
                        replyMessage = await originalMessage.Channel.GetMessageAsync(originalMessage.Reference.MessageId.Value);
                        string replyMessageContent = replyMessage.Content;
                        string senderMention = replyMessage.Author.Mention;
                        if (replyMessage.Author.IsWebhook)
                            senderMention = $"@{replyMessage.Author.Username}";
                        foreach (var dollMessage in dollMessages)
                        {
                            if (dollMessage.messageId == replyMessage.Id)
                            {
                                if (!dollMessage.isRealPerson)
                                    senderMention = $"@{dollMessage.name} (<@{dollMessage.senderId}>)";
                                else
                                    senderMention = $"<@{dollMessage.senderId}>";
                                if (dollMessage.replyMessage != null)
                                {
                                    string[] messageContentLines = replyMessage.Content.Split('\n');
                                    messageContentLines[0] = string.Empty;
                                    replyMessageContent = string.Empty;
                                    foreach (var line in messageContentLines)
                                    {
                                        replyMessageContent += line;
                                    }
                                }
                                break;
                            }
                        }
                        try
                        {
                            foreach (var userId in replyMessage.MentionedUserIds)
                            {
                                if (replyMessageContent.Contains($"<@{userId}>"))
                                {
                                    var user = await originalMessage.Channel.GetUserAsync(userId);
                                    if (user != null)
                                        replyMessageContent = replyMessageContent.Replace($"<@{userId}>", $"@{user.Username}");
                                }
                            }   
                            foreach (var roleId in replyMessage.MentionedRoleIds)
                            {
                                if (replyMessageContent.Contains($"<@&{roleId}>"))
                                {
                                    var role = (originalMessage.Channel as SocketGuildChannel).Guild.GetRole(roleId);
                                    if (role != null)
                                        replyMessageContent = replyMessageContent.Replace($"<@&{role.Id}>", $"@{role.Name}");
                                }
                            }
                            foreach (var channelId in replyMessage.MentionedChannelIds)
                            {
                                if (replyMessageContent.Contains($"<#{channelId}>"))
                                {
                                    var channel = (originalMessage.Channel as SocketGuildChannel).Guild.GetChannel(channelId);
                                    if (channel != null)
                                        replyMessageContent = replyMessageContent.Replace($"<#{channel.Id}>", $"#{channel.Name}");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        replyMessageContent = Regex.Replace(replyMessageContent, "(www|http:|https:)+[^\\s]+[\\w]", "{Link}");
                        string replyMessageCutoff = string.Empty;
                        int replyMessageCutoffAmount = 25;
                        
                        if (Regex.IsMatch(replyMessageContent, "<:.*:\\d*>"))
                        {
                            foreach (Match match in Regex.Matches(replyMessageContent, "<:.*:\\d*>"))
                            {
                                if (match.Index < replyMessageCutoffAmount)
                                {
                                    replyMessageCutoffAmount += match.Length;
                                }
                            }
                        }
                        for (int i = 0; i < replyMessageCutoffAmount; i++)
                        {
                            if (i >= replyMessageContent.Length) break;
                            replyMessageCutoff += replyMessageContent[i];
                            if (i >= replyMessageCutoffAmount - 1) replyMessageCutoff += "...";
                        }
                        replyMessageCutoff = replyMessageCutoff.Replace("\n", "  ");
                        if (replyMessage.Attachments.Count > 0)
                        {
                            if (replyMessageCutoff != string.Empty)
                                replyMessageCutoff += " <:attachment:1293210676092862534>";
                            else
                                replyMessageCutoff += "*Click to see attachment* <:attachment:1293210676092862534>";
                        }
                        finalResult = $"-# {senderMention} [{replyMessageCutoff}]({replyMessage.GetJumpUrl()})\n" + finalResult;
                    }
                }
            }

            List<FileAttachment> attachments = new List<FileAttachment>();
            if (useReplyAndAttachments)
            {
                foreach (var attachment in originalMessage.Attachments)
                {
                    attachments.Add(new FileAttachment(await Tools.GetStreamFromUrlAsync(attachment.Url), attachment.Filename));
                }
            }

            try
            {
                ulong authorId = originalMessage.Author.Id;
                ulong dollMessageId;
                if (originalMessage.Channel.GetChannelType() == ChannelType.PublicThread || originalMessage.Channel.GetChannelType() == ChannelType.PrivateThread || originalMessage.Channel.GetChannelType() == ChannelType.NewsThread)
                    dollMessageId = await dollWebhookClients[dollWebhookIndex].SendFilesAsync(attachments, finalResult, username: doll.name, avatarUrl: doll.imageUrl, threadId: (originalMessage.Channel as SocketThreadChannel).Id);
                else
                    dollMessageId = await dollWebhookClients[dollWebhookIndex].SendFilesAsync(attachments, finalResult, username: doll.name, avatarUrl: doll.imageUrl);
                Console.WriteLine($"Message Id = {dollMessageId}, Author Id = {authorId}");
                dollMessages[currentDollMessageCounter] = new DollMessage(doll.name, dollMessageId, authorId, (await originalMessage.Channel.GetMessageAsync(dollMessageId)).CreatedAt, replyMessage, isRealPerson);
                currentDollMessageCounter = (currentDollMessageCounter + 1) % dollMessages.Length;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public bool DetermineDollFromMessage(SocketMessage message, out Tools.Doll outDoll)
        {
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            if (users.ContainsKey(message.Author.Id))
            {
                foreach (Tools.Doll doll in users[message.Author.Id])
                {
                    if (Regex.IsMatch(message.Content, $"^{Regex.Escape(doll.pattern).Replace("text", ".*")}"))
                    {
                        outDoll = doll;
                        return true;
                    }
                }
            }
            outDoll = new Tools.Doll();
            return false;
        }

        public bool DetermineDollsFromText(string message, ulong userId, out Tools.Doll[] dolls, out string[] texts)
        {
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            string[] lines = message.Split('\n');
            bool isDollMessage = false;
            List<Tools.Doll> dollList = new List<Tools.Doll>();
            List<string> textsList = new List<string>();
            if (users.ContainsKey(userId))
            {
                if (users[userId].Any(x => Regex.IsMatch(message, $"^{Regex.Escape(x.pattern).Replace("text", ".*")}")))
                {
                    isDollMessage = true;
                    dollList.Add(users[userId].First(x => Regex.IsMatch(message, $"^{Regex.Escape(x.pattern).Replace("text", ".*")}")));
                    string text = lines[0];
                    string[] split = Regex.Escape(dollList[0].pattern).Split(new string[] { "text" }, StringSplitOptions.None);
                    foreach (string part in split)
                    {
                        text = Regex.Replace(text, part, string.Empty);
                    }
                    textsList.Add(text);
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (users[userId].Any(x => Regex.IsMatch(lines[i], $"^{Regex.Escape(x.pattern).Replace("text", ".*")}")) && dollList.Count < 3)
                        {
                            dollList.Add(users[userId].First(x => Regex.IsMatch(lines[i], $"^{Regex.Escape(x.pattern).Replace("text", ".*")}")));
                            string textloop = lines[i];
                            string[] splitloop = Regex.Escape(dollList[0].pattern).Split(new string[] { "text" }, StringSplitOptions.None);
                            foreach (string part in splitloop)
                            {
                                textloop = Regex.Replace(textloop, part, string.Empty);
                            }
                            textsList.Add(textloop);
                        }
                        else
                        {
                            textsList[dollList.Count - 1] += "\n" + lines[i];
                        }
                    }
                }
            }
            dolls = dollList.ToArray();
            texts = textsList.ToArray();
            return isDollMessage;
        }

        public DateTimeOffset GetCooldown(SocketMessage message)
        {
            if (!Tools.cooldowns.ContainsKey(message.Channel.Id))
            {
                Tools.cooldowns.Add(message.Channel.Id, new Dictionary<ulong, DateTimeOffset>());
            }
            if (!Tools.cooldowns[message.Channel.Id].ContainsKey(message.Author.Id))
            {
                Tools.cooldowns[message.Channel.Id].Add(message.Author.Id, DateTimeOffset.MinValue);
            }
            return Tools.cooldowns[message.Channel.Id][message.Author.Id];
        }

        public Task Handler(SocketMessage message)
        {
            _ =
            Task.Run(async () =>
            {
                if (message.Author.IsBot) return;
                if (File.ReadAllLines("C:\\Users\\kuzzz\\source\\repos\\HertaBot\\HertaBot\\channeldisable.txt").Contains(message.Channel.Id.ToString())) return;
                /*
                string channelName = message.Channel.Name;
                string name = "Herta #" + random.Next(1000) + " in #" + channelName;
                Tools.Doll placeholderDoll = new Tools.Doll(name, "https://pbs.twimg.com/media/Fun5LkVagAUYIbP?format=png&name=small", "hertabotpattern: text");
                */

                Tools.Doll[] usedDolls = null;
                string[] texts = null;

                if (!DetermineDollsFromText(message.Content, message.Author.Id, out usedDolls, out texts))
                {
                    Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>> autoProxies = null;
                    if (File.ReadAllText(Tools.autoProxiesPath) != string.Empty)
                    {
                        string jsonText = File.ReadAllText(Tools.autoProxiesPath);
                        autoProxies = JsonSerializer.Deserialize<Dictionary<ulong, Dictionary<ulong, List<Tools.AutoProxy>>>>(jsonText);
                    }
                    ulong guildId = (message.Channel as SocketGuildChannel).Guild.Id;
                    if (autoProxies != null)
                    {
                        if (autoProxies.ContainsKey(guildId))
                        {
                            if (autoProxies[guildId].ContainsKey(message.Author.Id))
                            {
                                Tools.Doll doll;
                                if (autoProxies[guildId][message.Author.Id].Any(x => x.channelId == message.Channel.Id))
                                {
                                    if (Tools.DetermineDollFromName(autoProxies[guildId][message.Author.Id].First(x => x.channelId == message.Channel.Id).dollName, message.Author.Id, out doll))
                                    {
                                        usedDolls = new Tools.Doll[1] { doll };
                                        texts = new string[1] { message.Content };
                                    }
                                }
                                else if (autoProxies[guildId][message.Author.Id].Any(x => x.channelId == 0))
                                {
                                    if (Tools.DetermineDollFromName(autoProxies[guildId][message.Author.Id].First(x => x.channelId == 0).dollName, message.Author.Id, out doll))
                                    {
                                        usedDolls = new Tools.Doll[1] { doll };
                                        texts = new string[1] { message.Content };
                                    }
                                }
                            }
                        }
                    }
                }

                if (message.Content.StartsWith("doll!edit "))
                {
                    DollMessage dollMsg = new DollMessage();
                    IMessage channelMessage = null;
                    string editedMsg = Regex.Replace(message.Content, "^doll!edit ", string.Empty);
                    IMessageChannel channel = message.Channel;
                    SocketThreadChannel thread = message.Channel as SocketThreadChannel;
                    if (channel.GetChannelType() == ChannelType.PublicThread || channel.GetChannelType() == ChannelType.PrivateThread || channel.GetChannelType() == ChannelType.NewsThread)
                    {
                        channel = (message.Channel as SocketThreadChannel).ParentChannel as IMessageChannel;
                    }
                    if (message.Reference != null)
                    {
                        if (message.Reference.MessageId.IsSpecified)
                        {
                            if (IsDollMessage(message.Reference.MessageId.Value, out dollMsg))
                            {
                                if (thread != null)
                                    channelMessage = await thread.GetMessageAsync(dollMsg.messageId);
                                else
                                    channelMessage = await channel.GetMessageAsync(dollMsg.messageId);
                            }
                        }
                    }
                    else
                    {
                        if (dollMessages.Any(x => x.senderId == message.Author.Id))
                        {
                            dollMsg = dollMessages.Where(x => x.senderId == message.Author.Id).ToList().OrderBy(x => x.createdAt).ToList().LastOrDefault();
                            if (thread != null)
                                channelMessage = await thread.GetMessageAsync(dollMsg.messageId);
                            else
                                channelMessage = await channel.GetMessageAsync(dollMsg.messageId);
                        }
                    }
                    if (dollMsg.senderId == message.Author.Id)
                    {
                        for (int i = 0; i < dollWebhooks.Length; i++)
                        {
                            if (dollWebhooks[i]?.ChannelId == channel.Id)
                            {
                                string newMessageContent = channelMessage.Content;
                                if (dollMsg.replyMessage != null)
                                    newMessageContent = newMessageContent.Split('\n')[0] + "\n" + editedMsg;
                                else
                                    newMessageContent = editedMsg;
                                try
                                {
                                    await dollWebhookClients[i].ModifyMessageAsync(dollMsg.messageId, x => x.Content = newMessageContent, threadId: thread?.Id);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                            }
                        }
                    }
                    await message.Channel.DeleteMessageAsync(message.Id);
                }
                else if (usedDolls.Length > 0)
                {
                    //if (GetCooldown(message).AddSeconds(10) > DateTimeOffset.UtcNow)
                    //{
                    //    await message.Channel.DeleteMessageAsync(message.Id);
                    //}
                    //else
                    //{
                    //Tools.cooldowns[message.Channel.Id][message.Author.Id] = DateTimeOffset.UtcNow;
                    try
                    {
                        int currentChannelDollWebhookIndex = await CreateWebhookAsync(message);
                        if (currentChannelDollWebhookIndex < 0)
                        {
                            return;
                        }

                        for (int i = 0; i < usedDolls.Length; i++)
                        {
                            await CreateAndSendDollMessage(texts[i], message, currentChannelDollWebhookIndex, usedDolls[i], useReplyAndAttachments: i == 0);
                        }
                        await message.Channel.DeleteMessageAsync(message.Id);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    //}
                }
                else if (message.Reference != null)
                {
                    if (System.IO.File.ReadAllLines(Tools.autoreplyoptinPath).Contains(message.Author.Id.ToString()))
                    {
                        //if (GetCooldown(message).AddSeconds(10) < DateTimeOffset.UtcNow)
                        //{
                        //Tools.cooldowns[message.Channel.Id][message.Author.Id] = DateTimeOffset.UtcNow;
                        IMessage replyMessage = null;
                        if (message.Reference.ReferenceType.Value == MessageReferenceType.Default)
                        {
                            if (message.Reference.MessageId.IsSpecified)
                            {
                                replyMessage = await message.Channel.GetMessageAsync(message.Reference.MessageId.Value);
                                if (dollMessages.Any(x => x.messageId == replyMessage.Id))
                                {
                                    var dollMessage = dollMessages.First(x => x.messageId == replyMessage.Id);
                                    int currentChannelDollWebhookIndex = await CreateWebhookAsync(message);
                                    if (currentChannelDollWebhookIndex < 0) return;

                                    SocketGuildUser user = message.Author as SocketGuildUser;
                                    Tools.Doll personDoll = new Tools.Doll(user.DisplayName, user.GetDisplayAvatarUrl());

                                    await CreateAndSendDollMessage(message.Content, message, currentChannelDollWebhookIndex, personDoll, true);
                                    await message.Channel.DeleteMessageAsync(message.Id);
                                }
                            }
                        }
                        //}
                    }
                }
            });
            return Task.CompletedTask;
        }

        public async Task EditHandler(Cacheable<IMessage, ulong> cacheable, SocketMessage message, ISocketMessageChannel channel)
        {
            await Handler(message);
        }
        
        public async Task ReactHandler(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> originChannel, SocketReaction reaction)
        {
            var message = await cachedMessage.GetOrDownloadAsync();
            var channel = await originChannel.GetOrDownloadAsync();
            if (reaction.Emote.Name == "❓")
            {
                DollMessage dollMessage = new DollMessage();
                if (IsDollMessage(message.Id, out dollMessage))
                {
                    if (dollMessage.senderId == 0) return;
                    var originalSender = await channel.GetUserAsync(dollMessage.senderId);
                    var dmChannel = await reaction.User.GetValueOrDefault().CreateDMChannelAsync();
                    await dmChannel.SendMessageAsync($"{message.GetJumpUrl()}\nOriginally sent by <@{originalSender.Id}>\ntag: {originalSender.Username} id: {originalSender.Id}");
                    await message.RemoveReactionAsync(reaction.Emote, reaction.User.GetValueOrDefault());
                }
            }
            if (reaction.Emote.Name == "❌")
            {
                DollMessage dollMessage = new DollMessage();
                if (message == null) return;
                if (IsDollMessage(message.Id, out dollMessage))
                {
                    if (dollMessage.senderId == 0) return;
                    if (dollMessage.senderId == reaction.UserId)
                    {
                        await channel.DeleteMessageAsync(message.Id);
                    }
                }
            }
        }   
    }
}
