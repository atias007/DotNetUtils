using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CustomsCloud.InfrastructureCore.WebApi.CloudApp
{
    public interface ICloudWebAppParameters
    {
        Type BaseType { get; }

        string[] Args { get; }

        Type ServiceConfigurationType { get; }

        Action<IMvcBuilder> MvcBuilderAction { get; }

        string ApplicationName { get; }

        List<Assembly> AutoMapperAssemblies { get; }

        List<Assembly> FluentValidationAssemblies { get; }
    }
}