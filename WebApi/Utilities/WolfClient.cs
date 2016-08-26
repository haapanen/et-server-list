using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using WebApi.Models;

namespace WebApi.Utilities
{
    public class WolfClientOptions
    {
        public string Address { get; set; }
        public int Port { get; set; }
    }

    public class WolfClient
    {
        private readonly WolfClientOptions _options;
        private readonly UdpClient _client;

        public WolfClient(WolfClientOptions options)
        {
            _options = options;
            _client = new UdpClient();
        }

        /// <summary>
        /// Fetches the status for a single server
        /// Does not timeout (excluding the default C# socket timeout)
        /// </summary>
        /// <returns></returns>
        public async Task<ServerStatus> GetStatus()
        {
            var packet = PacketUtilities.CreatePacket("getstatus");
            var task = await _client.SendAsync(packet, packet.Length, _options.Address, _options.Port);
            var result = await _client.ReceiveAsync();
            return PacketUtilities.ParseStatusResponse(result.Buffer);
        }
    }
}
