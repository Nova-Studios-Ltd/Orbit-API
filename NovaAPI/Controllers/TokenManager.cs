using System;
using System.Collections.Generic;
using NovaAPI.DataTypes;
using NovaAPI.Util;

namespace NovaAPI.Controllers
{
    public class TokenManager
    {
        private static Dictionary<string, Token> Tokens = new Dictionary<string, Token>();

        public string GenerateToken(string user_uuid, int uses)
        {
            if (uses > 10) return "";
            string token = EncryptionUtils.Get256HashString(user_uuid + DateTime.Now + uses);
            Tokens.Add(token, new Token(uses));
            return token;
        }

        public bool UseToken(string token)
        {
            if (!Tokens.ContainsKey(token)) return false;
            if ((DateTime.Now - Tokens[token].Created).TotalMinutes > 10)
            {
                Tokens.Remove(token);
                return false;
            }
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

        public void AddID(string token, string id)
        {
            if (Tokens.ContainsKey(token)) Tokens[token].ContentIds.Add(id);
        }

        public bool ValidToken(string token)
        {
            if (Tokens.ContainsKey(token) && (DateTime.Now - Tokens[token].Created).TotalMinutes <= 10) return true;
            return false;
        }
        
        public List<string> GetIDs(string token)
        {
            if (Tokens.ContainsKey(token)) return Tokens[token].ContentIds;
            return new List<string>();
        }

        public void InvalidateToken(string token)
        {
            Tokens.Remove(token);
        }
    }
}