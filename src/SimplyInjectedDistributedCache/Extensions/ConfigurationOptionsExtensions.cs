using StackExchange.Redis;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace SimplyInjectedDistributedCache.Extensions
{
    public static class ConfigurationOptionsExtensions
    {
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
                X509Certificate certificates,
                X509Chain chain,
                SslPolicyErrors sslPolicyErrors)
            {
                return certificates?.Subject != null;
            };
        }
    }
}