# Debugging & Crash Report Guide

This guide outlines how to collect crash reports and memory dumps for .NET applications—including .NET Framework, .NET Core, and .NET 5+—on both Windows and Linux.

In the rare event that Zen is causing critical issues such as crashes or deadlocks, sharing these diagnostic files with us can significantly help in identifying and resolving the problem.

---

## 1. How Memory Dumps Help us Diagnose Issues

A memory dump is a snapshot of your application's memory at a specific moment. It contains threads, objects, stack traces, and more—essential information for deep diagnostics. These provide crucial insight into the problem and will help us resolve the issue.
> [!WARNING]
> Dumps may contain sensitive data (like passwords or personal info), so treat them carefully and follow your company's security policies.

---

## 2. Collecting Memory Dumps

### Windows

#### A. Task Manager (Any .NET App)
1. Open Task Manager (`Ctrl+Shift+Esc`).
2. Find your app’s process.
3. Right-click → **Create Dump File**.
4. Note the dump file location (usually `%TEMP%`).

#### B. ProcDump (Sysinternals)
1. Download [ProcDump](https://docs.microsoft.com/en-us/sysinternals/downloads/procdump).
2. Run in Command Prompt:
   ```cmd
   procdump -ma <PID> <output.dmp>
   ```
   - `<PID>` is your process ID (see Task Manager).
   - `-ma` creates a full memory dump.

#### C. dotnet-dump (For .NET Core/5+)
1. Install:
   ```sh
   dotnet tool install --global dotnet-dump
   ```
2. List processes:
   ```sh
   dotnet-dump ps
   ```
3. Collect dump:
   ```sh
   dotnet-dump collect -p <PID> --type Full -o <output_path>
   ```

#### D. Visual Studio
1. Attach to process (Debug > Attach to Process).
2. Debug > Save Dump As.

---

### Linux

#### A. dotnet-dump (Recommended)
1. Install:
   ```sh
   dotnet tool install --global dotnet-dump
   ```
2. Find PID:
   ```sh
   ps aux | grep dotnet
   ```
3. Collect dump:
   ```sh
   dotnet-dump collect -p <PID> --type Full -o <output_file>
   ```

#### B. dotnet-monitor (Advanced/Cloud/Containers)
- Allows automated or remote dump collection. See [dotnet-monitor docs](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dumps).

---

## 3. Automated Dump Collection (On Crash)

### Windows
- Use **Windows Error Reporting (WER)** or registry keys to auto-capture memory dumps for .NET Framework.
- See: [Microsoft Crash Dump Guide](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dumps)

### Linux
- Set environment variable `DOTNET_DUMP_PATH`.
- Configure memory dump settings in systemd or environment.

---

## 4. Submitting Your Crash Report

1. **Compress** the dump file (ZIP recommended). Note that the dump files are usually very large, but have a high compression rate.
2. Include the following info:
   - Application name & version
   - .NET Runtime version (`dotnet --info`)
   - Operating system
   - Steps to reproduce the issue
   - Any relevant logs
3. **Send securely** to customer service at support@aikido.dev with a link to the memory dump.

---

## 5. Analyzing Memory Dumps Yourself

If you want to perform the analysis yourself:
- Analyze memory dumps with Visual Studio, WinDbg, JetBrains dotMemory, or `dotnet-dump analyze`.
- On Linux, you can transfer the memory dump to Windows for advanced tools.

**Further Reading:**
- [Debug Deadlock (Microsoft Guide)](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-deadlock?tabs=windows)
- [Dumps - .NET | Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dumps)
- [dotnet-dump diagnostic tool](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-dump)
- [How to create Memory Dumps for .NET in Linux (dev.to)](https://dev.to/ernitingarg/how-to-create-and-analyze-memory-dumps-for-dotnet-applications-in-linux-3o8m)
- [Practical WinDbg Guide (GitHub)](https://github.com/bulentkazanci/Cheat-Sheet-Windbg/)
- [dotnet-monitor (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-monitor)

---

Need help? Contact us at [support@aikido.dev](mailto:support@aikido.dev).
