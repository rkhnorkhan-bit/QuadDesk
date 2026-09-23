using System.Reflection;

namespace QuadDesk;

internal static class AppInfo
{
    public static string Version
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version is null) return "dev";
            return version.Revision == 0
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : version.ToString();
        }
    }

    public static string DisplayName => $"QuadDesk {Version}";
}
