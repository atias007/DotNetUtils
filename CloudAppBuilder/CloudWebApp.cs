using CustomsCloud.AspNetCore.HttpLogging;
using CustomsCloud.InfrastructureCore.BL;
using CustomsCloud.InfrastructureCore.Consul;
using CustomsCloud.InfrastructureCore.Interfaces.Customs;
using CustomsCloud.InfrastructureCore.Interfaces.DependencyInjection;
using CustomsCloud.InfrastructureCore.Logging;
using CustomsCloud.InfrastructureCore.Queue;
using CustomsCloud.InfrastructureCore.WebApi.CloudApp;
using CustomsCloud.InfrastructureCore.WebApi.Filters;
using CustomsCloud.InfrastructureCore.WebApi.JsonConvertor;
using CustomsCloud.InfrastructureCore.WebApi.Middlewares.Metadata;
using CustomsCloud.InfrastructureCore.WebApi.Middlewares.Monitor;
using CustomsCloud.InfrastructureCore.WebApi.Util;
using CustomsCloud.InfrastructureCore.WebApi.Versionion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace CustomsCloud.InfrastructureCore.WebApi
{
    public sealed class CloudWebApp
    {
        private static string _applicationName;

        public static string ApplicationName => _applicationName;

        public static ICloudWebAppBuilder CreateCloudWebAppBuilder()
        {
            return new CloudWebAppBuilder();
        }

        public static WebApplication Build(ICloudWebAppBuilder builder)
        {
            FillBLAssemblies(builder);

            var webAppParameters = builder.Build();
            _applicationName =
                string.IsNullOrEmpty(webAppParameters.ApplicationName) ?
                GetApplicationName(webAppParameters.BaseType) :
                webAppParameters.ApplicationName;

            var appBuilder = WebApplication.CreateBuilder(webAppParameters.Args);
            ConfigureWebHost(appBuilder.WebHost);
            BuildConsulConfiguration(appBuilder);
            ConfigureSerilog(appBuilder.Host, _applicationName);
            ConfigureServices(appBuilder, webAppParameters);
            var app = appBuilder.Build();
            ConfigureWebApplication(app, appBuilder.Environment);
            MapActions(app, appBuilder.Environment, webAppParameters);

            SelfLog.Enable(Console.Out);

            return app;
        }

        private static void FillBLAssemblies(ICloudWebAppBuilder builder)
        {
            var blAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                            .Where(a => a.FullName != null && a.FullName.Contains(".BL, "))
                            .ToList();

            blAssemblies.ForEach(assembly => builder.AddAutoMapperAssembly(assembly));
            blAssemblies.ForEach(assembly => builder.AddFluentValidationAssembly(assembly));
        }

        private static void BuildConsulConfiguration(WebApplicationBuilder builder)
        {
            builder.WebHost.ConfigureAppConfiguration(config =>
            {
                config.AddConsulToWebApi(builder.Environment.EnvironmentName);
            });
        }

        private static string GetApplicationName(Type type)
        {
            var parts = type.Namespace.Split('.');
            var application = parts.Length == 4 ? parts[2] : parts.Last();
            return application;
        }

        private static void ConfigureSerilog(ConfigureHostBuilder builder, string applicationName)
        {
            builder.UseSerilog((context, services, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
                config.Enrich.WithWebApiEnricher(services, applicationName);
                config.Filter.With(new WebApiLogFilter());
            });
        }

        private static void ConfigureWebHost(ConfigureWebHostBuilder builder)
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (string.Compare(env, "development", true) == 0)
            {
                var exist = Enum.TryParse<CustomsMicroServices>(_applicationName, true, out var microService);
                if (exist)
                {
                    var port = (int)microService;
                    builder.UseUrls($"http://localhost:{port}");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(String.Empty.PadLeft(80, '-'));
                    Console.WriteLine($"[x] could not found micro service port for {_applicationName}. load the default port 5000");
                    Console.WriteLine(String.Empty.PadLeft(80, '-'));
                    builder.UseUrls("http://localhost:5000");
                }
            }
        }

        private static void ConfigureServices(WebApplicationBuilder builder, ICloudWebAppParameters parameters)
        {
            var services = builder.Services;
            var mvcBuilder = services.AddControllers();
            if (parameters.MvcBuilderAction != null)
            {
                parameters.MvcBuilderAction(mvcBuilder);
            }

            services.AddMvc(options =>
            {
                options.Filters.Add<ValidateModelStateAttribute>();
                options.Filters.Add<HttpResponseExceptionFilter>();
            })
            .AddNewtonsoftJson(options =>
            {
                options.SerializerSettings.DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore;
                options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
                options.SerializerSettings.Converters.Add(new TimeSpanConverter());
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = BadRequestUtil.CreateCustomErrorResponse;
            });

            services.AddFluentAutoValidation();

            #region Versioning

            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ApiVersionReader = new HeaderApiVersionReader("X-API-Version");
            });

            services.AddVersionedApiExplorer(options => options.GroupNameFormat = "'v'VVV");
            services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

            #endregion Versioning

            services.AddSwaggerGen(options => options.OperationFilter<SwaggerDefaultValues>());

            services.AddFluentValidators(parameters.FluentValidationAssemblies);
            services.AddRedisConnection();
            services.AddRabbitMQConnection();
            services.AddQueueService();
            services.AddHttpContextAccessor();
            services.AddRequestMetadata();
            services.AddHealthChecks();

            if (parameters.ServiceConfigurationType != null)
            {
                var instance = Activator.CreateInstance(parameters.ServiceConfigurationType) as IServicesConfiguration;
                instance.RegisterServices(builder.Configuration, services);
            }

            var info = GetApplicationInfo(builder);
            services.AddSingleton<ICustomsApplicationInfo>(info);
            services.AddAutoMapperProfiles(parameters.AutoMapperAssemblies);
            services.AddHttpLogging(logging =>
            {
                logging.LoggingFields = HttpLoggingFields.RequestBody | HttpLoggingFields.ResponseBody;
                logging.RequestBodyLogLimit = 4096;
                logging.ResponseBodyLogLimit = 4096;
            });
        }

        private static ICustomsApplicationInfo GetApplicationInfo(WebApplicationBuilder builder)
        {
            var info = new CustomsApplicationInfo
            {
                Application = _applicationName,
                Environment = builder.Environment.EnvironmentName,
                IsProduction = builder.Environment.IsProduction()
            };

            return info;
        }

        private static void ConfigureWebApplication(WebApplication app, IWebHostEnvironment env)
        {
            ////if (!env.IsDevelopment())
            ////{
            ////    app.UseHttpsRedirection();
            ////}

            app.UseRouting();

            app
                .UseCustomsCloudHttpLogging()
                .UseMiddleware<MonitorMiddelware>()
                .UseMiddleware<MetadataMiddelware>();

            app.UseDeveloperExceptionPage();

            app.UseAuthorization();

            app.UseSerilogRequestLogging();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

                foreach (var description in provider.ApiVersionDescriptions)
                {
                    options.SwaggerEndpoint(
                        $"/swagger/{description.GroupName}/swagger.json",
                        description.GroupName.ToUpperInvariant());
                }
            });
        }

        private static void MapActions(WebApplication app, IWebHostEnvironment env, ICloudWebAppParameters webAppParameters)
        {
            app.MapHealthChecks("/health-check", new HealthCheckOptions
            {
                AllowCachingResponses = false,
                ResultStatusCodes =
                 {
                    [HealthStatus.Healthy] = StatusCodes.Status200OK,
                    [HealthStatus.Degraded] = StatusCodes.Status200OK,
                    [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
                }
            });

            app.MapGet("/", () => $"Welcome to {_applicationName} (Environment: {env.EnvironmentName})");
            var date = GetLinkerTime(webAppParameters.BaseType.Assembly);
            var version =
                date == DateTime.MinValue ?
                "version undefined" :
                date.ToString("yyyyMMdd_HHmmss", CultureInfo.CurrentCulture);
            app.MapGet("/version", () => version);
        }

        private static DateTime GetLinkerTime(Assembly assembly)
        {
            const string BuildVersionMetadataPrefix = "+build";
            const string dateFormat = "yyyy-MM-ddTHH:mm:ss:fffZ";

            var attribute = assembly
              .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            if (attribute?.InformationalVersion != null)
            {
                var value = attribute.InformationalVersion;
                var index = value.IndexOf(BuildVersionMetadataPrefix);
                if (index > 0)
                {
                    value = value[(index + BuildVersionMetadataPrefix.Length)..];

                    return DateTime.ParseExact(
                        value,
                      dateFormat,
                      CultureInfo.InvariantCulture);
                }
            }

            return default;
        }
    }
}