using Microsoft.Extensions.Configuration;

namespace UKHO.ADDS.Configuration.Client
{
    internal class AddsConfigurationProvider : ConfigurationProvider
    {
        private readonly string _baseUri;
        private readonly string[] _serviceNames;

        public AddsConfigurationProvider(string baseUri, string[] serviceNames)
        {
            _baseUri = baseUri;
            _serviceNames = serviceNames;
        }

        public override void Load()
        {
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri($"{_baseUri}/grpc");

            var client = new AddsConfigurationClient(httpClient);
            var configuration = client.GetConfigurationAsync(_serviceNames).GetAwaiter().GetResult();

            Data = configuration;
        }
    }
}
