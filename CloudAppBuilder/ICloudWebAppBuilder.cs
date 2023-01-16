using CustomsCloud.InfrastructureCore.Interfaces.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.WebApi.CloudApp
{
    public interface ICloudWebAppBuilder
    {
        ICloudWebAppBuilder UseBaseType<T>() where T : class;

        ICloudWebAppBuilder WithArgs(string[] args);

        ICloudWebAppBuilder AddServiceConfiguration<TServiceConfigure>()
            where TServiceConfigure : IServicesConfiguration;

        ICloudWebAppBuilder MvcBuilderAction(Action<IMvcBuilder> mvcBuilder);

        ICloudWebAppBuilder SetApplicationName(string applicationName);

        ICloudWebAppBuilder AddAutoMapperAssembly<T>() where T : class;

        ICloudWebAppBuilder AddAutoMapperAssembly(Assembly assembly);

        ICloudWebAppBuilder AddFluentValidationAssembly<T>() where T : class;

        ICloudWebAppBuilder AddFluentValidationAssembly(Assembly assembly);

        ICloudWebAppParameters Build();
    }
}