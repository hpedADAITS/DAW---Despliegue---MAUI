namespace MauiApp1
{
    public partial class App : Application
    {
        public App()
        {
            LocalizationService.SetCulture("es");
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
