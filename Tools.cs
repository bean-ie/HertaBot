using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace HertaBot
{
    public static class Tools
    {
        public static string autoreplyoptinPath = "..\\autoreplyoptin.txt";
        public static string dollsPath = "..\\dolls.json";
        public static string autoProxiesPath = "..\\autoproxy.json";
        public static async Task<Stream> GetStreamFromUrlAsync(string url)
        {
            byte[] imageData = null;
            using (var wc = new System.Net.WebClient())
            {
                imageData = await wc.DownloadDataTaskAsync(new Uri(url));
            }

            return new MemoryStream(imageData);
        }

        public struct Doll
        {
            public string name { get; set; }
            public string imageUrl { get; set; }
            public string pattern { get; set; }

            public Doll(string name, string imageUrl, string pattern = "")
            {
                this.name = name;
                this.imageUrl = imageUrl;
                this.pattern = pattern;
            }
        }

        public struct AutoProxy
        {
            public string dollName { get; set; }
            public ulong channelId { get; set; }

            public AutoProxy(string dollName, ulong channelId)
            {
                this.dollName = dollName;
                this.channelId = channelId;
            }
        }

        public static Dictionary<ulong, Dictionary<ulong, DateTimeOffset>> cooldowns = new Dictionary<ulong, Dictionary<ulong, DateTimeOffset>>();

        public static bool TryGetDollFromName(ulong userId, string name, out Doll doll)
        {
            doll = new Doll();
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            if (users.ContainsKey(userId))
            {
                if (users[userId].Any(x => x.name == name))
                {
                    doll = users[userId].FirstOrDefault(x => x.name == name);
                    return true;
                }
            }
            return false;
        }

        public static bool DetermineDollFromName(string name, ulong userId, out Tools.Doll outDoll)
        {
            Dictionary<ulong, List<Tools.Doll>> users = null;
            if (File.ReadAllText(Tools.dollsPath) != string.Empty)
            {
                string jsonText = File.ReadAllText(Tools.dollsPath);
                users = JsonSerializer.Deserialize<Dictionary<ulong, List<Tools.Doll>>>(jsonText);
            }
            if (users.ContainsKey(userId))
            {
                foreach (Tools.Doll doll in users[userId])
                {
                    if (doll.name == name)
                    {
                        outDoll = doll;
                        return true;
                    }
                }
            }
            outDoll = new Tools.Doll();
            return false;
        }
    }
}
