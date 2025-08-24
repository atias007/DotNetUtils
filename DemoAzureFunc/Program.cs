using Microsoft.Extensions.Hosting;
using RepoDb;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .Build();

GlobalConfiguration.Setup().UseSqlServer();

await host.RunAsync();