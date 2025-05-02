using Spectre.Console;

namespace HttpHammer.Console.Renderers;

internal static class SplashScreen
{
    public static void ShowSplashScreen(this IAnsiConsole console) => console.Write(
        new FigletText("HTTP Hammer")
            .LeftJustified()
            .Color(Color.Purple));
}