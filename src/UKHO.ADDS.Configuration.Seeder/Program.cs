using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKHO.ADDS.Configuration.Schema;

namespace UKHO.ADDS.Configuration.Seeder
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            if (args.Length == 0)
            {
                var builder = Host.CreateApplicationBuilder(args);

                var configFilePath = builder.Configuration[WellKnownConfigurationName.ConfigurationFilePath]!;

                builder.AddAzureTableClient(WellKnownConfigurationName.ConfigurationServiceTableStorageName);
                builder.Services.AddSingleton<ConfigurationWriter>();
                builder.Services.AddHostedService(x => new LocalSeederService(x.GetRequiredService<IHostApplicationLifetime>(), x.GetRequiredService<ConfigurationWriter>(), configFilePath));

                var app = builder.Build();

                await app.RunAsync();
            }
            else
            {
                if (args.Length != 3)
                {
                    Console.WriteLine("Usage: <environment> <configFilePath> <tableUri>");
                    return 4;
                }

                var idToken = Environment.GetEnvironmentVariable("idToken");

                if (string.IsNullOrEmpty(idToken))
                {
                    Console.WriteLine("ID Token is not set in the environment variables.");
                    return 4;
                }

                //var credential = new IdTokenCredential(idToken);

                var environmentName = args[0];
                Console.WriteLine($"Seeding configuration for environment: {environmentName}");
                var environment = AddsEnvironment.Parse(environmentName);

                var configFilePath = args[1];

                if (!File.Exists(configFilePath))
                {
                    Console.WriteLine($"Configuration file not found: {configFilePath}");
                    return 4;
                }

                Console.WriteLine($"Reading configuration from: {configFilePath}");
                var configJson = await File.ReadAllTextAsync(configFilePath);

                var tableUri = args[2];

                if (!Uri.TryCreate(tableUri, UriKind.Absolute, out var uri))
                {
                    Console.WriteLine($"Invalid Table Storage URI: {tableUri}");
                    return 4;
                }

                Console.WriteLine($"Using Table Storage URI: {tableUri}");
                var credential = new DefaultAzureCredential();
                var tableServiceClient = new TableServiceClient(new Uri(tableUri), credential);
                var configurationWriter = new ConfigurationWriter(tableServiceClient);

                await configurationWriter.WriteConfigurationAsync(environment, configJson);
            }

            return 0;
        }
    }
}
