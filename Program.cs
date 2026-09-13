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
    private static string? host_RunDurationHint()
    {
        string? v = Environment.GetEnvironmentVariable("STALKER_RUN_DURATION_SEC");
        return int.TryParse(v, out int s) && s > 0 ? $"auto-stop after {s}s" : null;
    }

    public static void Main(string[] args)
    {
        var settings = SimulationSettings.FromEnvironment();

        // Resolve the run mode BEFORE building anything, and say so out loud.
        //
        // This exists because a headless measurement run once silently became an
        // unbounded server run: STALKER_HEADLESS_TICKS failed to reach the
        // process, HeadlessTicks came back null, and execution fell through to
        // the timer with no stop condition. It ran for an hour before anyone
        // noticed, because nothing ever stated which mode it was in.
        string? requestedMode = Environment.GetEnvironmentVariable("STALKER_MODE");
        int? headlessTicks = SimulationHost.HeadlessTicksFromEnvironment();

        if (string.Equals(requestedMode, "headless", StringComparison.OrdinalIgnoreCase)
            && headlessTicks is null)
        {
            Console.Error.WriteLine(
                "[FATAL] STALKER_MODE=headless but STALKER_HEADLESS_TICKS is unset or not a " +
                "positive integer. Refusing to fall through to an unbounded server run.");
            Environment.ExitCode = 2;
            return;
        }

        Console.WriteLine(headlessTicks is int t
            ? $"[Mode] HEADLESS — {t} ticks, no timer, no web host"
            : "[Mode] SERVE — timer-driven, web host, runs until stopped" +
              (host_RunDurationHint() is string h ? $" ({h})" : " (no auto-stop configured)"));

        // Build the simulation (data load, world gen, entities, loop).
        var host = new SimulationHost(settings);

        // Headless measurement run: fixed tick count, no timer, no web host.
        // Two runs at the same tick count cover exactly the same span of game
        // time, so their counters can be diffed directly — which a timed run
        // cannot offer, since it drops a variable number of ticks under load.
        if (headlessTicks is int ticks)
        {
            host.RunHeadless(ticks);
            host.Stop();
            Console.WriteLine("[Mode] HEADLESS run complete");
            return;
        }

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

        // Enables the "/ws" WebSocket telemetry endpoint mapped in
        // WebApiEndpoints.MapSimulationApi. Must run before that mapping so the
        // upgrade request reaches AcceptWebSocketAsync.
        app.UseWebSockets();

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
