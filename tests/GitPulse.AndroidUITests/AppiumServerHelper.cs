using OpenQA.Selenium.Appium.Service;

namespace GitPulse.AndroidUITests;

/// <summary>
///   Starts a local Appium 2 server for Android Emulator UI Smoke when one is not
///   already provided (see docs/DEVELOPMENT.md).
/// </summary>
public static class AppiumServerHelper
{
    private static AppiumLocalService? _appiumLocalService;

    public const string DefaultHostAddress = "127.0.0.1";
    public const int DefaultHostPort = 4723;

    public static void StartAppiumLocalServer(
        string host = DefaultHostAddress,
        int port = DefaultHostPort)
    {
        if (_appiumLocalService is not null)
        {
            return;
        }

        // Prefer an already-running server (manual `appium` in another terminal).
        if (IsPortOpen(host, port))
        {
            return;
        }

        AppiumServiceBuilder builder = new AppiumServiceBuilder()
            .WithIPAddress(host)
            .UsingPort(port);

        _appiumLocalService = builder.Build();
        _appiumLocalService.Start();
    }

    public static void DisposeAppiumLocalServer()
    {
        _appiumLocalService?.Dispose();
        _appiumLocalService = null;
    }

    static bool IsPortOpen(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            IAsyncResult result = client.BeginConnect(host, port, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            if (!success)
            {
                return false;
            }

            client.EndConnect(result);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
