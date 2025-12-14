using DrawSound.Services;
using Microsoft.Extensions.Logging;

namespace DrawSound;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        // Options
        builder.Services.Configure<AudioSettings>(builder.Configuration.GetSection("Audio"));

        // Register services
        builder.Services.AddSingleton<ITonePlayer, TonePlayer>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
