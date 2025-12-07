namespace MauiApp1.Views
{
    public partial class SettingsView : StackLayout
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        public async void OnApiStatusCheckClicked(object sender, EventArgs e)
        {
            if (MainPage.Instance != null)
            {
                await MainPage.Instance.CheckApiStatusAsync();
            }
        }
    }
}
