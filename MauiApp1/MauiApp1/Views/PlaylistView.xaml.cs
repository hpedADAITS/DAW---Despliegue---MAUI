namespace MauiApp1.Views
{
    public partial class PlaylistView : StackLayout
    {
        public PlaylistView()
        {
            InitializeComponent();
        }

        public void OnMissingCoverToggleClicked(object sender, EventArgs e)
        {
            MainPage.Instance?.OnMissingCoverToggleClicked(sender, e);
        }

        public void OnPlaylistItemSelected(object sender, SelectionChangedEventArgs e)
        {
            MainPage.Instance?.OnPlaylistItemSelected(sender, e);
        }
    }
}
