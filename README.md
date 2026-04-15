# Kehlet.Scripting

Tiny shell helpers for C# scripts and small utilities.

* No dependencies
* Single file, copy-paste friendly
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

---

## Quick start

```csharp
using Kehlet.Scripting;

var result = await "echo hello".RunAsync();

Console.WriteLine(result.StandardOutput);
```

---

## Async / sync

```csharp
// Async (recommended)
var result = await "echo hello".RunAsync();

// Sync
var result = "echo hello".Run();
```

---

## Shell selection

```csharp
await "ls -la".RunAsync(ShellKind.Bash);
await "git status".RunAsync(ShellKind.Sh);
await "dir".RunAsync(ShellKind.Cmd);
await "echo $env:USERPROFILE".RunAsync(ShellKind.Pwsh);
await "ls -la".RunAsync(ShellKind.WslBash);
```

---

## Platform defaults

`Run()` / `RunAsync()` without specifying a shell uses:

* **Windows** → `pwsh`
* **Linux / macOS** → `/bin/sh`

```csharp
await "echo hi".RunAsync();
```

---

## Pattern matching

```csharp
if (await "git diff --quiet".RunAsync() is { ExitCode: 0 })
{
    Console.WriteLine("No changes");
}
```

---

## Deconstruction

```csharp
var (stdout, stderr, code) = await "echo hello".RunAsync();
```

---

## Cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await "sleep 10".RunAsync(cts.Token);
```

Cancelling will attempt to terminate the process and its entire process tree.

---

## Operator shorthand

```csharp
var result = await !"echo hello";
```

Equivalent to:

```csharp
await "echo hello".RunAsync();
```

---

## Result type

```csharp
public readonly record struct ProcessResult(
    string StandardOutput,
    string StandardError,
    int ExitCode
);
```

---

## Shells

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

---

## Notes

* Commands are passed directly to the shell (no escaping or validation).
* Command syntax must match the selected shell.
* Output is fully buffered and returned after the process exits (not streamed).
* Shell executables must exist on the system (`pwsh`, `/bin/sh`, `bash`, etc.).

---

## License

MIT
