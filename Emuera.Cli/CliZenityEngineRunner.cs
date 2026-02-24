using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.RuntimeCore.Bootstrap;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.RuntimeEngine;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

internal static class CliZenityEngineRunner
{
    private const string DialogTitle = "Emuera Linux GUI";
    private const int DialogMaxLines = 120;
    private const int DialogMaxChars = 18000;
    private const string ManualEntryValue = "__manual_input__";

    private static readonly Regex ButtonTokenRegex = new(
        "<(?<value>[^<>\\r\\n]{1,48})>",
        RegexOptions.Compiled);

    private static readonly Regex ButtonChoiceRegex = new(
        "(?<label>[^<>\\r\\n]{1,64})<(?<value>[^<>\\r\\n]{1,48})>",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceRegex = new(
        "\\s+",
        RegexOptions.Compiled);

    private static readonly Regex AnsiColorRegex = new(
        "\\u001b\\[[0-9;]*m",
        RegexOptions.Compiled);

    public static async Task<int> RunAsync(GameDataValidationResult validation, bool strict, int strictRetries)
    {
        if (validation == null)
            return 2;

        if (!IsZenityAvailable())
        {
            Console.Error.WriteLine("GUI mode requires 'zenity' on Linux. Falling back to terminal runtime mode.");
            return await CliEngineRunner.RunAsync(validation, strict, strictRetries);
        }

        if (!HasDisplayServer())
        {
            Console.Error.WriteLine("GUI mode requires DISPLAY/WAYLAND_DISPLAY. Falling back to terminal runtime mode.");
            return await CliEngineRunner.RunAsync(validation, strict, strictRetries);
        }

        var smoke = await LauncherUi.RunSmokeOnlyAsync(validation, strict, strictRetries);
        if (smoke != 0)
        {
            ShowErrorDialog("Launch blocked", $"Smoke checks failed (exit code {smoke}).");
            return smoke;
        }

        return await RunAfterSmokeAsync(validation);
    }

    private static async Task<int> RunAfterSmokeAsync(GameDataValidationResult validation)
    {
        try
        {
            RuntimeEngineBootstrap.InitializeForCli(validation.RootDir);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Runtime bootstrap failed", ex.Message);
            return 9;
        }

        var executionConsole = new CliExecutionConsole();
        CliRuntimeHostBridge.AttachExecutionConsole(executionConsole);

        IRuntimeProcess? runtimeProcess;
        try
        {
            runtimeProcess = RuntimeHost.CreateRuntimeProcess(executionConsole);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Runtime process failed", ex.Message);
            return 9;
        }

        if (runtimeProcess == null)
        {
            ShowErrorDialog("Runtime process failed", "Process factory returned null.");
            return 9;
        }

        executionConsole.AttachProcess(runtimeProcess);

        using var initLogStream = new MemoryStream();
        using var initLogWriter = new StreamWriter(initLogStream) { AutoFlush = true };

        bool initialized;
        try
        {
            initialized = await runtimeProcess.Initialize(initLogWriter);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Runtime initialization failed", ex.ToString());
            return 10;
        }

        if (!initialized)
        {
            ShowErrorDialog("Runtime initialization failed", "Runtime initialization did not complete.");
            return 10;
        }

        runtimeProcess.BeginTitle();

        while (true)
        {
            executionConsole.BeginRunning();
            runtimeProcess.DoScript();

            if (executionConsole.IsTerminated)
                return executionConsole.HasRuntimeError ? 11 : 0;

            if (!executionConsole.IsWaitingInput)
            {
                ShowErrorDialog("Runtime loop error", "Runtime loop paused without an input request.");
                return 12;
            }

            if (!TryHandleInput(executionConsole))
            {
                executionConsole.Quit();
                return 0;
            }
        }
    }

    private static bool TryHandleInput(CliExecutionConsole executionConsole)
    {
        var request = executionConsole.PendingRequest;
        if (request == null)
            return false;

        DateTimeOffset? timeoutDeadline = null;
        if (request.Timelimit > 0)
            timeoutDeadline = DateTimeOffset.UtcNow.AddMilliseconds(request.Timelimit);

        while (true)
        {
            RuntimeHost.RefreshStrings(false);
            var lines = executionConsole.GetOutputLinesSnapshot(includePendingLine: true);
            var remainingTimeoutMs = GetRemainingTimeoutMs(timeoutDeadline);
            var viewText = BuildDialogText(lines, request, remainingTimeoutMs);
            var result = request.NeedValue
                ? PromptValue(request, viewText, lines, remainingTimeoutMs)
                : PromptContinue(viewText, remainingTimeoutMs);

            if (result.Fatal)
            {
                Console.Error.WriteLine("GUI dialog backend failed. Ending current session.");
                return false;
            }

            if (result.TimedOut)
            {
                executionConsole.MarkTimedOut();
                EmitTimeoutMessage(executionConsole, request);
                timeoutDeadline = null;
                if (executionConsole.TrySubmitInput(string.Empty, timedOut: true))
                    return true;

                ShowErrorDialog("Input timeout", "Default timeout value was not accepted. Please enter a value.");
                continue;
            }

            if (result.Cancelled)
            {
                if (ConfirmExit())
                    return false;
                continue;
            }

            var rawInput = result.Value ?? string.Empty;
            if (executionConsole.TrySubmitInput(rawInput))
                return true;

            ShowErrorDialog("Invalid input", "Input value is not valid for the current request.");
        }
    }

    private static PromptResult PromptValue(InputRequest request, string dialogText, IReadOnlyList<string> lines, long timeoutMs)
    {
        var buttonChoices = ExtractButtonChoices(lines, request);
        if (buttonChoices.Count == 0)
            return PromptEntry(request, dialogText, timeoutMs);

        var selectResult = PromptButtonSelection(request, dialogText, buttonChoices, timeoutMs);
        if (!selectResult.Fatal &&
            !selectResult.Cancelled &&
            !selectResult.TimedOut &&
            string.Equals(selectResult.Value, ManualEntryValue, StringComparison.Ordinal))
        {
            return PromptEntry(request, dialogText, timeoutMs);
        }

        return selectResult;
    }

    private static PromptResult PromptButtonSelection(
        InputRequest request,
        string dialogText,
        IReadOnlyList<ButtonChoice> choices,
        long timeoutMs)
    {
        var args = new List<string>
        {
            "--list",
            "--radiolist",
            "--title", DialogTitle,
            "--ok-label", "Select",
            "--cancel-label", "Exit",
            "--width", "980",
            "--height", "760",
            "--text", $"{dialogText}\n\nClick option text to select input.",
            "--column", "Pick",
            "--column", "Option",
            "--column", "Input",
            "--print-column", "3",
        };

        var defaultValue = ResolveDefaultInput(request);
        var hasSelectedDefault = false;
        foreach (var choice in choices)
        {
            var select =
                !hasSelectedDefault &&
                defaultValue.Length > 0 &&
                string.Equals(choice.Value, defaultValue, StringComparison.OrdinalIgnoreCase);
            if (select)
                hasSelectedDefault = true;

            args.Add(select ? "TRUE" : "FALSE");
            args.Add(choice.Label);
            args.Add(choice.Value);
        }

        args.Add(hasSelectedDefault ? "FALSE" : "TRUE");
        args.Add("[manual input]");
        args.Add(ManualEntryValue);

        AddTimeoutArg(args, timeoutMs);
        var result = RunZenity(args);
        if (result.ExitCode == 0)
        {
            var value = result.Output.TrimEnd('\r', '\n').Trim();
            if (string.IsNullOrWhiteSpace(value))
                value = ManualEntryValue;
            return new PromptResult(false, false, false, value);
        }
        if (result.ExitCode == 5)
            return new PromptResult(false, true, false, string.Empty);
        if (result.ExitCode == 1)
            return new PromptResult(true, false, false, string.Empty);
        return new PromptResult(false, false, true, string.Empty);
    }

    private static PromptResult PromptContinue(string dialogText, long timeoutMs)
    {
        var args = new List<string>
        {
            "--question",
            "--title", DialogTitle,
            "--ok-label", "Continue",
            "--cancel-label", "Exit",
            "--width", "980",
            "--height", "760",
            "--text", dialogText,
        };
        AddTimeoutArg(args, timeoutMs);
        var result = RunZenity(args);
        if (result.ExitCode == 0)
            return new PromptResult(false, false, false, string.Empty);
        if (result.ExitCode == 5)
            return new PromptResult(false, true, false, string.Empty);
        if (result.ExitCode == 1)
            return new PromptResult(true, false, false, string.Empty);
        return new PromptResult(false, false, true, string.Empty);
    }

    private static PromptResult PromptEntry(InputRequest request, string dialogText, long timeoutMs)
    {
        var args = new List<string>
        {
            "--entry",
            "--title", DialogTitle,
            "--ok-label", "Submit",
            "--cancel-label", "Exit",
            "--width", "980",
            "--height", "760",
            "--text", dialogText,
        };

        var defaultText = ResolveDefaultInput(request);
        if (defaultText.Length > 0)
        {
            args.Add("--entry-text");
            args.Add(defaultText);
        }

        AddTimeoutArg(args, timeoutMs);
        var result = RunZenity(args);
        if (result.ExitCode == 0)
            return new PromptResult(false, false, false, result.Output.TrimEnd('\r', '\n'));
        if (result.ExitCode == 5)
            return new PromptResult(false, true, false, string.Empty);
        if (result.ExitCode == 1)
            return new PromptResult(true, false, false, string.Empty);
        return new PromptResult(false, false, true, string.Empty);
    }

    private static string BuildDialogText(IReadOnlyList<string> lines, InputRequest request, long timeoutMs)
    {
        var tailStart = Math.Max(0, lines.Count - DialogMaxLines);
        var builder = new StringBuilder();

        if (tailStart > 0)
            builder.AppendLine($"... ({tailStart} earlier lines hidden) ...");

        for (var i = tailStart; i < lines.Count; i++)
        {
            var line = StripAnsi(lines[i] ?? string.Empty);
            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.AppendLine($"Prompt: {BuildPrompt(request)}");
        if (request.HasDefValue)
            builder.AppendLine($"Default: {ResolveDefaultInput(request)}");
        if (timeoutMs > 0)
            builder.AppendLine($"Timeout: {Math.Ceiling(timeoutMs / 1000d)}s");

        var hints = ExtractButtonHints(lines);
        if (hints.Length > 0)
            builder.AppendLine($"Buttons: {hints}");

        var text = builder.ToString();
        if (text.Length <= DialogMaxChars)
            return text;

        return "...\n" + text[^DialogMaxChars..];
    }

    private static IReadOnlyList<ButtonChoice> ExtractButtonChoices(IReadOnlyList<string> lines, InputRequest request)
    {
        var choices = new List<ButtonChoice>();
        if (lines == null || lines.Count == 0)
            return choices;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var start = Math.Max(0, lines.Count - 30);
        for (var i = start; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            foreach (Match match in ButtonChoiceRegex.Matches(line))
            {
                var value = match.Groups["value"].Value.Trim();
                if (!IsChoiceValueCompatible(request, value))
                    continue;
                if (!seen.Add(value))
                    continue;

                var label = NormalizeChoiceLabel(match.Groups["label"].Value);
                if (label.Length == 0)
                    label = value;
                choices.Add(new ButtonChoice(label, value));
                if (choices.Count >= 48)
                    return choices;
            }
        }

        return choices;
    }

    private static bool IsChoiceValueCompatible(InputRequest request, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return request.InputType switch
        {
            InputType.IntValue or InputType.IntButton => long.TryParse(value, out _),
            InputType.StrValue or InputType.StrButton => true,
            InputType.AnyValue => true,
            _ => false,
        };
    }

    private static string NormalizeChoiceLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var collapsed = WhitespaceRegex.Replace(raw, " ").Trim();
        if (collapsed.Length <= 48)
            return collapsed;
        return collapsed[^48..];
    }

    private static string ExtractButtonHints(IReadOnlyList<string> lines)
    {
        if (lines == null || lines.Count == 0)
            return string.Empty;

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var start = Math.Max(0, lines.Count - 20);
        for (var i = start; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line))
                continue;

            foreach (Match match in ButtonTokenRegex.Matches(line))
            {
                var raw = match.Groups["value"].Value.Trim();
                if (raw.Length == 0 || raw.Contains(' ') || raw.Length > 16)
                    continue;
                if (!seen.Add(raw))
                    continue;
                values.Add(raw);
                if (values.Count >= 16)
                    return string.Join(", ", values);
            }
        }

