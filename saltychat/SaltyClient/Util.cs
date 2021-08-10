using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;

namespace SaltyClient
{
    public static class Util
    {
        public static string ToJson(object obj)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(obj);
        }

        public static bool TryParseJson<T>(string json, out T result)
        {
            result = default;

            try
            {
                result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json);
            }
            catch { }

            return result is object;
        }

        public static void SendChatMessage(string sender, string message)
        {
            System.Drawing.Color color = System.Drawing.Color.FromArgb(255, 255, 255);

            BaseScript.TriggerEvent("chat:addMessage", new
            {
                color = new[] { color.R, color.G, color.B },
                args = new[] { sender, message }
            });
        }
    }
}
