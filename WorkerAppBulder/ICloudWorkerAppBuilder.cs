using CustomsCloud.InfrastructureCore.Interfaces.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.Worker.CloudApp
{
    public interface ICloudWorkerAppBuilder
    {
        ICloudWorkerAppBuilder UseBaseType<T>() where T : class;

        ICloudWorkerAppBuilder WithArgs(string[] args);

        ICloudWorkerAppBuilder AddServiceConfiguration<TServiceConfigure>()
            where TServiceConfigure : IServicesConfiguration;

        ICloudWorkerAppBuilder SetApplicationName(string applicationName);

        ICloudWorkerAppBuilder AddAutoMapperAssembly<T>() where T : class;

        ICloudWorkerAppBuilder AddAutoMapperAssembly(Assembly assembly);

        ICloudWorkerAppBuilder AddFluentValidationAssembly<T>() where T : class;

        ICloudWorkerAppBuilder AddFluentValidationAssembly(Assembly assembly);

        ICloudWorkerAppParameters Build();
    }
}