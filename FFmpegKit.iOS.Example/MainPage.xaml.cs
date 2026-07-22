using CommunityToolkit.Maui.Views;
using Ffmpegkit.Ios;

namespace FFmpegKitExample;

public partial class MainPage : ContentPage
{
	const string SampleAssetName = "sample.mp4";
	string _sourcePath = string.Empty;

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

		var command = buildCommand(outputPath);

		var (success, message) = await Task.Run(() =>
		{
			var session = FFmpegKit.Execute(command);
			var returnCode = session.ReturnCode;
			var ok = returnCode is not null && returnCode.IsValueSuccess;
			var log = ok
				? $"success, {session.Duration} ms, {session.State}"
				: $"failed (code {returnCode?.Value}): {session.FailStackTrace}";
			return (ok, log);
		});

		if (success)
			ResultPlayer.Source = MediaSource.FromFile(outputPath);

		SetBusy(false, $"{operationTitle} — {message}");
	}

	void SetBusy(bool isBusy, string status)
	{
		Busy.IsVisible = isBusy;
		Busy.IsRunning = isBusy;
		ResizeButton.IsEnabled = !isBusy;
		GrayscaleButton.IsEnabled = !isBusy;
		ExtractAudioButton.IsEnabled = !isBusy;
		StatusLabel.Text = status;
	}
}
