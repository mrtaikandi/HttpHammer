using System.CommandLine;
using System.Text;
using HttpHammer.Console;
using HttpHammer.Console.Renderers;
using HttpHammer.Diagnostics;
using HttpHammer.Http;
using HttpHammer.Plan;
using HttpHammer.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Vertical.SpectreLogger;
using Vertical.SpectreLogger.Options;
using VariableHandler = HttpHammer.Processors.VariableHandler;

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
        var config = new CommandLineConfiguration(rootCommand) { EnableDefaultExceptionHandler = true };
        var exitCode = await config.InvokeAsync(args).ConfigureAwait(false);

        return exitCode;
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
        builder.Services.AddSingleton<IProcessorFactory, ProcessorFactory>();
        builder.Services.AddScoped<RequestProcessor>();
        builder.Services.AddScoped<DelayProcessor>();
        builder.Services.AddScoped<PromptProcessor>();

        builder.Services.AddTransient<IExecutionPlanLoader, ExecutionPlanLoader>();
        builder.Services.AddTransient<HammeringCommand>();

        return builder.Build();
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