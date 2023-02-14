using CustomsCloud.InfrastructureCore.Interfaces.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.Worker.CloudApp
{
    internal class CloudWorkerAppBuilder : ICloudWorkerAppBuilder
    {
        private CloudWorkerAppParameters _app = new CloudWorkerAppParameters();

        public ICloudWorkerAppBuilder UseBaseType<T>()
            where T : class
        {
            _app.BaseType = typeof(T);
            return this;
        }

        public ICloudWorkerAppBuilder WithArgs(string[] args)
        {
            _app.Args = args;
            return this;
        }

        public ICloudWorkerAppBuilder AddServiceConfiguration<TServiceConfigure>()
            where TServiceConfigure : IServicesConfiguration
        {
            _app.ServiceConfigurationType = typeof(TServiceConfigure);
            return this;
        }

        public ICloudWorkerAppBuilder SetApplicationName(string applicationName)
        {
            _app.ApplicationName = applicationName;
            return this;
        }

        public ICloudWorkerAppBuilder AddAutoMapperAssembly<T>()
            where T : class
        {
            var type = typeof(T);
            var assembly = type.Assembly;
            return AddAutoMapperAssembly(assembly);
        }

        public ICloudWorkerAppBuilder AddFluentValidationAssembly<T>()
            where T : class
        {
            var type = typeof(T);
            var assembly = type.Assembly;
            return AddFluentValidationAssembly(assembly);
        }

        public ICloudWorkerAppBuilder AddAutoMapperAssembly(Assembly assembly)
        {
            _app.AutoMapperAssemblies.Add(assembly);
            return this;
        }

        public ICloudWorkerAppBuilder AddFluentValidationAssembly(Assembly assembly)
        {
            _app.FluentValidationAssemblies.Add(assembly);
            return this;
        }

        public ICloudWorkerAppParameters Build()
        {
            return _app;
        }
    }
}