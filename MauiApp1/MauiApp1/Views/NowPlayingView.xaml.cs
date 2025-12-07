namespace MauiApp1.Views
{
    public partial class NowPlayingView : StackLayout
    {
        public NowPlayingView()
        {
            InitializeComponent();
        }

        
        public void OnPlayPauseClicked(object sender, EventArgs e)
        {
            MainPage.Instance?.OnPlayPauseClicked(sender, e);
        }

        public void OnStopClicked(object sender, EventArgs e)
        {
            MainPage.Instance?.OnStopClicked(sender, e);
        }

        public void OnPreviousClicked(object sender, EventArgs e)
        {
            MainPage.Instance?.OnPreviousClicked(sender, e);
        }

        public void OnNextClicked(object sender, EventArgs e)
        {
            MainPage.Instance?.OnNextClicked(sender, e);
        }
    }
}
