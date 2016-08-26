using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using WebApi.Database;
using WebApi.Utilities;

namespace WebApi.Models.Repositories
{
    public class ServerRepository : IServerRepository
    {
        /// <summary>
        /// Store servers in a simple list
        /// </summary>
        private ConcurrentBag<Server> _servers;

        public ServerRepository()
        {
            _servers = new ConcurrentBag<Server>();
            using (var db = new ServerContext())
            {
                db.Servers.ToList().ForEach(s => _servers.Add(new Server
                {
                    Id = s.Id,
                    Port = s.Port,
                    Address = s.Address,
                    LastQueryTime = DateTime.MinValue,
                    LatestServerStatus = null
                }));
            }
        }

        /// <summary>
        /// Gets the server from cache or fetches a new status response if it has been
        /// more than 30 seconds since last update
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<Server> Get(string address, int port)
        {
            if (port <= 0 || port > 65536)
            {
                return null;
            }

            var match = _servers.Where(s => s.Address == address && s.Port == port);
            if (!match.Any())
            {
                var newServer = await GetNewServer(address, port, TimeSpan.FromSeconds(1));
                if (newServer != null)
                {
                    _servers.Add(newServer);
                    using (var db = new ServerContext())
                    {   
                        db.Servers.Add(newServer);
                        db.SaveChanges();
                    }
                }
                return newServer;
            }

            var server = match.First();
            if (DateTime.UtcNow - server.LastQueryTime < TimeSpan.FromSeconds(30))
            {
                return server;
            }

            server.LatestServerStatus =
                await new WolfClient(new WolfClientOptions {Address = address, Port = port}).GetStatus();
            server.LastQueryTime = DateTime.UtcNow;
            return server;
        }

        /// <summary>
        /// Returns all servers. Makes sure each server's status is max 30 seconds old
        /// </summary>
        /// <returns></returns>
        public async Task<List<Server>> GetAll()
        {
            var servers = _servers.Select(async (s) =>
            {
                if (DateTime.UtcNow - s.LastQueryTime > TimeSpan.FromSeconds(30))
                {
                    s.LatestServerStatus =
                        await new WolfClient(new WolfClientOptions {Address = s.Address, Port = s.Port}).GetStatus();
                    s.LastQueryTime = DateTime.UtcNow;
                }
                return s;
            });

            return new List<Server>(await Task.WhenAll(servers));
        }

        /// <summary>
        /// Tries to fetch server status information and return a new server based on it 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        private static async Task<Server> GetNewServer(string address, int port, TimeSpan timeout)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var client = new WolfClient(new WolfClientOptions { Address = address, Port = port });
            var task = client.GetStatus();
            var completed = await Task.WhenAny(task, Task.Delay(timeout, cancellationTokenSource.Token));
            if (completed == task)
            {
                cancellationTokenSource.Cancel();
            }
            else
            {
                return null;
            }

            if (task.Result == null)
            {
                return null;
            }

            return new Server
            {
                Address = address,
                Port = port,
                LastQueryTime = DateTime.UtcNow,
                LatestServerStatus = task.Result
            };
        }
    }
}
