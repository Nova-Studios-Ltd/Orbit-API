using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class PasswordUpdate
    {
        public string Password { get; set; }
        public AESMemoryEncryptData Key { get; set; }
    }
}
