using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Models
{
    public class ServerStatus
    {
        public List<string> Players { get; set; }
        public Dictionary<string, string> ServerInfo { get; set; }
    }
}
