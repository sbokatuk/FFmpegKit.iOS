using System.Globalization;

using CommunityToolkit.Maui.Views;
using Ffmpegkit.Ios;

namespace FFmpegKitExample;

public partial class MainPage : ContentPage
{
	const string SampleAssetName = "sample.mp4";
	string _sourcePath = string.Empty;
	CancellationTokenSource? _cancellation;
	double _sourceDurationMs;

	public MainPage()
	{
		InitializeComponent();
		Loaded += OnPageLoaded;
	}

	async void OnPageLoaded(object? sender, EventArgs e)
	{
		Loaded -= OnPageLoaded;
		StatusLabel.Text = "Preparing source video…";
		_sourcePath = await CopySampleToCacheAsync();
		SourcePlayer.Source = MediaSource.FromFile(_sourcePath);

		// Probed for its duration so the statistics callback below can be shown as a percentage.
		// FFprobe reports seconds as a string; a failure here only costs the progress bar.
		var probe = await FFprobeKit.GetMediaInformationAsync(_sourcePath);
		if (double.TryParse(probe.MediaInformation?.Duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
			_sourceDurationMs = seconds * 1000;

		StatusLabel.Text = "Ready. Choose an operation.";
	}

	static async Task<string> CopySampleToCacheAsync()
	{
		var targetPath = Path.Combine(FileSystem.CacheDirectory, SampleAssetName);
		if (!File.Exists(targetPath))
		{
			using var input = await FileSystem.OpenAppPackageFileAsync(SampleAssetName);
			using var output = File.Create(targetPath);
			await input.CopyToAsync(output);
		}
		return targetPath;
	}

	async void OnResizeClicked(object sender, EventArgs e) =>
		await RunTransformAsync(
			"Resize (960x540 → 480px wide)",
			output => $"-y -i \"{_sourcePath}\" -vf scale=480:-2 \"{output}\"",
			"result_resized.mp4");

	async void OnGrayscaleClicked(object sender, EventArgs e) =>
		await RunTransformAsync(
			"Grayscale",
			output => $"-y -i \"{_sourcePath}\" -vf hue=s=0 \"{output}\"",
			"result_grayscale.mp4");

	async void OnExtractAudioClicked(object sender, EventArgs e) =>
		await RunTransformAsync(
			"Extract audio track",
			output => $"-y -i \"{_sourcePath}\" -vn -acodec copy \"{output}\"",
			"result_audio.m4a");

	async Task RunTransformAsync(string operationTitle, Func<string, string> buildCommand, string outputFileName)
	{
		if (string.IsNullOrEmpty(_sourcePath))
		{
			StatusLabel.Text = "Source video is not ready yet, please wait.";
			return;
		}

		SetBusy(true, $"{operationTitle}…");

		var outputPath = Path.Combine(FileSystem.CacheDirectory, outputFileName);
		if (File.Exists(outputPath))
			File.Delete(outputPath);

		_cancellation = new CancellationTokenSource();

		// Statistics arrive on FFmpegKit's own thread, so the UI update is marshalled back.
		FFmpegKitConfig.EnableStatisticsCallback(statistics =>
		{
			if (_sourceDurationMs <= 0)
				return;

			var fraction = Math.Clamp(statistics.Time / _sourceDurationMs, 0, 1);
			MainThread.BeginInvokeOnMainThread(() => Progress.Progress = fraction);
		});

		try
		{
			// Awaited directly: FFmpegKit.ExecuteAsync wraps the native completion callback, so
			// no Task.Run is needed to keep the UI responsive.
			var session = await FFmpegKit.ExecuteAsync(buildCommand(outputPath), _cancellation.Token);

			var returnCode = session.ReturnCode;
			var message = returnCode switch
			{
				null => "no return code",
				{ IsValueSuccess: true } => $"success, {session.Duration} ms, {session.State}",
				{ IsValueCancel: true } => "cancelled",
				// A command that ran and failed explains itself in Output (its last line is
				// FFmpeg's error message); FailStackTrace only exists when the session could
				// not run at all.
				_ => $"failed (code {returnCode.Value}): {LastLine(session.Output) ?? session.FailStackTrace}",
			};

			if (returnCode is { IsValueSuccess: true })
				ResultPlayer.Source = MediaSource.FromFile(outputPath);

			SetBusy(false, $"{operationTitle} — {message}");
		}
		finally
		{
			// Leaving the callback registered would keep updating the progress bar during the
			// next operation.
			FFmpegKitConfig.DisableStatisticsCallback();
			_cancellation.Dispose();
			_cancellation = null;
		}
	}

	void OnCancelClicked(object sender, EventArgs e)
	{
		// Cancellation is co-operative: FFmpeg stops as soon as it notices, and the awaited
		// session then completes with a cancelled return code rather than throwing.
		_cancellation?.Cancel();
		StatusLabel.Text = "Cancelling…";
	}

	static string? LastLine(string? output) =>
		output?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } lines
			? lines[^1]
			: null;

	void SetBusy(bool isBusy, string status)
	{
		Busy.IsVisible = isBusy;
		Busy.IsRunning = isBusy;
		Progress.IsVisible = isBusy;
		Progress.Progress = 0;
		CancelButton.IsEnabled = isBusy;
		ResizeButton.IsEnabled = !isBusy;
		GrayscaleButton.IsEnabled = !isBusy;
		ExtractAudioButton.IsEnabled = !isBusy;
		StatusLabel.Text = status;
	}
}
