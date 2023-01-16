using CustomsCloud.InfrastructureCore.Consul;
using CustomsCloud.InfrastructureCore.Interfaces.Customs;
using CustomsCloud.InfrastructureCore.Queue;
using CustomsCloud.InfrastructureCore.Logging;
using CustomsCloud.InfrastructureCore.Worker.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using CustomsCloud.InfrastructureCore.Interfaces.DependencyInjection;

namespace CustomsCloud.InfrastructureCore.Worker.CloudApp
{
    public class CloudWorkerApp
    {
        public static ICloudWorkerAppBuilder CreateCloudWorkerAppBuilder()
        {
            return new CloudWorkerAppBuilder();
        }

        public static IHost Build(ICloudWorkerAppBuilder builder)
        {
            FillWorkerAssemblies(builder);

            var workerAppParameters = builder.Build();
            var applicationName =
                string.IsNullOrEmpty(workerAppParameters.ApplicationName) ?
                GetApplicationName(workerAppParameters.BaseType) :
                workerAppParameters.ApplicationName;

            var environment = GetEnvironment();
            var info = GetApplicationInfo(environment, applicationName);

            var hostBuilder = Host.CreateDefaultBuilder(workerAppParameters.Args);
            InitializeConfiguration(hostBuilder, environment);
            InitializeServices(hostBuilder, workerAppParameters, info);
            BasicInitialize(hostBuilder, applicationName);

            var host = hostBuilder.Build();
            return host;
        }

        private static void FillWorkerAssemblies(ICloudWorkerAppBuilder builder)
        {
            var blAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                            .Where(a => a.FullName != null && a.FullName.Contains(".Worker, "))
                            .ToList();

            blAssemblies.ForEach(assembly => builder.AddAutoMapperAssembly(assembly));
            blAssemblies.ForEach(assembly => builder.AddFluentValidationAssembly(assembly));
        }

        private static void BasicInitialize(IHostBuilder builder, string applicationName)
        {
            builder
                .UseWindowsService()
                .UseSerilog((context, services, config) =>
                {
                    config.ReadFrom.Configuration(context.Configuration);
                    config.Enrich.WithWorkerEnricher(applicationName);
                });
        }

        private static void InitializeServices(IHostBuilder builder, ICloudWorkerAppParameters workerAppParameters, ICustomsApplicationInfo info)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.AddRabbitMQConnection();
                services.AddRedisConnection();
                services.AddQueueConsumerService();
                services.AddQueueService();
                services.AddScoped<IHttpContextAccessor, WorkerHttpContextAccessor>();
                services.AddSingleton<ICustomsApplicationInfo>(info);
                services.AddAutoMapperProfiles(workerAppParameters.AutoMapperAssemblies);
                services.AddFluentValidators(workerAppParameters.FluentValidationAssemblies);
                if (workerAppParameters.ServiceConfigurationType != null)
                {
                    var instance = Activator.CreateInstance(workerAppParameters.ServiceConfigurationType) as IServicesConfiguration;
                    instance.RegisterServices(context.Configuration, services);
                }
            });
        }

        private static ICustomsApplicationInfo GetApplicationInfo(string environment, string applicationName)
        {
            const string production = "production";

            var info = new CustomsApplicationInfo
            {
                Application = applicationName,
                Environment = environment,
                IsProduction = string.Equals(environment, production, StringComparison.OrdinalIgnoreCase)
            };

            return info;
        }

        private static string GetApplicationName(Type baseType)
        {
            var parts = baseType.Namespace.Split('.');
            var application = parts.Length == 4 ? parts[2] : parts.Last();
            return application;
        }

        private static void InitializeConfiguration(IHostBuilder builder, string environment)
        {
            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                BuildConfig(config, environment);
            });
        }

        private static void BuildConfig(IConfigurationBuilder builder, string environment)
        {
            builder.AddConsulToWorker(environment);

            var file1 = Path.Combine("Settings", "appsettings.json");
            var file2 = Path.Combine("Settings", $"appsettings.{environment}.json");
            builder
                .AddJsonFile(file1, optional: false, reloadOnChange: true)
                .AddJsonFile(file2, optional: false, reloadOnChange: true);

            builder.AddEnvironmentVariables();
        }

        private static string GetEnvironment()
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (string.IsNullOrEmpty(environment))
            {
                environment = "Production";
            }

            return environment;
        }
    }
}