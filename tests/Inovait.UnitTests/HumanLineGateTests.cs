using System.Diagnostics;

namespace Inovait.UnitTests;

public sealed class HumanLineGateTests
{
    [Theory]
    [InlineData("400\t0\tat-limit.cs\n", 0)]
    [InlineData("401\t0\tover-limit.cs\n", 1)]
    [InlineData("malformed\n", 2)]
    [InlineData("not-an-integer\t0\tinvalid.cs\n", 2)]
    [InlineData("-\t-\tbinary.dll\n", 2)]
    public async Task NumstatContract_ReturnsExpectedExitCode(string input, int expectedExitCode)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "python3",
                WorkingDirectory = FindRepositoryRoot(),
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("scripts/check-human-lines.py");

        Assert.True(process.Start());
        var cancellationToken = TestContext.Current.CancellationToken;
        await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
        process.StandardInput.Close();

        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(standardOutput, standardError);

        Assert.Equal(expectedExitCode, process.ExitCode);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Inovait.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