        return values.Count == 0 ? string.Empty : string.Join(", ", values);
    }

    private static string ResolveDefaultInput(InputRequest request)
    {
        if (request == null || !request.HasDefValue)
            return string.Empty;

        return request.InputType switch
        {
            InputType.IntValue or InputType.IntButton => request.DefIntValue.ToString(),
            InputType.StrValue or InputType.StrButton => request.DefStrValue ?? string.Empty,
            InputType.AnyValue => string.IsNullOrEmpty(request.DefStrValue)
                ? request.DefIntValue.ToString()
                : request.DefStrValue,
            _ => string.Empty,
        };
    }

    private static bool ConfirmExit()
    {
        var result = RunZenity(new List<string>
        {
            "--question",
            "--title", DialogTitle,
            "--ok-label", "Exit",
            "--cancel-label", "Continue",
            "--text", "Close the current game session?"
        });
        return result.ExitCode == 0;
    }

    private static void ShowErrorDialog(string caption, string message)
    {
        var text = string.IsNullOrWhiteSpace(message) ? "(no details)" : message;
        var result = RunZenity(new List<string>
        {
            "--error",
            "--title", $"{DialogTitle} - {caption}",
            "--width", "760",
            "--text", text,
        });

        if (result.ExitCode == -1)
            Console.Error.WriteLine($"[{caption}] {text}");
    }

    private static string BuildPrompt(InputRequest request)
    {
        return request.InputType switch
        {
            InputType.EnterKey => "[ENTER]",
            InputType.AnyKey => "[ANYKEY]",
            InputType.Void => "[WAIT]",
            InputType.IntValue or InputType.IntButton => "INPUT(INT)",
            InputType.StrValue or InputType.StrButton => "INPUT(STR)",
            InputType.AnyValue => "INPUT(ANY)",
            InputType.PrimitiveMouseKey => "INPUT(PRIMITIVE)",
            _ => "INPUT",
        };
    }

    private static long GetRemainingTimeoutMs(DateTimeOffset? timeoutDeadline)
    {
        if (!timeoutDeadline.HasValue)
            return -1;

        var remaining = timeoutDeadline.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
            return 0;
        return (long)Math.Ceiling(remaining.TotalMilliseconds);
    }

    private static void EmitTimeoutMessage(CliExecutionConsole executionConsole, InputRequest request)
    {
        if (request.TimeUpMes == null)
            return;
        executionConsole.PrintSingleLine(request.TimeUpMes);
    }

    private static void AddTimeoutArg(List<string> args, long timeoutMs)
    {
        if (timeoutMs <= 0)
            return;

        var seconds = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000d));
        args.Add("--timeout");
        args.Add(seconds.ToString());
    }

    private static string StripAnsi(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return AnsiColorRegex.Replace(text, string.Empty);
    }

    private static bool IsZenityAvailable()
    {
        return !string.IsNullOrWhiteSpace(FindExecutableInPath("zenity"));
    }

    private static bool HasDisplayServer()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    private static string? FindExecutableInPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static ZenityResult RunZenity(List<string> arguments)
    {
        var zenity = FindExecutableInPath("zenity");
        if (string.IsNullOrWhiteSpace(zenity))
            return new ZenityResult(-1, string.Empty, "zenity not found");

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = zenity,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };

            foreach (var argument in arguments)
                process.StartInfo.ArgumentList.Add(argument);

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ZenityResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new ZenityResult(-1, string.Empty, ex.Message);
        }
    }

    private readonly record struct PromptResult(bool Cancelled, bool TimedOut, bool Fatal, string Value);
    private readonly record struct ZenityResult(int ExitCode, string Output, string Error);
    private readonly record struct ButtonChoice(string Label, string Value);
}
