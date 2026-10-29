namespace Metanoia.IntegrationTests;

internal static class TestPaths
{
    public static string Root { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string Fixtures { get; } = Path.Combine(Root, "tests", "Fixtures");

    public static string Results { get; } = Path.Combine(Root, "artifacts", "test-results");
}
