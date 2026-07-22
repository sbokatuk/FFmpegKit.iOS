namespace FFmpegKitExample;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	// Application.MainPage is obsolete in MAUI 9; the root page is supplied by overriding
	// CreateWindow instead. Setting MainPage still worked, but warned on every build.
	protected override Window CreateWindow(IActivationState? activationState) =>
		new(new AppShell());
}
