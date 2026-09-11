using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StalkerALifeSandbox.Core;
using StalkerALifeSandbox.Web;

namespace StalkerALifeSandbox;

public class Program
{
    public static void Main(string[] args)
    {
        // Build and start the simulation (data load, world gen, entities, loop).
        var host = new SimulationHost();
        host.Start();

        // Host the read-only visualizer web API.
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddCors(options =>
            options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

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

        app.Run("http://localhost:5050");
    }
}
