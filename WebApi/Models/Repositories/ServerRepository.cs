using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApi.Database;
using WebApi.Utilities;

namespace WebApi.Models.Repositories
{
    public class ServerRepositoryOptions
    {
        /// <summary>
        /// How many times getstatus can fail before we remove the server
        /// </summary>
        public int FailedGetStatusLimit { get; set; }
        /// <summary>
        /// How long getstatus can last before we time it out
        /// </summary>
        public TimeSpan GetStatusTimeout { get; set; }
        /// <summary>
        /// How often do we query for each server status?
        /// </summary>
        public TimeSpan QueryInterval { get; set; }
    }

    public class ServerRepository : IServerRepository
    {
        /// <summary>
        /// Store servers in a simple list
        /// </summary>
        private ConcurrentBag<Server> _servers;

        /// <summary>
        /// Repository options
        /// </summary>
        private ServerRepositoryOptions _options;

        public ServerRepository(ServerRepositoryOptions options)
        {
            _options = options;
            _servers = new ConcurrentBag<Server>();
            using (var db = new ServerContext())
            {
                db.Servers.ToList().ForEach(s => _servers.Add(new Server
                {
                    Id = s.Id,
                    Port = s.Port,
                    Address = s.Address,
                    LastQueryTime = DateTime.MinValue,
                    LatestServerStatus = null,
                    GetStatusFailCount = 0
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

            var match = _servers.FirstOrDefault(s => s.Address == address && s.Port == port);
            if (match == null)
            {
                var newServer = await GetNewServer(address, port, _options.GetStatusTimeout);
                // don't add the server to database if we can't reach it
                if (newServer == null) return null;
                _servers.Add(newServer);
                using (var db = new ServerContext())
                {   
                    db.Servers.Add(newServer);
                    await db.SaveChangesAsync();
                }
                return newServer;
            }

            var server = match;
            if (DateTime.UtcNow - server.LastQueryTime < _options.QueryInterval)
            {
                return server;
            }

            await UpdateServerStatus(server);
            // Remove server getstatus has failed too many times
            await CheckFailCount(server);
            
            return server;
        }

        /// <summary>
        /// Checks if the server getstatus query has failed too many times,
        /// if yes, deletes server
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        private async Task CheckFailCount(Server server)
        {
            if (server.GetStatusFailCount > _options.FailedGetStatusLimit)
            {
                _servers = new ConcurrentBag<Server>(_servers.Where(s => s.Id != server.Id));
                using (var db = new ServerContext())
                {
                    db.Servers.Remove(server);
                    await db.SaveChangesAsync();
                }
            }
        }

        /// <summary>
        /// Updates the server's status.
        /// Deletes the server if no status could be received in a certain amount of tries
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        private async Task UpdateServerStatus(Server server)
        {
            var ctSource = new CancellationTokenSource();

            var task = new WolfClient(new WolfClientOptions { Address = server.Address, Port = server.Port }).GetStatus();
            var completed = await Task.WhenAny(task, Task.Delay(_options.GetStatusTimeout, ctSource.Token));
            if (completed == task)
            {
                server.LatestServerStatus = task.Result;
                server.LastQueryTime = DateTime.UtcNow;
                server.GetStatusFailCount = 0;
                ctSource.Cancel();
            }
            else
            {
                server.GetStatusFailCount++;
                return;
            }
        }

        /// <summary>
        /// Returns all servers. Makes sure each server's status is max 30 seconds old
        /// </summary>
        /// <returns></returns>
        public async Task<List<Server>> GetAll()
        {
            var tasks = new List<Task>();
            var servers = _servers.Select(async (s) =>
            {
                if (DateTime.UtcNow - s.LastQueryTime > TimeSpan.FromSeconds(30))
                {
                    await UpdateServerStatus(s);
                    await CheckFailCount(s);
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
                LatestServerStatus = task.Result,
                GetStatusFailCount = 0
            };
        }
    }
}
