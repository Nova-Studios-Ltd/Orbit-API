using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using NovaAPI.Models;
using NovaAPI.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Controllers
{
    [Route("Auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly NovaChatDatabaseContext Context;

        public AuthController(NovaChatDatabaseContext context)
        {
            Context = context;
        }

        [HttpPost("Login")]
        public ActionResult<ReturnLoginUserInfo> AuthUser(LoginUserInfo info)
        {
            MySqlCommand cmd = new($"SELECT * FROM Users WHERE (Email=@email)", Context.GetUserConn());
            cmd.Parameters.AddWithValue("@email", info.Email);
            MySqlDataReader reader = cmd.ExecuteReader();
            string saltedPassword = EncryptionUtils.GetSaltedHashString(info.Password, (byte[])reader["Salt"]);
            while (reader.Read())
            {
                if (reader["Password"].ToString() == saltedPassword)
                {
                    reader.Close();
                    return new ReturnLoginUserInfo
                    {
                        UUID = reader["UUID"].ToString(),
                        Token = reader["Token"].ToString(),
                        PublicKey = reader["PubKey"].ToString(),
                        Key = new AESMemoryEncryptData
                        {
                            Content = reader["PrivKey"].ToString(),
                            IV = reader["IV"].ToString()
                        }
                    };
                }
                else
                {
                    reader.Close();
                    return StatusCode(403, $"Unable to authenticate user with email: {info.Email}");
                }
            }
            return StatusCode(404, $"Unable to find user with email {info.Email}");
        }

        [HttpPost("Register")]
        public ActionResult RegisterUser(CreateUserInfo info)
        {
            string UUID = Guid.NewGuid().ToString("N");
            string token = EncryptionUtils.GetSaltedHashString(UUID + info.Email + EncryptionUtils.GetHashString(info.Password) + info.Username + DateTime.Now.ToString(), EncryptionUtils.GetSalt(8));
            using (MySqlConnection conn = Context.GetUsers())
            {
                conn.Open();
                using MySqlCommand dis = new($"SELECT `GetRandomDiscriminator`(@user) AS `GetRandomDiscriminator`", conn);
                dis.Parameters.AddWithValue("@user", info.Username);
                MySqlDataReader reader = dis.ExecuteReader();
                string disc = null;
                while (reader.Read()) disc = reader["GetRandomDiscriminator"].ToString();
                reader.Close();

                using MySqlCommand checkForEmail = new($"SELECT * From Users WHERE (Email=@email)", conn);
                checkForEmail.Parameters.AddWithValue("@email", info.Email);
                MySqlDataReader read = checkForEmail.ExecuteReader();
                if (read.HasRows) return StatusCode(409);

                conn.Close();
                conn.Open();

                // Get Salt - Sometimes things are better with a bit of salt
                byte[] salt = EncryptionUtils.GetSalt(64);

                using MySqlCommand cmd = new($"INSERT INTO Users (UUID, Username, Discriminator, Password, Salt, Email, Token, Avatar, PubKey, PrivKey, IV) VALUES (@uuid, @user, @disc, @pass, @salt, @email, @tok, @avatar, @pubKey, @privKey, @iv)", conn);
                cmd.Parameters.AddWithValue("@uuid", UUID);
                cmd.Parameters.AddWithValue("@user", info.Username);
                cmd.Parameters.AddWithValue("@disc", disc);
                cmd.Parameters.AddWithValue("@pass", EncryptionUtils.GetSaltedHashString(info.Password, salt));
                cmd.Parameters.AddWithValue("@salt", salt);
                cmd.Parameters.AddWithValue("@email", info.Email);
                cmd.Parameters.AddWithValue("@tok", token);
                cmd.Parameters.AddWithValue("@avatar", Path.GetFileName(MediaController.DefaultAvatars[MediaController.GetRandom.Next(0, MediaController.DefaultAvatars.Length - 1)]));
                cmd.Parameters.AddWithValue("@pubKey", info.Key.Pub);
                cmd.Parameters.AddWithValue("@privKey", info.Key.Priv);
                cmd.Parameters.AddWithValue("@iv", info.Key.PrivIV);
                cmd.ExecuteNonQuery();

                using MySqlCommand createTable = new($"CREATE TABLE `{UUID}` (Id INT NOT NULL AUTO_INCREMENT, Property CHAR(255) NOT NULL, Value VARCHAR(1000) NOT NULL, PRIMARY KEY(`Id`)) ENGINE = InnoDB;", conn);
                createTable.ExecuteNonQuery();

                using MySqlCommand createKeystore = new($"CREATE TABLE `{UUID}_keystore` (UUID CHAR(255) NOT NULL , PubKey VARCHAR(1000) NOT NULL , PRIMARY KEY (`UUID`)) ENGINE = InnoDB;", conn);
                createKeystore.ExecuteNonQuery();

                using MySqlCommand createFriends = new($"CREATE TABLE `{UUID}_friends` (Id INT NOT NULL AUTO_INCREMENT, UUID CHAR(255) NOT NULL, State CHAR(255) NOT NULL, PRIMARY KEY (`Id`), UNIQUE (`UUID`)) ENGINE = InnoDB;", conn);
                createFriends.ExecuteNonQuery();
            }
            return StatusCode(200, "User created");
        }
    }
}
