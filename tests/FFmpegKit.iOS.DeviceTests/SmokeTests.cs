using Ffmpegkit.Ios;
// This assembly's own root namespace is 'FFmpegKit', which would otherwise shadow the bound type.
using FFmpeg = Ffmpegkit.Ios.FFmpegKit;

namespace FFmpegKit.iOS.DeviceTests;

/// <summary>A single on-simulator check. Throws to fail.</summary>
/// <param name="Name">Human readable name, reported to stdout.</param>
/// <param name="Execute">Runs the check. Receives a writable working directory.</param>
public sealed record SmokeTest(string Name, Action<string> Execute);

/// <summary>
/// End-to-end checks that only mean anything on a real device or simulator: they load the native
/// FFmpeg libraries out of the packaged xcframeworks and run actual FFmpeg commands.
/// </summary>
public static class SmokeTests
{
    public static SmokeTest[] All =>
    [
        new("native library reports its build", ReportsItsBuild),
        new("ffmpeg -version succeeds", VersionCommandSucceeds),
        new("encodes raw frames to mp4", EncodesRawFramesToMp4),
        new("encodes from pre-split arguments", EncodesFromArguments),
        new("ffprobe reads back the encoded file", FFprobeReadsBackTheEncodedFile),
        new("failing command reports a non-success return code", FailingCommandIsReportedAsFailure),
        new("awaits an async execute", AsyncExecuteCompletes),
        new("awaits an async ffprobe", AsyncProbeReturnsMediaInformation),
        new("cancels a running command", CancellationStopsACommand),
        new("reports a completed session state", SessionStateIsCompleted),
        new("exposes enum-typed log level and helpers", ErgonomicHelpersWork),
        new("delivers log output to a delegate", LogDelegateReceivesOutput),
    ];

    private static void ReportsItsBuild(string workingDirectory)
    {
        // Reaching this at all proves the native frameworks linked and loaded.
        var packageName = Packages.PackageName;
        Assert(!string.IsNullOrWhiteSpace(packageName), "Packages.PackageName was empty.");

        var libraries = Packages.ExternalLibraries;
        Assert(libraries is { Length: > 0 }, "Packages.ExternalLibraries was empty.");

        Report($"package={packageName} externalLibraries={string.Join(",", libraries!)}");
    }

    private static void VersionCommandSucceeds(string workingDirectory)
    {
        var session = FFmpeg.Execute("-version");

        AssertSuccess(session, "-version");
        Assert(
            session.Output?.Contains("ffmpeg version", StringComparison.OrdinalIgnoreCase) == true,
            $"'-version' output did not look like FFmpeg: {session.Output}");
    }

    private static void EncodesRawFramesToMp4(string workingDirectory)
    {
        var input = Path.Combine(workingDirectory, "input.raw");
        var output = Path.Combine(workingDirectory, "output.mp4");

        WriteRawFrames(input);
        File.Delete(output);

        // rawvideo in, mpeg4 out: both are always present regardless of which FFmpegKit variant
        // is under test, and the scale filter exercises libavfilter/libswscale on the way through.
        var session = FFmpeg.Execute(BuildEncodeCommand(input, output));

        AssertSuccess(session, "encode");
        Assert(File.Exists(output), $"'{output}' was not produced.");

        var size = new FileInfo(output).Length;
        Assert(size > 0, $"'{output}' is empty.");
        Report($"encoded {size} bytes");
    }

    private static void EncodesFromArguments(string workingDirectory)
    {
        var input = Path.Combine(workingDirectory, "input.raw");
        var output = Path.Combine(workingDirectory, "arguments.mp4");
        WriteRawFrames(input);
        File.Delete(output);

        // The pre-split form takes an NSArray natively. If that marshalling is wrong the command
        // still "runs" but with mangled arguments, so this is checked separately from the
        // string form rather than assumed to follow from it.
        var session = FFmpeg.ExecuteWithArguments(
        [
            "-y", "-f", "rawvideo", "-pixel_format", "rgb24",
            "-video_size", $"{FrameWidth}x{FrameHeight}", "-framerate", "10",
            "-i", input, "-vf", "scale=64:64", "-c:v", "mpeg4", output,
        ]);

        AssertSuccess(session, "encode from arguments");
        Assert(File.Exists(output), $"'{output}' was not produced.");
    }

