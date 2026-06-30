using GitPulse.Core.Abstractions;
using GitPulse.GitHubApi;
using Microsoft.Extensions.Logging;
using R3;
using R3.Maui;

namespace GitPulse.App;

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
			})
			.UseR3();

		// Application services (expanded per milestone).
		// ICredentialStore is registered per-platform in platform entry points.
		builder.Services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
