using CustomsCloud.InfrastructureCore.WebApi.CloudApp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.WebApi
{
    internal class CloudWebAppParameters : ICloudWebAppParameters
    {
        public Type BaseType { get; set; }

        public string[] Args { get; set; }

        public Type ServiceConfigurationType { get; set; }

        public Action<IMvcBuilder> MvcBuilderAction { get; set; }

        public string ApplicationName { get; set; }

        public List<Assembly> AutoMapperAssemblies { get; private set; } = new();

        public List<Assembly> FluentValidationAssemblies { get; private set; } = new();
    }
}