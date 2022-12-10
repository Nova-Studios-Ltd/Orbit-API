using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NovaAPI.DataTypes;
using NovaAPI.Util;

namespace NovaAPI.Controllers;

public static class ResetTokenController
{
    private static Dictionary<string, ResetToken> Tokens = new Dictionary<string, ResetToken>();
    
    // Cleans up invalid tokens and associated files every 10m
    private static Timer cleanup = new Timer((state =>
    {
        string[] expiredTokens = Tokens.AsParallel()
            .Where(x => x.Value.CleanUp || (DateTime.Now - x.Value.Created).TotalMinutes >= 10)
            .Select(x => x.Key).ToArray();

        foreach (string token in expiredTokens)
        {
            Tokens.Remove(token);
        }
    }), null, 10000, 0);

    public static string GenerateToken(string user_uuid)
    {
        string token = EncryptionUtils.GetSaltedHashString(user_uuid + DateTime.Now, EncryptionUtils.GetSalt(4));
        Tokens.Add(token, new ResetToken(user_uuid));
        return token;
    }

    public static string GetToken(string token)
    {
        if (Tokens.ContainsKey(token))
            return Tokens[token].UUID;
        return "";
    }

    public static void InvalidToken(string token)
    {
        if (Tokens.ContainsKey(token))
            Tokens[token].CleanUp = true;
    }
}