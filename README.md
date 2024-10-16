# SimplyInjectedDistributedCache
An example of how distributed caches can be used in .NET Framework web APIs with Simple Injector.

## About

Information regarding dependency injection in .NET Framework applications can be hard to come by. This repository aims to exemplify, for educational purposes, how distributed caches can be consumed by these legacy applications.

## Distributed cache

According to Wikipedia:

> In computing, a distributed cache is an extension of the traditional concept of cache used in a single locale. A distributed cache may span multiple servers so that it can grow in size and in transactional capacity. It is mainly used to store application data residing in database and web session data.

## Distributed cache in .NET applications

There are a few ways distributed caches can be consumed by .NET applications. One of the simplest is by injecting Microsoft's [IDistributedCache](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache?view=net-8.0) interface using their [IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection?view=net-8.0). [This article](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-8.0) show you how to do it. However, it's not so simple if you're not using more recent versions of .NET.

.NET Framework applications tend to use third-party libraries to handle their dependency injection. While they may be well documented, some specific injections are not so straightfoward, such as this case.

## Injecting IDistributedCache

For this example, we're using [Simple Injector](https://simpleinjector.org/), a popular Dependency Injection (DI) library.

### Simple Injector

> Simple Injector is an easy-to-use Dependency Injection (DI) library for .NET 4.5, .NET Core, .NET 5, .NET Standard, UWP, Mono, and Xamarin. Simple Injector is easily integrated with frameworks such as Web API, MVC, WCF, ASP.NET Core and many others. It’s easy to implement the Dependency Injection pattern with loosely coupled components using Simple Injector.

Let's do a basic setup of SimpleInjector. Start by installing the necessary SimpleInjector packages:

```powershell
Install-package SimpleInjector;
Install-package SimpleInjector.Integration.WebApi
```

Next, setup SimpleInjector in the `Global.asax` file.

```csharp
public class WebApiApplication : System.Web.HttpApplication
{
    protected void Application_Start()
    {
        // ...

        var container = new Container();
        container.RegisterWebApiControllers(GlobalConfiguration.Configuration);
        container.RegisterDistributedCache();
        container.Verify();
        GlobalConfiguration.Configuration.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);
    }
}
```

For more infomation regarding the setup, have a look at their [official page](https://docs.simpleinjector.org/en/latest/quickstart.html).

### Redis

There are many distributed cache solutions you can connect to, but for this example we're using [Redis](https://redis.io/) due to how popular it is.

> Redis is a source-available, in-memory storage, used as a distributed, in-memory key–value database, cache and message broker, with optional durability

First, we're going to install Microsoft's own package for connecting to the service.

```powershell
Install-package Microsoft.Extensions.Caching.StackExchangeRedis
```

Then we make the connection info available to our application. In this case, we're adding it to the appsettings so we can easily read and modify the values without changing the code.

```xml
<configuration>
  <appSettings>
	<add key="Redis.Host" value="127.0.0.1"/>
	<add key="Redis.Port" value="6379"/>
	<add key="Redis.Password" value="password"/>
	<add key="Redis.UseSSL" value="false"/>
	<add key="Redis.Certificate" value="Certificates\redis.pfx"/>
	<add key="Redis.InstanceName" value="my-app"/>
  </appSettings>
</configuration>
```

> Remember to never leave sensitive data in the application's settings or repository. We're doing it here because our purpose is purely educational and handling sensitive data securely is not in the scope of our study.

In order to make the code cleaner and separate responsabilities better, I've decided to create an extension method for SimpleInjector's container specifically to handle IDistributedCache's injection.

```csharp
public static void RegisterDistributedCache(this Container container)
{
    var host = ConfigurationManager.AppSettings["Redis.Host"];
    var port = Convert.ToInt32(ConfigurationManager.AppSettings["Redis.Port"]);
    var password = ConfigurationManager.AppSettings["Redis.Password"];  // Find somewhere safe
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
```

Since we may need to setup a [SSL connection](https://www.ibm.com/docs/en/cics-tg-zos/9.3.0?topic=ssl-how-connection-is-established), I've also create an extension method for the `ConfigurationOptions`:

```csharp
public static void ConfigureSSL(this ConfigurationOptions options, string certificate, string password)
{
    options.Ssl = true;
    options.SslProtocols = SslProtocols.Tls12;
    options.CertificateSelection += delegate
    {
        return new X509Certificate2(certificate, password);
    };
    options.CertificateValidation += delegate (
        object sender,
        X509Certificate certificatez,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        return certificatez?.Subject != null;
    };
}
```

If you're storing the certificate locally, `certificate` should include the file path, name and extension.

### Using IDistributedCache

Now you can use the injected `IDistributedCache` instance in any class. For example, let's use it in two very simple endpoints to store and fetch a value from the cache:

```csharp
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
```