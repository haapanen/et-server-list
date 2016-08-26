using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Models.Repositories
{
    public interface IServerRepository
    {
        Task<Server> Get(string address, int port);
        Task<List<Server>> GetAll();
    }
}
