using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Web;

namespace StalkerALifeSandbox;

public class Program
{
    public static void Main(string[] args)
    {
        var settings = SimulationSettings.FromEnvironment();

        // Build and start the simulation (data load, world gen, entities, loop).
        var host = new SimulationHost(settings);
        host.Start();

        // Host the read-only visualizer web API.
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(p => p
                .WithOrigins(settings.CorsOrigins.ToArray())
                .AllowAnyHeader()
                .AllowAnyMethod()));

        var app = builder.Build();
        app.UseCors();

        // Serve static files from the visualizer directory (app.js, icons, etc.).
        var staticPath = Path.Combine(Directory.GetCurrentDirectory(), "visualizer");
        if (Directory.Exists(staticPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(staticPath),
                RequestPath = ""
            });
        }

        app.MapSimulationApi(host);

        // Graceful shutdown: stop the sim loop and WebSocket server when the host stops.
        app.Lifetime.ApplicationStopping.Register(host.Stop);

        // Optional auto-stop: request a graceful shutdown after the configured window
        // instead of killing the process with Environment.Exit.
        if (host.RunDurationSeconds is int runSec)
        {
            System.Console.WriteLine($"[Debug] Auto-stop scheduled in {runSec}s (STALKER_RUN_DURATION_SEC)");
            _ = Task.Run(async () =>
            {
                await Task.Delay(runSec * 1000);
                System.Console.WriteLine("[Debug] Run duration reached — shutting down gracefully.");
                app.Lifetime.StopApplication();
            });
        }

        app.Run(settings.RestUrl);
    }
}
