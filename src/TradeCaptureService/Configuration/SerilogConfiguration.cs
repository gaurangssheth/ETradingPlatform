using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeCaptureService.Configuration
{
    public static class SerilogConfiguration
    {
        public static IHostBuilder UseSerilogConfiguration(this IHostBuilder hostBuilder)
        {
            return hostBuilder.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext();
            });
        }

        public static IConfigurationBuilder AddSerilogConfiguration(this IConfigurationBuilder configuration,
            IHostEnvironment environment)
        {
            configuration
                .AddJsonFile("serilog.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"serilog.{environment.EnvironmentName}.json",
                    optional: true, reloadOnChange: true);

            return configuration;
        }
    }
}
