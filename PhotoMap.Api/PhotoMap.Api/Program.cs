using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace PhotoMap.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ConfigureSerilog();

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("hostsettings.json", optional: true)
                .AddCommandLine(args)
                .Build();

            return Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseConfiguration(config)
                        .UseStartup<Startup>();
                });
        }

        private static void ConfigureSerilog()
        {
            Log.Logger = new LoggerConfiguration()
                // .MinimumLevel.Debug()
                // .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("/logs/photo-map-api.ndjson")
                .WriteTo.Seq("http://localhost:5341")
                .CreateLogger();
        }
    }
}
