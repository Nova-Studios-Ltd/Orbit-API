using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class ReturnLoginUserInfo
    {
        public string UUID { get; set; }
        public string Token { get; set; }
        public string PublicKey { get; set; }
        public AESMemoryEncryptData Key { get; set; }
    }
}
