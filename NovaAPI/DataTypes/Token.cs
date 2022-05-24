using System;
using System.Collections.Generic;

namespace NovaAPI.DataTypes
{
    public class Token
    {
        public List<string> ContentIds = new List<string>();
        public int Uses = 0;
        public DateTime Created;

        public Token(int uses)
        {
            Uses = uses;
            Created = DateTime.Now;
        }
    }
}