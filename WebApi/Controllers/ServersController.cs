using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Models.Repositories;

namespace WebApi.Controllers
{
    [Route("api/servers")]
    public class ServersController : Controller
    {
        private readonly IServerRepository _serverRepository;

        public ServersController(IServerRepository serverRepository)
        {
            _serverRepository = serverRepository;
        }

        [Route("{address}:{port}")]
        public async Task<IActionResult> Get(string address, int port)
        {
            return Ok(await _serverRepository.Get(address, port));
        }

        [Route("")]
        public async Task<IActionResult> Get()
        {
            return Ok(await _serverRepository.GetAll());
        }
    }
}
