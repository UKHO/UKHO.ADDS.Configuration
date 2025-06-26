using System.Dynamic;
using AzureKeyVaultEmulator.Aspire.Hosting;
using HandlebarsDotNet;
using Microsoft.Extensions.Hosting;
using Projects;
using UKHO.ADDS.Configuration.Aspire.Extensions;
using UKHO.ADDS.Configuration.Schema;

namespace UKHO.ADDS.Configuration.Aspire
{
    public static class DistributedApplicationBuilderExtensions
    {
        public static IResourceBuilder<ProjectResource> AddConfiguration(this IDistributedApplicationBuilder builder, string configJsonPath, Action<EndpointTemplateBuilder> templateCallback)
        {
            var storage = builder.AddAzureStorage(WellKnownConfigurationName.ConfigurationServiceStorageName).RunAsEmulator(e => { e.WithDataVolume(); });

            var storageTable = storage.AddTables(WellKnownConfigurationName.ConfigurationServiceTableStorageName);
            var keyVault = builder.AddAzureKeyVaultEmulator(WellKnownConfigurationName.ConfigurationServiceKeyVaultName, new KeyVaultEmulatorOptions { Persist = false });

            var configOriginalPath = Path.GetFullPath(configJsonPath);
            var configFilePath = CopyToTempFile(configOriginalPath);

            var configJson = File.ReadAllText(configFilePath);

            IResourceBuilder<ProjectResource> seederService = null!;

            if (builder.Environment.IsDevelopment())
            {
                var template = Handlebars.Compile(configJson);
                var context = new ExpandoObject();

                var templateBuilder = new EndpointTemplateBuilder();

                // Only add the seeder service in local development environment
                seederService = builder.AddProject<UKHO_ADDS_Configuration_Seeder>(WellKnownConfigurationName.ConfigurationSeederName)
                    .WithReference(storageTable)
                    .WithEnvironment(x =>
                    {
                        x.EnvironmentVariables.Add(WellKnownConfigurationName.ConfigurationFilePath, configFilePath);

                        templateCallback(templateBuilder);

                        foreach (var endpoint in templateBuilder.Templates)
                        {
                            var url = endpoint.Resource.GetEndpoint(endpoint.UseHttps ? "https" : "http").Url;
                            var urlBuilder = new UriBuilder(url);

                            if (!string.IsNullOrEmpty(endpoint.Hostname))
                            {
                                urlBuilder.Host = endpoint.Hostname;
                            }

                            var path = endpoint.Path;

                            if (path != null && !path.EndsWith("/"))
                            {
                                path = string.Concat(path, "/");
                                urlBuilder.Path = path;
                            }

                            context.TryAdd(endpoint.Name, urlBuilder.ToString());
                        }

                        var resultJson = template(context);
                        File.WriteAllText(configFilePath, resultJson);

                        //var fssBuilderEndpoint = new UriBuilder(addsMockEndpoint.Url) { Host = "host.docker.internal", Path = "fss/" };
                        //var fssOrchestratorEndpoint = new UriBuilder(addsMockEndpoint.Url) { Host = addsMockEndpoint.Host, Path = "fss/" };
                        //var scsEndpoint = new UriBuilder(addsMockEndpoint.Url) { Host = addsMockEndpoint.Host, Path = "scs/" };
                    });
            }

            var configurationService = builder.AddProject<UKHO_ADDS_Configuration>(WellKnownConfigurationName.ConfigurationServiceName)
                .WithReference(storageTable)
                .WaitFor(storageTable)
                .WithReference(keyVault)
                .WaitFor(keyVault)
                .WithScalar("API Browser")
                .WithDashboard("Configuration Dashboard")
                
                .WithEnvironment(WellKnownConfigurationName.AddsEnvironmentName, AddsEnvironment.Local.Value);

            if (seederService != null)
            {
                // Make sure the seeder runs before the configuration service starts
                configurationService.WithReference(seederService).WaitFor(seederService);
            }

            return configurationService;
        }

        public static IResourceBuilder<ProjectResource> WithConfiguration(this IResourceBuilder<ProjectResource> builder, IResourceBuilder<IResourceWithServiceDiscovery> service)
        {
            builder.WithReference(service);
            builder.WaitFor(service);
            builder.WithEnvironment(WellKnownConfigurationName.AddsEnvironmentName, AddsEnvironment.Local.Value);

            return builder;
        }

        private static string CopyToTempFile(string sourceFilePath)
        {
            var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            var content = File.ReadAllText(sourceFilePath);
            File.WriteAllText(tempFilePath, content);

            return tempFilePath;
        }
    }
}
