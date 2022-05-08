using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Security.Cryptography;

namespace NovaAPI.Util
{
    public static class EncryptionUtils
    {     
        
        private static byte[] GetHash(string inputString)
        {
            using HashAlgorithm algorithm = SHA512.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        private static byte[] GetSaltedHash(string inputString, byte[] salt) 
        {
            using HashAlgorithm algorithm = SHA512.Create();
            byte[] hashSalt = Encoding.UTF8.GetBytes(inputString).Concat(salt).ToArray();
            return algorithm.ComputeHash(hashSalt);
        }

        private static byte[] Get256Hash(string inputString)
        {
            using HashAlgorithm algorithm = SHA256.Create();
            return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        static RNGCryptoServiceProvider rngCsg = new RNGCryptoServiceProvider();
        public static byte[] GetSalt(int length) 
        {
            byte[] salt = new byte[length];
            rngCsg.GetBytes(salt);
            return salt;
        }

        public static string GetHashString(string inputString)
        {
            StringBuilder sb = new();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
        
        public static string Get256HashString(string inputString)
        {
            StringBuilder sb = new();
            foreach (byte b in Get256Hash(inputString))
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }

        public static string GetSaltedHashString(string inputString, byte[] salt) 
        {
            StringBuilder sb = new();
            foreach (byte b in GetSaltedHash(inputString, salt))
                sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}