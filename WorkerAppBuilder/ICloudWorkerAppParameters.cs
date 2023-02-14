using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.Worker.CloudApp
{
    public interface ICloudWorkerAppParameters
    {
        Type BaseType { get; }

        string[] Args { get; }

        Type ServiceConfigurationType { get; }

        public string ApplicationName { get; set; }

        List<Assembly> AutoMapperAssemblies { get; }

        List<Assembly> FluentValidationAssemblies { get; }
    }
}