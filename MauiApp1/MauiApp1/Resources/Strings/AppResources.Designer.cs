using System;
using System.Globalization;
using System.Resources;

namespace MauiApp1.Resources.Strings
{
    public class AppResources
    {
        private static ResourceManager resourceMan;
        private static CultureInfo resourceCulture;

        public AppResources()
        {
        }

        public static ResourceManager ResourceManager
        {
            get
            {
                return resourceMan ??= new ResourceManager("MauiApp1.Resources.Strings.AppResources", typeof(AppResources).Assembly);
            }
        }

        public static CultureInfo Culture
        {
            get => resourceCulture;
            set => resourceCulture = value;
        }

        public static string AboutLabel => ResourceManager.GetString("AboutLabel", resourceCulture);
        public static string AddFilesMessage => ResourceManager.GetString("AddFilesMessage", resourceCulture);
        public static string AddFilesTitle => ResourceManager.GetString("AddFilesTitle", resourceCulture);
        public static string ApiStatusBackend => ResourceManager.GetString("ApiStatusBackend", resourceCulture);
        public static string ApiStatusChecking => ResourceManager.GetString("ApiStatusChecking", resourceCulture);
        public static string ApiStatusDirectory => ResourceManager.GetString("ApiStatusDirectory", resourceCulture);
        public static string ApiStatusError => ResourceManager.GetString("ApiStatusError", resourceCulture);
        public static string ApiStatusFail => ResourceManager.GetString("ApiStatusFail", resourceCulture);
        public static string ApiStatusOk => ResourceManager.GetString("ApiStatusOk", resourceCulture);
        public static string ApiStatusTitle => ResourceManager.GetString("ApiStatusTitle", resourceCulture);
        public static string ApiStatusUnknown => ResourceManager.GetString("ApiStatusUnknown", resourceCulture);
        public static string ApiUnavailableMessage => ResourceManager.GetString("ApiUnavailableMessage", resourceCulture);
        public static string ApiUnavailableTitle => ResourceManager.GetString("ApiUnavailableTitle", resourceCulture);
        public static string AppDescription => ResourceManager.GetString("AppDescription", resourceCulture);
        public static string AppNameVersion => ResourceManager.GetString("AppNameVersion", resourceCulture);
        public static string AppTitle => ResourceManager.GetString("AppTitle", resourceCulture);
        public static string ArrangeSubtitle => ResourceManager.GetString("ArrangeSubtitle", resourceCulture);
        public static string ArrangeTitle => ResourceManager.GetString("ArrangeTitle", resourceCulture);
        public static string CheckApiStatusButton => ResourceManager.GetString("CheckApiStatusButton", resourceCulture);
        public static string CreditsDesign => ResourceManager.GetString("CreditsDesign", resourceCulture);
        public static string CreditsDirectory => ResourceManager.GetString("CreditsDirectory", resourceCulture);
        public static string CreditsIcons => ResourceManager.GetString("CreditsIcons", resourceCulture);
        public static string CreditsLabel => ResourceManager.GetString("CreditsLabel", resourceCulture);
        public static string DarkOption => ResourceManager.GetString("DarkOption", resourceCulture);
        public static string GenreLabel => ResourceManager.GetString("GenreLabel", resourceCulture);
        public static string HomeTitle => ResourceManager.GetString("HomeTitle", resourceCulture);
        public static string LanguageEnglish => ResourceManager.GetString("LanguageEnglish", resourceCulture);
        public static string LanguageLabel => ResourceManager.GetString("LanguageLabel", resourceCulture);
        public static string LanguagePickerTitle => ResourceManager.GetString("LanguagePickerTitle", resourceCulture);
        public static string LanguageSpanish => ResourceManager.GetString("LanguageSpanish", resourceCulture);
        public static string LightOption => ResourceManager.GetString("LightOption", resourceCulture);
        public static string MinBitrateLabel => ResourceManager.GetString("MinBitrateLabel", resourceCulture);
        public static string MissingCoverTitle => ResourceManager.GetString("MissingCoverTitle", resourceCulture);
        public static string MissingCoverToggle => ResourceManager.GetString("MissingCoverToggle", resourceCulture);
        public static string NoSongPlaying => ResourceManager.GetString("NoSongPlaying", resourceCulture);
        public static string OkButton => ResourceManager.GetString("OkButton", resourceCulture);
        public static string OnlineRadioLabel => ResourceManager.GetString("OnlineRadioLabel", resourceCulture);
        public static string PlaybackErrorMessage => ResourceManager.GetString("PlaybackErrorMessage", resourceCulture);
        public static string PlaybackErrorTitle => ResourceManager.GetString("PlaybackErrorTitle", resourceCulture);
        public static string PlaylistHeader => ResourceManager.GetString("PlaylistHeader", resourceCulture);
        public static string RadioDirectoryLabel => ResourceManager.GetString("RadioDirectoryLabel", resourceCulture);
        public static string RadioGenreFallback => ResourceManager.GetString("RadioGenreFallback", resourceCulture);
        public static string SettingsTabText => ResourceManager.GetString("SettingsTabText", resourceCulture);
        public static string ShellTabArrange => ResourceManager.GetString("ShellTabArrange", resourceCulture);
        public static string ShellTabNow => ResourceManager.GetString("ShellTabNow", resourceCulture);
        public static string ShellTabPlaylist => ResourceManager.GetString("ShellTabPlaylist", resourceCulture);
        public static string SortOptionBitrate => ResourceManager.GetString("SortOptionBitrate", resourceCulture);
        public static string SortOptionCountry => ResourceManager.GetString("SortOptionCountry", resourceCulture);
        public static string SortOptionDefault => ResourceManager.GetString("SortOptionDefault", resourceCulture);
        public static string SortOptionGenre => ResourceManager.GetString("SortOptionGenre", resourceCulture);
        public static string SortOptionTitle => ResourceManager.GetString("SortOptionTitle", resourceCulture);
        public static string SortOrderLabel => ResourceManager.GetString("SortOrderLabel", resourceCulture);
        public static string SortPlaylistSubtitle => ResourceManager.GetString("SortPlaylistSubtitle", resourceCulture);
        public static string SortPlaylistTitle => ResourceManager.GetString("SortPlaylistTitle", resourceCulture);
        public static string StationWithoutCoverListTitle => ResourceManager.GetString("StationWithoutCoverListTitle", resourceCulture);
        public static string StatusNotChecked => ResourceManager.GetString("StatusNotChecked", resourceCulture);
        public static string StopMessageTitle => ResourceManager.GetString("StopMessageTitle", resourceCulture);
        public static string ThemeLabel => ResourceManager.GetString("ThemeLabel", resourceCulture);
        public static string ThemePickerTitle => ResourceManager.GetString("ThemePickerTitle", resourceCulture);
        public static string UnknownArtist => ResourceManager.GetString("UnknownArtist", resourceCulture);
        public static string UnknownGenre => ResourceManager.GetString("UnknownGenre", resourceCulture);
        public static string UnknownTitle => ResourceManager.GetString("UnknownTitle", resourceCulture);
        public static string VolumeLabel => ResourceManager.GetString("VolumeLabel", resourceCulture);
    }
}