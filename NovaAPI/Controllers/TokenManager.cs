using System;
using System.Collections.Generic;
using System.Timers;
using NovaAPI.DataTypes;
using NovaAPI.Util;
using Timer = System.Threading.Timer;

namespace NovaAPI.Controllers
{
    public static class TokenManager
    {
        private static Dictionary<string, Token> Tokens = new Dictionary<string, Token>();

        private static Timer cleanup = new Timer((state =>
        {
            List<string> expiredTokens = new List<string>();
            foreach (KeyValuePair<string, Token> token in Tokens)
            {
                if (token.Value.CleanUp)
                {
                    expiredTokens.Add(token.Key);
                    continue;
                }
                else
                {
                    if ((DateTime.Now - token.Value.Created).TotalMinutes <= 10)
                    {
                        expiredTokens.Add(token.Key);
                    }
                }
            }

            foreach (string token in expiredTokens)
            {
                StorageUtil.RemoveSelectChannelContent(Tokens[token].channel_uuid, Tokens[token].ContentIds);
                Tokens.Remove(token);
            }
        }), null, 10000, 0);

        public static string GenerateToken(string user_uuid, int uses, string channel_uuid)
        {
            if (uses > 10) return "";
            string token = EncryptionUtils.Get256HashString(user_uuid + DateTime.Now + uses);
            Tokens.Add(token, new Token(uses, channel_uuid));
            return token;
        }

        public static bool UseToken(string token, string channel_uuid)
        {
            if (!ValidToken(token, channel_uuid)) return false;
            if (Tokens[token].Uses > 0)
            {
                Tokens[token].Uses--;
                return true;
            }
            else
            {
                InvalidateToken(token);
                return false;
            }
        }

        public static void AddID(string token, string id)
        {
            if (Tokens.ContainsKey(token)) Tokens[token].ContentIds.Add(id);
        }

        public static bool ValidToken(string token, string channel_uuid)
        {
            if (Tokens.ContainsKey(token) && (DateTime.Now - Tokens[token].Created).TotalMinutes <= 10 && Tokens[token].channel_uuid == channel_uuid) return true;
            return false;
        }
        
        public static List<string> GetIDs(string token)
        {
            if (Tokens.ContainsKey(token)) return Tokens[token].ContentIds;
            return new List<string>();
        }

        public static void InvalidateToken(string token)
        {
            Tokens[token].CleanUp = true;
        }
    }
}