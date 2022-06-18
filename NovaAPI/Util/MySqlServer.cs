using MySql.Data.MySqlClient;

namespace NovaAPI.Util
{
    public enum Database
    {
        Channel,
        User,
        Master
    }
    public static class MySqlServer
    {
        public static bool AutoConfig { get; set; }
        // General Server Information
        public static string Server { get; set; }
        public static string Port { get; set; }
        public static string User { get; set; }
        public static string Password { get; set; }

        // Database Names
        public static string UserDatabaseName { get; set; }
        public static string ChannelsDatabaseName { get; set; }
        public static string MasterDatabaseName { get; set; }

        
        // Table creation strings
        public static string UserTableString = @"CREATE TABLE IF NOT EXISTS `Users` (
            `UUID` char(255) NOT NULL,
        `Username` char(255) NOT NULL,
        `Discriminator` int NOT NULL,
        `Password` char(255) NOT NULL,
        `Salt` varbinary(64) NOT NULL,
            `Email` char(255) NOT NULL,
        `Token` char(255) NOT NULL,
        `Avatar` varchar(1000) NOT NULL,
            `PubKey` varchar(1000) NOT NULL,
            `PrivKey` varchar(10000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
        `IV` varchar(1000) NOT NULL,
            `CreationDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
        PRIMARY KEY (`UUID`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
            ";

        public static string ChannelTableString = @"CREATE TABLE IF NOT EXISTS `Channels` (
          `Table_ID` char(255) NOT NULL,
          `Owner_UUID` char(255) NOT NULL,
          `ChannelIcon` char(255) NOT NULL,
          `IsGroup` tinyint(1) NOT NULL DEFAULT '0',
          `GroupName` char(255) NOT NULL DEFAULT 'NewGroup',
          `Timestamp` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
          PRIMARY KEY (`Table_ID`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
        ";

        public static string ChannelMediaTableString = @"CREATE TABLE IF NOT EXISTS `ChannelMedia` (
          `File_UUID` char(255) NOT NULL,
          `User_UUID` char(255) NOT NULL,
          `Filename` char(255) NOT NULL,
          `MimeType` char(255) NOT NULL,
          `Size` int NOT NULL,
          `ContentWidth` int NOT NULL DEFAULT '0',
          `ContentHeight` int NOT NULL DEFAULT '0',
          PRIMARY KEY (`File_UUID`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;";

        public static string DiscrimnatorGen = @"
            CREATE FUNCTION `GetRandomDiscriminator`(`username` CHAR(255)) RETURNS int
                NO SQL
                DETERMINISTIC
            BEGIN
                DECLARE newDis INT DEFAULT 1;
                WHILE newDis = 1 OR EXISTS(SELECT * FROM Users WHERE (Discriminator=newDis) AND (Username=username)) DO
                    SET newDis = FLOOR(RAND() * 9999);
                END WHILE;
                RETURN newDis;
            END$$
            ";
        
        public static string CreateSQLString(string database = "")
        {
            if (database == "")
                return $"server={Server};port={Port};user={User};password={Password};";
            else
                return $"server={Server};port={Port};user={User};password={Password};database={database};";
        }

        public static MySqlConnection CreateSQLConnection(Database database)
        {
            if (database == Database.Channel)
                return new MySqlConnection(CreateSQLString(ChannelsDatabaseName));
            else if (database == Database.Master)
                return new MySqlConnection(CreateSQLString(MasterDatabaseName));
            else if (database == Database.User)
                return new MySqlConnection(CreateSQLString(UserDatabaseName));
            else
                return new MySqlConnection(CreateSQLString());
        }
    }
}