    private static void FFprobeReadsBackTheEncodedFile(string workingDirectory)
    {
        var output = Path.Combine(workingDirectory, "output.mp4");
        Assert(File.Exists(output), "The encode check must run before this one.");

        var session = FFprobeKit.GetMediaInformation(output);
        var information = session.MediaInformation;

        Assert(information is not null, "FFprobe returned no media information.");

        var streams = information!.Streams;
        Assert(streams is { Length: > 0 }, "FFprobe reported no streams.");

        var video = streams!.FirstOrDefault(s => s.Type == "video");
        Assert(video is not null, $"No video stream found. Streams: {string.Join(",", streams.Select(s => s.Type))}");
        Report($"probed codec={video!.Codec} format={information.Format}");
    }

    private static void FailingCommandIsReportedAsFailure(string workingDirectory)
    {
        // A binding that mis-marshals return codes would make every command look successful,
        // which would quietly defeat every other check here.
        var session = FFmpeg.Execute($"-i \"{Path.Combine(workingDirectory, "does-not-exist.mp4")}\" -f null -");

        Assert(
            session.ReturnCode is not null && !session.ReturnCode.IsValueSuccess,
            "FFmpeg reported success for a command that should have failed.");
    }

    private static void AsyncExecuteCompletes(string workingDirectory)
    {
        var input = Path.Combine(workingDirectory, "input.raw");
        var output = Path.Combine(workingDirectory, "async.mp4");
        WriteRawFrames(input);
        File.Delete(output);

        // Exercises the Task-based wrapper, which bridges FFmpegKit's completion callback back
        // across the binding. Blocking on the task is fine here - the point is the round trip.
        var session = FFmpeg.ExecuteAsync(BuildEncodeCommand(input, output)).GetAwaiter().GetResult();

        AssertSuccess(session, "async encode");
        Assert(File.Exists(output), $"'{output}' was not produced.");
    }

    private static void AsyncProbeReturnsMediaInformation(string workingDirectory)
    {
        var output = Path.Combine(workingDirectory, "async.mp4");
        Assert(File.Exists(output), "The async encode check must run before this one.");

        var session = FFprobeKit.GetMediaInformationAsync(output).GetAwaiter().GetResult();

        Assert(session.MediaInformation is not null, "Async FFprobe returned no media information.");
        Report($"async probe format={session.MediaInformation!.Format}");
    }

    private static void CancellationStopsACommand(string workingDirectory)
    {
        var input = Path.Combine(workingDirectory, "long.raw");
        var output = Path.Combine(workingDirectory, "cancelled.mp4");
        WriteRawFrames(input, frameCount: 4000);
        File.Delete(output);

        using var cancellation = new CancellationTokenSource();

        // Upscaling several thousand frames keeps FFmpeg busy long enough to cancel mid-run.
        var task = FFmpeg.ExecuteAsync(
            $"-y -f rawvideo -pixel_format rgb24 -video_size {FrameWidth}x{FrameHeight} " +
            $"-framerate 30 -i \"{input}\" -vf scale=1280:720 -c:v mpeg4 \"{output}\"",
            cancellation.Token);

        cancellation.CancelAfter(TimeSpan.FromMilliseconds(300));

        // Cancelling must complete the task rather than hang or throw...
        Assert(task.Wait(TimeSpan.FromSeconds(60)), "The cancelled command never completed.");

        // ...and must actually have stopped FFmpeg. Several thousand frames upscaled to 720p
        // cannot finish in 300ms on any device this runs on, so a success code here would mean
        // the token was ignored rather than that the work simply beat the timer.
        var returnCode = task.Result.ReturnCode;
        Assert(
            returnCode is not null && returnCode.IsValueCancel,
            $"Expected a cancelled return code, got {returnCode?.Value.ToString() ?? "<null>"}.");

        Report($"cancelled session returned {returnCode!.Value}");
    }

