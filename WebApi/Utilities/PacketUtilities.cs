using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebApi.Models;

namespace WebApi.Utilities
{
    public static class PacketUtilities
    {
        /// <summary>
        /// Out of band Q3 packet prefix
        /// </summary>
        public static readonly byte[] PacketPrefix = {0xff, 0xff, 0xff, 0xff};

        /// <summary>
        /// Creates an out of band Q3 packet
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static byte[] CreatePacket(string message)
        {
            return PacketPrefix.Concat(Encoding.ASCII.GetBytes(message)).ToArray();
        }

        /// <summary>
        /// Converts the status response byte array to a server status object
        /// </summary>
        /// <param name="statusResponseBuffer"></param>
        /// <returns></returns>
        public static ServerStatus ParseStatusResponse(byte[] statusResponseBuffer)
        {
            var statusResponse = Encoding.ASCII.GetString(statusResponseBuffer);
            var serverStatus = new ServerStatus
            {
                Players = new List<string>(),
                ServerInfo = new Dictionary<string, string>()
            };
            var rows = statusResponse.Split('\n');

            for (var i = 2; i < rows.Length - 1; ++i)
            {
                serverStatus.Players.Add(rows[i].Split('"')[1]);
            }

            var keyValues = rows[1].Split('\\');
            string key = null;
            foreach (var val in keyValues.Skip(1))
            {
                if (key == null)
                {
                    key = val;
                }
                else
                {
                    serverStatus.ServerInfo[key] = val;
                    key = null;
                }
            }
            return serverStatus;
        }
    }
}
