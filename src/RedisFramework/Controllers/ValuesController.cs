using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace SimplyInjectedDistributedCache.Controllers
{
    [RoutePrefix("Values")]
    public class ValuesController : ApiController
    {
        private readonly IDistributedCache _distributedCache;

        public ValuesController(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
        }

        [HttpGet]
        [Route("")]
        public async Task<IHttpActionResult> Get(CancellationToken cancellationToken)
        {
            var value = await _distributedCache.GetStringAsync("my-key", cancellationToken);
            return Ok(value);
        }

        [HttpPost]
        [Route("")]
        public async Task<IHttpActionResult> Post([FromBody] string value, CancellationToken cancellationToken)
        {
            var options = new DistributedCacheEntryOptions()
            {
                AbsoluteExpiration = DateTime.Now.AddMinutes(1)
            };

            await _distributedCache.SetAsync(
                "my-key",
                Encoding.ASCII.GetBytes(value),
                options,
                cancellationToken);

            return Ok();
        }
    }
}
