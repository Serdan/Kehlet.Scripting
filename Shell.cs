using System.Diagnostics;

namespace Kehlet.Scripting;

/// <summary>
/// Represents the output of a completed process execution.
/// </summary>
/// <param name="StandardOutput">
/// The text captured from the process standard output stream.
/// </param>
/// <param name="StandardError">
/// The text captured from the process standard error stream.
/// </param>
/// <param name="ExitCode">
/// The process exit code returned when execution completed.
/// </param>
public readonly record struct ProcessResult(string StandardOutput, string StandardError, int ExitCode)
{
    public bool Success => ExitCode is 0;
}

/// <summary>
/// Specifies which shell is used to execute a command.
/// </summary>
/// <remarks>
/// <see cref="PlatformDefault"/> selects a shell based on the current operating system.
/// </remarks>
public enum ShellKind
{
    /// <summary>
    /// Uses <c>/bin/sh</c>.
    /// </summary>
    Sh,

    /// <summary>
    /// Uses <c>/bin/bash</c>.
    /// </summary>
    Bash,

    /// <summary>
    /// Uses <c>cmd.exe</c>.
    /// </summary>
    Cmd,

    /// <summary>
    /// Uses <c>pwsh</c>.
    /// </summary>
    Pwsh,

    /// <summary>
    /// Uses <c>wsl.exe bash</c>.
    /// </summary>
    WslBash,

    /// <summary>
    /// Uses the default shell for the current platform.
    /// </summary>
    /// <remarks>
    /// This resolves to <c>pwsh</c> on Windows and <c>/bin/sh</c> on Linux and macOS.
    /// </remarks>
    PlatformDefault,
}

/// <summary>
/// Provides helpers for running shell commands and configured processes.
/// </summary>
/// <remarks>
/// Commands are passed directly to the selected shell without escaping or validation.
/// The caller is responsible for ensuring that command text is valid for the selected shell.
/// </remarks>
public static class Shell
{
    private static ProcessStartInfo CreateStartInfo(string fileName, params string[] arguments) =>
        new(fileName, arguments.ToArray())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

    private static ProcessStartInfo CreateUnixShellStartInfo(string command) => CreateStartInfo("/bin/sh", "-c", command);
    private static ProcessStartInfo CreateUnixBashStartInfo(string command) => CreateStartInfo("/bin/bash", "-lc", command);
    private static ProcessStartInfo CreateWindowsCmdStartInfo(string command) => CreateStartInfo("cmd.exe", "/C", command);
    private static ProcessStartInfo CreateWindowsPwshStartInfo(string command) => CreateStartInfo("pwsh", "-NoProfile", "-NonInteractive", "-Command", command);
    private static ProcessStartInfo CreateWslStartInfo(string command) => CreateStartInfo("wsl.exe", "bash", "-lc", command);

    private static ProcessStartInfo CreateShellStartInfo(string command, ShellKind kind) =>
        kind switch
        {
            ShellKind.Sh => CreateUnixShellStartInfo(command),
            ShellKind.Bash => CreateUnixBashStartInfo(command),
            ShellKind.Pwsh => CreateWindowsPwshStartInfo(command),
            ShellKind.Cmd => CreateWindowsCmdStartInfo(command),
            ShellKind.WslBash => CreateWslStartInfo(command),
            ShellKind.PlatformDefault =>
                OperatingSystem.IsWindows() ? CreateWindowsPwshStartInfo(command) :
                OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? CreateUnixShellStartInfo(command) :
                throw new PlatformNotSupportedException(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    /// <summary>
    /// Executes a configured process asynchronously.
    /// </summary>
    /// <param name="info">
    /// The <see cref="ProcessStartInfo"/> describing the process to run.
    /// </param>
    /// <param name="token">
    /// A cancellation token that, when triggered, attempts to terminate the process and its entire process tree.
    /// </param>
    /// <returns>
    /// A task that completes with a <see cref="ProcessResult"/> containing the captured standard output,
    /// standard error, and exit code.
    /// </returns>
    /// <remarks>
    /// Output is fully buffered and returned after the process exits. This method does not stream output incrementally.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the process fails to start.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if <paramref name="token"/> is canceled.
    /// </exception>
    public static async Task<ProcessResult> RunAsync(ProcessStartInfo info, CancellationToken token = default)
    {
        using var process = new Process { StartInfo = info };
        using var registration = token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        });

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start process");
        }

        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        var processWait = process.WaitForExitAsync(token);

        await Task.WhenAll(stdout, stderr, processWait);

