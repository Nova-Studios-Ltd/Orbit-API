namespace NovaAPI.Util
{
    public static class MySqlServerData
    {
        // General Server Information
        public static string Server { get; set; }
        public static string Port { get; set; }
        public static string User { get; set; }
        public static string Password { get; set; }
        public static string SslMode { get; set; }
        
        // Database Names
        public static string UserDatabaseName { get; set; }
        public static string ChannelsDatabaseName { get; set; }
        public static string MasterDatabaseName { get; set; }

        
        // Table creation strings
        public static string UserTableString = @"CREATE TABLE `Users` (
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

        public static string ChannelTableString = @"CREATE TABLE `Channels` (
          `Table_ID` char(255) NOT NULL,
          `Owner_UUID` char(255) NOT NULL,
          `ChannelIcon` char(255) NOT NULL,
          `IsGroup` tinyint(1) NOT NULL DEFAULT '0',
          `GroupName` char(255) NOT NULL DEFAULT 'NewGroup',
          `Timestamp` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
          PRIMARY KEY (`Table_ID`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
        ";

        public static string ChannelMediaTableString = @"CREATE TABLE `ChannelMedia` (
          `File_UUID` char(255) NOT NULL,
          `Filename` char(255) NOT NULL,
          `MimeType` char(255) NOT NULL,
          `Size` int NOT NULL,
          `ContentWidth` int NOT NULL DEFAULT '0',
          `ContentHeight` int NOT NULL DEFAULT '0',
          PRIMARY KEY (`File_UUID`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;";
        
        
        public static string CreateSQLString(string database = "")
        {
            if (database == "")
                return $"server={Server};port={Port};user={User};password={Password};";
            else
                return $"server={Server};port={Port};user={User};password={Password};database={database};";
        }
    }
}