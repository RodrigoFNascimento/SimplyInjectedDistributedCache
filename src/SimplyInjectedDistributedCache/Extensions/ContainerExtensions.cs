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
            var certificate = ConfigurationManager.AppSettings["Redis.Certificate"];
            var instanceName = ConfigurationManager.AppSettings["Redis.InstanceName"];
            var configuration = new ConfigurationOptions
            {
                EndPoints = { { host, port } },
                Password = password
            };

            configuration.ConfigureSSL(certificate, password);

            container.Register(() =>
            {
                return ConnectionMultiplexer.Connect(configuration);
            }, Lifestyle.Singleton);

            container.Register<IDistributedCache>(() =>
            {
                var connection = container.GetInstance<ConnectionMultiplexer>();
                return new RedisCache(new RedisCacheOptions
                {
                    Configuration = connection.Configuration,
                    ConfigurationOptions = configuration,
                    InstanceName = instanceName
                });
            }, Lifestyle.Singleton);
        }
    }
}