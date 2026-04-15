# Kehlet.Scripting

Tiny shell helpers for C# scripts and small utilities.

* No dependencies
* Single-file friendly
* Works with `dotnet run app.cs`
* Also available as a package

---

## Install

### Option 1: Use as a package

```csharp
#:package Kehlet.Scripting
```

---

### Option 2: Copy-paste

Copy `Shell.cs` into your project.

That’s it.

---

## Usage

```csharp
using static Shell;

var result = await "echo hello".Run();

Console.WriteLine(result.StandardOutput);
```

Explicit shell:

```csharp
await "ls -la".Run(ShellKind.Bash);
await "git status".Run(ShellKind.Sh);
await "dir".Run(ShellKind.Cmd);
await "echo $env:USERPROFILE".Run(ShellKind.Pwsh);
```

---

## Platform defaults

`Run()` without specifying a shell uses:

* **Windows** → `pwsh`
* **Linux / macOS** → `/bin/sh`

```csharp
await "echo hi".Run(); // Uses platform default
```

---

## Available shells

```csharp
public enum ShellKind
{
    Sh,
    Bash,
    Cmd,
    Pwsh,
    WslBash,
    PlatformDefault,
}
```

Examples:

```csharp
await "echo hello".Run(ShellKind.Sh);
await "echo hello".Run(ShellKind.Bash);
await "echo hello".Run(ShellKind.Pwsh);
await "echo hello".Run(ShellKind.WslBash);
```

---

## Result

```csharp
public readonly record struct ProcessResult(
    string StandardOutput,
    string StandardError,
    int ExitCode
);
```

---

## Cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await "sleep 10".Run(cts.Token);
```

* Cancelling will attempt to kill the process and its entire process tree.

---

## Notes

* Commands are passed directly to the shell. No escaping or validation is performed.
* Command syntax must match the selected shell.
* Output is captured after the process exits (not streamed).
* Shell executables must exist on the system (`pwsh`, `/bin/sh`, `bash`, etc.).

---

## License

MIT
