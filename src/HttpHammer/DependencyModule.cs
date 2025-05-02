// using HttpHammer.Console;
// using HttpHammer.Diagnostics;
// using HttpHammer.Http;
// using HttpHammer.Processors;
// using HttpHammer.Processors.Policies;
// using HttpHammer.Utilities;
// using Hutmesh.Host;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.DependencyInjection.Extensions;
// using Spectre.Console;
// using VariableHandler = HttpHammer.Utilities.VariableHandler;
//
// namespace HttpHammer;
//
// public class DependencyModule : IDependencyModule
// {
//     public void ConfigureServices(DependencyModuleContext context, IServiceCollection services)
//     {
//             services.AddSingleton(BuildAnsiConsole);
//
//             services
//                 .AddTransient<HttpRequestProfilingHandler>()
//                 .AddHttpClient()
//                 .ConfigureHttpClientDefaults(builder =>
//                 {
//                     builder.AddHttpMessageHandler<HttpRequestProfilingHandler>();
//                 });
//
//             // Shared services
//             services.AddSingleton<IVariableHandler, VariableHandler>();
//             services.AddSingleton<IHttpRequestBuilder, HttpRequestBuilder>();
//             services.AddSingleton<IProfiler, Profiler>();
//             services.AddSingleton<IProgressTracker, ProgressTracker>();
//
//             // Processors
//             services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessor, WarmupProcessor>());
//             services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessor, RequestProcessor>());
//             services.TryAddEnumerable(ServiceDescriptor.Singleton<IProcessor, ExecutionPlanFileProcessor>());
//
//             // Policies
//             services.AddSingleton<SynchronousExecutionPolicy>();
//             services.AddSingleton<ConcurrentExecutionPolicy>();
//             services.AddSingleton<IExecutionPolicyFactory, ExecutionPolicyFactory>();
//     }
//
//     private static IAnsiConsole BuildAnsiConsole(IServiceProvider serviceProvider)
//     {
//         var settings = new AnsiConsoleSettings
//         {
//             Ansi = AnsiSupport.Detect,
//             Interactive = InteractionSupport.Detect,
//             ColorSystem = ColorSystemSupport.Detect
//         };
//
//         var ansiConsole = AnsiConsole.Create(settings);
//         return ansiConsole;
//     }
// }