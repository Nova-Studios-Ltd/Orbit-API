using System;
using System.Collections.Generic;
using NovaAPI.DataTypes;
using NovaAPI.Util;

namespace NovaAPI.Controllers
{
    public static class TokenManager
    {
        private static Dictionary<string, Token> Tokens = new Dictionary<string, Token>();

        public static string GenerateToken(string user_uuid, int uses)
        {
            if (uses > 10) return "";
            string token = EncryptionUtils.Get256HashString(user_uuid + DateTime.Now + uses);
            Tokens.Add(token, new Token(uses));
            return token;
        }

        public static bool UseToken(string token)
        {
            if (!ValidToken(token)) return false;
            if (Tokens[token].Uses > 0)
            {
                Tokens[token].Uses--;
                return true;
            }
            else
            {
                Tokens.Remove(token);
                return false;
            }
        }

        public static void AddID(string token, string id)
        {
            if (Tokens.ContainsKey(token)) Tokens[token].ContentIds.Add(id);
        }

        public static bool ValidToken(string token)
        {
            if (Tokens.ContainsKey(token) && (DateTime.Now - Tokens[token].Created).TotalMinutes <= 10) return true;
            return false;
        }
        
        public static List<string> GetIDs(string token)
        {
            if (Tokens.ContainsKey(token)) return Tokens[token].ContentIds;
            return new List<string>();
        }

        public static void InvalidateToken(string token)
        {
            Tokens.Remove(token);
        }
    }
}