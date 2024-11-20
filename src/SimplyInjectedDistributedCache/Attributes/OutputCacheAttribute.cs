using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace SimplyInjectedDistributedCache.Attributes
{
    public class OutputCacheAttribute : ActionFilterAttribute
    {
        private readonly int _durationInSeconds;
        private IDistributedCache _cache;

        /// <summary>
        /// Adds output caching functionality to an endpoint.
        /// </summary>
        /// <param name="durationInSeconds">How long the response should be stored in cache in seconds.</param>
        public OutputCacheAttribute(int durationInSeconds)
        {
            _durationInSeconds = durationInSeconds;
        }

        /// <summary>
        /// Occurs before the action method is invoked.
        /// </summary>
        /// <param name="actionContext">The action context.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task OnActionExecutingAsync(HttpActionContext actionContext, CancellationToken cancellationToken)
        {
            var cacheControlHeader = actionContext.Request.Headers.CacheControl;
            var noCache = cacheControlHeader?.NoCache ?? false;

            if (noCache)
                return;

            // The constructor is executed before the IDistributedCache instance is injected,
            // so we need to attribute it here.
            _cache = (IDistributedCache)GlobalConfiguration.Configuration.DependencyResolver
                .GetService(typeof(IDistributedCache));

            var cacheKey = GenerateCacheKey(actionContext.Request);

            var cachedResponse = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedResponse))
            {
                var cachedData = JsonConvert.DeserializeObject<CachedResponse>(cachedResponse);

                // Create response with cached data
                var response = actionContext.Request.CreateResponse();
                response.Content = new StringContent(cachedData.Content);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(cachedData.ContentType);
                response.StatusCode = (HttpStatusCode)cachedData.StatusCode;

                foreach (var header in cachedData.Headers)
                {
                    response.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (!string.IsNullOrEmpty(cachedData.ContentEncoding))
                {
                    response.Content.Headers.ContentEncoding.Add(cachedData.ContentEncoding);
                }

                actionContext.Response = response;
            }
        }

        /// <summary>
        /// Occurs after the action method is invoked.
        /// </summary>
        /// <param name="actionExecutedContext">The action executed context.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            var cacheControlHeader = actionExecutedContext.Request.Headers.CacheControl;

            var noCache = cacheControlHeader?.NoCache ?? false;

            if (noCache)
                actionExecutedContext.Response.Headers.CacheControl.NoCache = noCache;

            var noStore = cacheControlHeader?.NoStore ?? false;

            if (noStore)
            {
                actionExecutedContext.Response.Headers.CacheControl.NoStore = noStore;
                return;
            }

            if (actionExecutedContext.Response != null && actionExecutedContext.Response.IsSuccessStatusCode)
            {
                var cacheKey = GenerateCacheKey(actionExecutedContext.Request);

                var headers = actionExecutedContext.Response.Headers.ToDictionary(x => x.Key, y => y.Value);
                var statusCode = (int)actionExecutedContext.Response.StatusCode;
                var contentEncoding = actionExecutedContext.Response.Content?.Headers?.ContentEncoding?.FirstOrDefault();
                var contentType = actionExecutedContext.Response.Content?.Headers?.ContentType?.MediaType;
                var content = await actionExecutedContext.Response.Content.ReadAsStringAsync();

                var cachedResponse = new CachedResponse
                {
                    Content = content,
                    ContentType = contentType,
                    StatusCode = statusCode,
                    Headers = headers,
                    ContentEncoding = contentEncoding
                };

                var serializedResponse = JsonConvert.SerializeObject(cachedResponse);

                await _cache.SetStringAsync(cacheKey, serializedResponse, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_durationInSeconds)
                });

                actionExecutedContext.Response.Headers.CacheControl.Public = true;
            }
        }

        private string GenerateCacheKey(HttpRequestMessage request) =>
            request.RequestUri.AbsolutePath.Trim('/').Replace('/', ':');

        private sealed class CachedResponse
        {
            public string Content { get; set; }
            public string ContentType { get; set; }
            public int StatusCode { get; set; }
            public Dictionary<string, IEnumerable<string>> Headers { get; set; }
            public string ContentEncoding { get; set; }
        }
    }
}