        return new ProcessResult(await stdout, await stderr, process.ExitCode);
    }

    /// <summary>
    /// Executes a configured process synchronously.
    /// </summary>
    /// <param name="info">
    /// The <see cref="ProcessStartInfo"/> describing the process to run.
    /// </param>
    /// <param name="token">
    /// A cancellation token that, when triggered, attempts to terminate the process and its entire process tree.
    /// </param>
    /// <returns>
    /// A <see cref="ProcessResult"/> containing the captured standard output, standard error, and exit code.
    /// </returns>
    /// <remarks>
    /// Output is fully buffered and returned after the process exits. This method blocks the calling thread until the
    /// process completes or cancellation is requested.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the process fails to start.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if <paramref name="token"/> is canceled.
    /// </exception>
    public static ProcessResult Run(ProcessStartInfo info, CancellationToken token = default)
    {
        using var process = new Process { StartInfo = info };

        using var registration = token.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }
        });

        if (!process.Start())
            throw new InvalidOperationException("Failed to start process");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        return new ProcessResult(stdout, stderr, process.ExitCode);
    }

    extension(string command)
    {
        /// <summary>
        /// Executes the current string as a shell command asynchronously using the specified shell.
        /// </summary>
        /// <param name="kind">
        /// The shell used to execute the command.
        /// </param>
        /// <param name="token">
        /// A cancellation token that, when triggered, attempts to terminate the process and its entire process tree.
        /// </param>
        /// <returns>
        /// A task that completes with a <see cref="ProcessResult"/> containing the captured standard output,
        /// standard error, and exit code.
        /// </returns>
        /// <remarks>
        /// The command string is passed directly to the selected shell without escaping or validation.
        /// The caller is responsible for ensuring the command is valid for the chosen shell.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the process fails to start.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="kind"/> is not a valid <see cref="ShellKind"/>.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if <see cref="ShellKind.PlatformDefault"/> is used on an unsupported operating system.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="token"/> is canceled.
        /// </exception>
        public Task<ProcessResult> RunAsync(ShellKind kind, CancellationToken token = default) => RunAsync(CreateShellStartInfo(command, kind), token);

        /// <summary>
        /// Executes the current string as a shell command asynchronously using the platform default shell.
        /// </summary>
        /// <param name="token">
        /// A cancellation token that, when triggered, attempts to terminate the process and its entire process tree.
        /// </param>
        /// <returns>
        /// A task that completes with a <see cref="ProcessResult"/> containing the captured standard output,
        /// standard error, and exit code.
        /// </returns>
        /// <remarks>
        /// The platform default shell is <c>pwsh</c> on Windows and <c>/bin/sh</c> on Linux and macOS.
        /// The command string is passed directly to the underlying shell, so its syntax must match the selected shell.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the process fails to start.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if the current operating system is not supported.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="token"/> is canceled.
        /// </exception>
        public Task<ProcessResult> RunAsync(CancellationToken token = default) => command.RunAsync(ShellKind.PlatformDefault, token);

        /// <summary>
        /// Executes the current string as a shell command synchronously using the platform default shell.
        /// </summary>
        /// <param name="token">
        /// A cancellation token that, when triggered, attempts to terminate the process and its entire process tree.
        /// </param>
        /// <returns>
        /// A <see cref="ProcessResult"/> containing the captured standard output, standard error, and exit code.
        /// </returns>
        /// <remarks>
        /// The platform default shell is <c>pwsh</c> on Windows and <c>/bin/sh</c> on Linux and macOS.
        /// The command string is passed directly to the underlying shell, so its syntax must match the selected shell.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the process fails to start.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if the current operating system is not supported.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="token"/> is canceled.
        /// </exception>
        public ProcessResult Run(CancellationToken token = default) => Run(CreateShellStartInfo(command, ShellKind.PlatformDefault), token);

        /// <summary>
        /// Executes the current string as a shell command synchronously using the specified shell.
        /// </summary>
        /// <param name="kind">
        /// The shell used to execute the command.
        /// </param>
        /// <param name="token">
        /// A cancellation token that, when triggered, attempts to terminate the process and its entire process tree.
        /// </param>
        /// <returns>
        /// A <see cref="ProcessResult"/> containing the captured standard output, standard error, and exit code.
        /// </returns>
        /// <remarks>
        /// The command string is passed directly to the selected shell without escaping or validation.
        /// The caller is responsible for ensuring the command is valid for the chosen shell.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the process fails to start.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="kind"/> is not a valid <see cref="ShellKind"/>.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if <see cref="ShellKind.PlatformDefault"/> is used on an unsupported operating system.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown if <paramref name="token"/> is canceled.
        /// </exception>
        public ProcessResult Run(ShellKind kind, CancellationToken token = default) => Run(CreateShellStartInfo(command, kind), token);

        /// <summary>
        /// Executes the current string as a shell command asynchronously using the platform default shell.
        /// </summary>
        /// <param name="cmd">
        /// The shell command to execute.
        /// </param>
        /// <returns>
        /// A task that completes with a <see cref="ProcessResult"/> containing the captured standard output,
        /// standard error, and exit code.
        /// </returns>
        /// <remarks>
        /// This operator is shorthand for calling <see cref="RunAsync(CancellationToken)"/> with the platform default shell.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the process fails to start.
        /// </exception>
        /// <exception cref="PlatformNotSupportedException">
        /// Thrown if the current operating system is not supported.
        /// </exception>
        public static Task<ProcessResult> operator !(string cmd) => cmd.RunAsync();
    }
}