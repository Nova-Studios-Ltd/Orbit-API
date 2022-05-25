using System;
using System.Collections.Generic;

namespace NovaAPI.DataTypes
{
    public class Token
    {
        public string channel_uuid;
        public List<string> ContentIds = new List<string>();
        public int Uses = 0;
        public DateTime Created;

        public Token(int uses, string channelUuid)
        {
            Uses = uses;
            channel_uuid = channelUuid;
            Created = DateTime.Now;
        }
    }
}