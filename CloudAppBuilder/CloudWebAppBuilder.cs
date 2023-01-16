using CustomsCloud.InfrastructureCore.Interfaces.DependencyInjection;
using CustomsCloud.InfrastructureCore.WebApi.CloudApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.WebApi
{
    internal class CloudWebAppBuilder : ICloudWebAppBuilder
    {
        private CloudWebAppParameters _app = new CloudWebAppParameters();

        public ICloudWebAppBuilder UseBaseType<T>()
            where T : class
        {
            _app.BaseType = typeof(T);
            return this;
        }

        public ICloudWebAppBuilder WithArgs(string[] args)
        {
            _app.Args = args;
            return this;
        }

        public ICloudWebAppBuilder AddServiceConfiguration<TServiceConfigure>()
            where TServiceConfigure : IServicesConfiguration
        {
            _app.ServiceConfigurationType = typeof(TServiceConfigure);
            return this;
        }

        public ICloudWebAppBuilder MvcBuilderAction(Action<IMvcBuilder> mvcBuilder)
        {
            _app.MvcBuilderAction = mvcBuilder;
            return this;
        }

        public ICloudWebAppBuilder SetApplicationName(string applicationName)
        {
            _app.ApplicationName = applicationName;
            return this;
        }

        public ICloudWebAppBuilder AddAutoMapperAssembly<T>()
            where T : class
        {
            var type = typeof(T);
            var assembly = type.Assembly;
            return AddAutoMapperAssembly(assembly);
        }

        public ICloudWebAppBuilder AddFluentValidationAssembly<T>()
            where T : class
        {
            var type = typeof(T);
            var assembly = type.Assembly;
            return AddFluentValidationAssembly(assembly);
        }

        public ICloudWebAppBuilder AddAutoMapperAssembly(Assembly assembly)
        {
            _app.AutoMapperAssemblies.Add(assembly);
            return this;
        }

        public ICloudWebAppBuilder AddFluentValidationAssembly(Assembly assembly)
        {
            _app.FluentValidationAssemblies.Add(assembly);
            return this;
        }

        public ICloudWebAppParameters Build()
        {
            return _app;
        }
    }
}