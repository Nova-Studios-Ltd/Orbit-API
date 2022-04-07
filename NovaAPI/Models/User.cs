using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NovaAPI.Models
{
    public class User
    {
        public string UUID { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string Email { get; set; }
        public DateTime CreationDate { get; set; }
        public string Avatar { get; set; }
    }
}
