using PalavraSecreta.Pages;
using PalavraSecreta.Services;

namespace PalavraSecreta;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // fontes padrão
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");

                // SUA fonte “cartaz”
                fonts.AddFont("PosterBlack.ttf", "PosterBlack");
            });

        builder.Services.AddSingleton<LocalWordBankService>();

        builder.Services.AddSingleton<GeneratorPage>();
        builder.Services.AddSingleton<GamePage>();

        return builder.Build();

    }
}