    private static void SessionStateIsCompleted(string workingDirectory)
    {
        var session = FFmpeg.Execute("-version");

        var state = session.State;
        Assert(state == SessionState.Completed, $"Expected a Completed session state, got {state}.");

        Report($"state={state} returnCode={session.ReturnCode?.Value}");
    }

    private static void ErgonomicHelpersWork(string workingDirectory)
    {
        var session = FFmpeg.Execute("-version");

        Assert(session.Succeeded(), "Succeeded() disagreed with the return code.");
        Assert(!session.Cancelled(), "Cancelled() was true for a command that completed.");

        // The point of the conversion: this is a switch, which the bound int could not express
        // without magic numbers.
        var previous = FFmpegKitConfig.GetLogLevel();
        try
        {
            FFmpegKitConfig.SetLogLevel(Level.Warning);
            var level = FFmpegKitConfig.GetLogLevel();

            Assert(level == Level.Warning, $"Expected Level.Warning, got {level}.");

            var described = level switch
            {
                Level.Warning => "warning",
                Level.Info => "info",
                _ => "other",
            };

            Assert(described == "warning", $"switch produced '{described}'.");
        }
        finally
        {
            FFmpegKitConfig.SetLogLevel(previous);
        }

        Report($"logLevel={FFmpegKitConfig.GetLogLevel()} ltsBuild={FFmpegKitConfig.IsLtsBuild}");
    }

    private static void LogDelegateReceivesOutput(string workingDirectory)
    {
        var lines = 0;

        Level? observed = null;
        FFmpegKitConfig.EnableLogCallback(log =>
        {
            observed ??= log.Severity();
            Interlocked.Increment(ref lines);
        });
        try
        {
            FFmpeg.Execute("-version");

            // Log callbacks arrive on FFmpegKit's own thread and can lag the Execute call that
            // produced them, so give them a moment rather than clearing the callback immediately
            // and racing the delivery.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Volatile.Read(ref lines) == 0 && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(50);
            }
        }
        finally
        {
            // Clearing the callback is what the native API expects a null here to mean; the
            // binding just does not model the parameter as nullable.
            FFmpegKitConfig.EnableLogCallback(null!);
        }

        Assert(Volatile.Read(ref lines) > 0, "The log delegate never fired.");
        Assert(observed is not null, "No log line yielded a severity.");
        Report($"received {Volatile.Read(ref lines)} log lines, first severity {observed}");
    }

    private static string BuildEncodeCommand(string input, string output) =>
        $"-y -f rawvideo -pixel_format rgb24 -video_size {FrameWidth}x{FrameHeight} " +
        $"-framerate 10 -i \"{input}\" -vf scale=64:64 -c:v mpeg4 \"{output}\"";

    private const int FrameWidth = 32;
    private const int FrameHeight = 32;
    private const int FrameCount = 10;

    /// <summary>Writes a handful of rgb24 frames so the encode test needs no bundled media.</summary>
    private static void WriteRawFrames(string path, int frameCount = FrameCount)
    {
        var frame = new byte[FrameWidth * FrameHeight * 3];
        using var stream = File.Create(path);

        for (var i = 0; i < frameCount; i++)
        {
            for (var pixel = 0; pixel < frame.Length; pixel += 3)
            {
                frame[pixel] = (byte)(i * 25);
                frame[pixel + 1] = (byte)(pixel % 256);
                frame[pixel + 2] = (byte)((pixel + i) % 256);
            }

            stream.Write(frame);
        }
    }

    private static void AssertSuccess(AbstractSession session, string what)
    {
        Assert(
            session.ReturnCode is not null && session.ReturnCode.IsValueSuccess,
            $"'{what}' failed with return code {session.ReturnCode?.Value.ToString() ?? "<null>"}. " +
            $"Logs:\n{session.AllLogsAsString}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new SmokeTestFailure(message);
        }
    }

    private static void Report(string message) => Reporter?.Invoke(message);

    /// <summary>Set by the app delegate so checks can surface detail to stdout.</summary>
    public static Action<string>? Reporter { get; set; }
}

public sealed class SmokeTestFailure(string message) : Exception(message);
