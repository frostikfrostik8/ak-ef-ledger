using System.Diagnostics;
using System.IO;
using System.Text;
using AefAccount.Localization;

namespace AefAccount.Services;

public sealed record UpdateResult(bool Ok, int ExitCode, string Output, string Error, TimeSpan Duration);

public sealed class UpdateService
{
    private readonly string _repoRoot;
    private readonly string _dbPath;

    public UpdateService(string repoRoot, string dbPath)
    {
        _repoRoot = repoRoot;
        _dbPath = dbPath;
    }

    public async Task<UpdateResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.StartNew();
        var script = Path.Combine(_repoRoot, "scripts", "snapshot.py");
        if (!File.Exists(script))
        {
            return new UpdateResult(false, -1, "", L.T("update_script_missing", script), started.Elapsed);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            WorkingDirectory = _repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add(script);
        startInfo.ArgumentList.Add("--db");
        startInfo.ArgumentList.Add(_dbPath);
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return new UpdateResult(false, -1, "", L.T("update_python_failed"), started.Elapsed);
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = await errorTask;
            return new UpdateResult(process.ExitCode == 0, process.ExitCode, output, error, started.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return new UpdateResult(false, -1, "", L.T("update_cancelled"), started.Elapsed);
        }
        catch (Exception exception)
        {
            return new UpdateResult(false, -1, "", exception.Message, started.Elapsed);
        }
    }
}

