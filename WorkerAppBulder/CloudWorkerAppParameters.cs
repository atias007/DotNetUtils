using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.Worker.CloudApp
{
    internal class CloudWorkerAppParameters : ICloudWorkerAppParameters
    {
        public Type BaseType { get; set; }

        public string[] Args { get; set; }

        public Type ServiceConfigurationType { get; set; }

        public string ApplicationName { get; set; }

        public List<Assembly> AutoMapperAssemblies { get; private set; } = new();

        public List<Assembly> FluentValidationAssemblies { get; private set; } = new();
    }
}