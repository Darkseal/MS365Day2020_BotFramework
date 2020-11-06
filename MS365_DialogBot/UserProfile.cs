using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MS365_DialogBot
{
    /// <summary>
    /// This is our application state. Just a regular serializable .NET class.
    /// </summary>
    public class UserProfile
    {
        public string Language { get; set; }

        public string Name { get; set; }

        public string City { get; set; }

        public int Age { get; set; }

        public bool IsRegistering { get; set; }
    }
}
