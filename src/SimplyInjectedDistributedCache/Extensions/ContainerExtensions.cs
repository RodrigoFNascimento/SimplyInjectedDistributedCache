using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using SimpleInjector;
using StackExchange.Redis;
using System;
using System.Configuration;

namespace SimplyInjectedDistributedCache.Extensions
{
    public static class ContainerExtensions
    {
        public static void RegisterDistributedCache(this Container container)
        {
            var host = ConfigurationManager.AppSettings["Redis.Host"];
            var port = Convert.ToInt32(ConfigurationManager.AppSettings["Redis.Port"]);
            var password = ConfigurationManager.AppSettings["Redis.Password"];
            var useSSL = Convert.ToBoolean(ConfigurationManager.AppSettings["Redis.UseSSL"]);
            var certificate = ConfigurationManager.AppSettings["Redis.Certificate"];
            var instanceName = $"{ConfigurationManager.AppSettings["Redis.InstanceName"]}:";
            var configuration = new ConfigurationOptions
            {
                EndPoints = { { host, port } },
                Password = password
            };

            if (useSSL)
                configuration.ConfigureSSL(certificate, password);

            container.Register<IConnectionMultiplexer>(
                () => ConnectionMultiplexer.Connect(configuration),
                Lifestyle.Singleton);

            container.Register<IDistributedCache>(() =>
            {
                return new RedisCache(new RedisCacheOptions
                {
                    Configuration = container.GetInstance<IConnectionMultiplexer>().Configuration,
                    ConfigurationOptions = configuration,
                    InstanceName = instanceName
                });
            }, Lifestyle.Singleton);
        }
    }
}