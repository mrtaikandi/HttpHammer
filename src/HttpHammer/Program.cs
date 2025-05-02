using System.CommandLine;
using System.Text;
using HttpHammer.Console;
using HttpHammer.Console.Renderers;
using HttpHammer.Diagnostics;
using HttpHammer.Http;
using HttpHammer.Processors;
using HttpHammer.Processors.Policies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Vertical.SpectreLogger;
using Vertical.SpectreLogger.Options;
using VariableHandler = HttpHammer.Internals.VariableHandler;

namespace HttpHammer;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        System.Console.OutputEncoding = Encoding.UTF8;
        AnsiConsole.Console.ShowSplashScreen();

        using var app = BuildApplication(args);
        await app.StartAsync().ConfigureAwait(false);

        var rootCommand = app.Services.GetRequiredService<HammeringCommand>();
        var config = new CommandLineConfiguration(rootCommand){ EnableDefaultExceptionHandler = true };
        var exitCode = await config.InvokeAsync(args).ConfigureAwait(false);

        return exitCode;
    }

    private static IHost BuildApplication(string[] args)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(BuildAnsiConsole);
        ConfigureLogging(builder.Logging, args);

        builder.Services
            .AddTransient<HttpRequestProfilingHandler>()
            .AddHttpClient()
            .ConfigureHttpClientDefaults(b => b.AddHttpMessageHandler<HttpRequestProfilingHandler>());

        // Shared services
        builder.Services.AddSingleton<IVariableHandler, VariableHandler>();
        builder.Services.AddSingleton<IHttpRequestBuilder, HttpRequestBuilder>();
        builder.Services.AddSingleton<IProfiler, Profiler>();
        builder.Services.AddSingleton<IProgressTracker, ProgressTracker>();

        // Processors
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessor, WarmupProcessor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessor, RequestProcessor>());
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessor, ExecutionPlanFileProcessor>());

        // Policies
        builder.Services.AddSingleton<SynchronousExecutionPolicy>();
        builder.Services.AddSingleton<ConcurrentExecutionPolicy>();
        builder.Services.AddSingleton<IExecutionPolicyFactory, ExecutionPolicyFactory>();

        // Commands
        builder.Services.AddTransient<HammeringCommand>();

        return builder.Build();
    }

    private static IAnsiConsole BuildAnsiConsole(IServiceProvider serviceProvider)
    {
        var settings = new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            Interactive = InteractionSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect
        };

        var ansiConsole = AnsiConsole.Create(settings);
        return ansiConsole;
    }

    private static void ConfigureLogging(ILoggingBuilder builder, string[] args)
    {
        builder.ClearProviders();
        var debugMode = args.Any(a => a is "--debug" or "-d");
        if (!debugMode)
        {
            return;
        }

        var isVerbose = args.Any(a => a is "--verbose" or "-v");
        var minLevel = isVerbose ? LogLevel.Debug : LogLevel.Information;

        builder.SetMinimumLevel(minLevel);
        builder.AddSpectreConsole();
        builder.Services.Configure<SpectreLoggerOptions>(o => o.MinimumLogLevel = minLevel);
    }
}