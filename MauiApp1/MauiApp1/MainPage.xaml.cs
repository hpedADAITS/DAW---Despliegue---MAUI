using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Controls;
using MauiApp1.Resources.Strings;

namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {
        private ObservableCollection<AudioTrack> playlist = new();
        private ObservableCollection<AudioTrack> visiblePlaylist = new();
        private ObservableCollection<AudioTrack> missingCoverPlaylist = new();
        private readonly HashSet<string> selectedGenres = new(StringComparer.OrdinalIgnoreCase);
        private int? selectedMinBitrate = null;
        private readonly List<Button> genreButtons = new();
        private readonly List<Button> bitrateButtons = new();
        private int currentTrackIndex = -1;
        private bool isPlaying = false;

        private const int RadioStationLimit = 120;
        private const string DefaultDockerApiUrl = "http://localhost:3000";
        private readonly string apiBaseUrl;
        private readonly string radiosEndpoint;
        private readonly Color primaryAccent = Color.FromArgb("#6750A4");
        private readonly Color neutralSurface = Color.FromArgb("#E8DEF8");
        private IDispatcherTimer? progressTimer;
        private EventHandler? progressTickHandler;

        private bool isDarkMode = false;
        private int loadOrderCounter = 0;
        private string currentSortOption = "default";
        private bool missingCoverVisible = false;
        private readonly string[] sortOptionCodes = new[] { "default", "bitrateHigh", "country", "genre", "title" };

        
        private CollectionView PlaylistView = default!;
        private CollectionView MissingCoverView = default!;
        private Picker SortPicker = default!;
        private FlexLayout GenreFlex = default!;
        private FlexLayout BitrateFlex = default!;
        private Slider VolumeSlider = default!;
        private Label VolumeValueLabel = default!;
        private Picker ThemePicker = default!;
        private Slider ProgressSlider = default!;
        private Button PlayPauseBtn = default!;
        private Button StopBtn = default!;
        private Button PreviousBtn = default!;
        private Button NextBtn = default!;
        private Label CurrentSongTitle = default!;
        private Label CurrentSongArtist = default!;
        private Label CurrentSongGenre = default!;
        private Label CurrentSongBitrate = default!;
        private Image AlbumCover = default!;
        private Label TimeDuration = default!;
        private Label TimeElapsed = default!;
        private Button MissingCoverToggle = default!;
        private Label ApiStatusLabel = default!;
        private Picker LanguagePicker = default!;

        
        public static MainPage? Instance { get; private set; }

        public MainPage()
        {
            InitializeComponent();
            Instance = this;
            
            
            PlaylistView = (CollectionView)PlaylistViewContent.FindByName("PlaylistCollectionView")!;
            MissingCoverView = (CollectionView)PlaylistViewContent.FindByName("MissingCoverCollectionView")!;
            SortPicker = (Picker)PlaylistViewContent.FindByName("SortPicker")!;
            MissingCoverToggle = (Button)PlaylistViewContent.FindByName("MissingCoverToggle")!;
            GenreFlex = (FlexLayout)PlaylistSettingsViewContent.FindByName("GenreFlex")!;
            BitrateFlex = (FlexLayout)PlaylistSettingsViewContent.FindByName("BitrateFlex")!;
            
            PlaylistView.ItemsSource = visiblePlaylist;
            MissingCoverView.ItemsSource = missingCoverPlaylist;
            UpdateMissingCoverToggleText();

            apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL")?.TrimEnd('/') ??
                (DeviceInfo.Current.Platform == DevicePlatform.Android
                    ? "http://10.0.2.2:3000"
                    : DefaultDockerApiUrl);
            radiosEndpoint = $"{apiBaseUrl}/api/radios/comprehensive?limit={RadioStationLimit}";

            
            VolumeSlider = (Slider)NowPlayingViewContent.FindByName("VolumeSlider")!;
            VolumeValueLabel = (Label)NowPlayingViewContent.FindByName("VolumeValueLabel")!;
            ProgressSlider = (Slider)NowPlayingViewContent.FindByName("ProgressSlider")!;
            PlayPauseBtn = (Button)NowPlayingViewContent.FindByName("PlayPauseBtn")!;
            StopBtn = (Button)NowPlayingViewContent.FindByName("StopBtn")!;
            PreviousBtn = (Button)NowPlayingViewContent.FindByName("PreviousBtn")!;
            NextBtn = (Button)NowPlayingViewContent.FindByName("NextBtn")!;
            CurrentSongTitle = (Label)NowPlayingViewContent.FindByName("CurrentSongTitle")!;
            CurrentSongArtist = (Label)NowPlayingViewContent.FindByName("CurrentSongArtist")!;
            CurrentSongGenre = (Label)NowPlayingViewContent.FindByName("CurrentSongGenre")!;
            CurrentSongBitrate = (Label)NowPlayingViewContent.FindByName("CurrentSongBitrate")!;
            AlbumCover = (Image)NowPlayingViewContent.FindByName("AlbumCover")!;
            TimeDuration = (Label)NowPlayingViewContent.FindByName("TimeDuration")!;
            TimeElapsed = (Label)NowPlayingViewContent.FindByName("TimeElapsed")!;

            VolumeSlider.ValueChanged += OnVolumeChanged;
            ApplyVolumeFromSlider(VolumeSlider.Value);

            
            ThemePicker = (Picker)SettingsViewContent.FindByName("ThemePicker")!;
            ApiStatusLabel = (Label)SettingsViewContent.FindByName("ApiStatusLabel")!;
            LanguagePicker = (Picker)SettingsViewContent.FindByName("LanguagePicker")!;
            SortPicker.ItemsSource = new[]
            {
                AppResources.SortOptionDefault,
                AppResources.SortOptionBitrate,
                AppResources.SortOptionCountry,
                AppResources.SortOptionGenre,
                AppResources.SortOptionTitle
            };
            SortPicker.SelectedIndex = 0;
            currentSortOption = sortOptionCodes[0];
            ThemePicker.SelectedIndexChanged += ThemePicker_SelectedIndexChanged;
            LanguagePicker.SelectedIndexChanged += LanguagePicker_SelectedIndexChanged;
            SortPicker.SelectedIndexChanged += OnSortPickerChanged;
            System.Diagnostics.Debug.WriteLine($"[Init] Resolved API base URL to {apiBaseUrl}");

            ThemePicker.SelectedIndex = 0;
            var culture = LocalizationService.CurrentCulture.TwoLetterISOLanguageName;
            LanguagePicker.SelectedIndex = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            ApplyTheme();
            OnNowPlayingClicked(this, EventArgs.Empty);

            AudioPlayer.Instance.PlaybackCompleted += OnPlaybackCompleted;

            MainThread.BeginInvokeOnMainThread(async () => 
            {
                await LoadRadioStationsAsync();
            });
        }

        private async Task LoadRadioStationsAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[Radio] START LoadRadioStationsAsync");
            try
            {
                using (var client = new HttpClient())
                {
                    System.Diagnostics.Debug.WriteLine($"[Radio] Loading from: {radiosEndpoint}");
                    client.Timeout = TimeSpan.FromSeconds(15);
                    var response = await client.GetAsync(radiosEndpoint);
                    
                    System.Diagnostics.Debug.WriteLine($"[Radio] Got response: {response.StatusCode}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonContent = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"[Radio] JSON received ({jsonContent.Length} bytes)");
                        
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var radios = JsonSerializer.Deserialize<List<Radio>>(jsonContent, options);
                        
                        System.Diagnostics.Debug.WriteLine($"[Radio] Deserialized: {radios?.Count ?? 0} stations");
                        
                        if (radios != null && radios.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Radio] Adding {radios.Count} stations to playlist");
                            
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                foreach (var radio in radios)
                                {
                                    var stationUuid = string.IsNullOrWhiteSpace(radio.StationUuid)
                                        ? $"radio-{loadOrderCounter}"
                                        : radio.StationUuid;
                                    var audioTrack = new AudioTrack
                                    {
                                        StationUuid = stationUuid,
                                        Title = radio.Title ?? AppResources.UnknownTitle,
                                        Artist = radio.CountryCode ?? AppResources.OnlineRadioLabel,
                                        CountryCode = radio.CountryCode,
                                        Duration = radio.Duration ?? "∞",
                                        DurationSeconds = 0,
                                        Genre = radio.Genre ?? AppResources.RadioGenreFallback,
                                        Url = radio.Url ?? "",
                                        CoverUrl = radio.CoverUrl ?? "",
                                        Bitrate = radio.Bitrate,
                                        LoadOrder = loadOrderCounter++,
                                        HasCover = radio.HasCover && !string.IsNullOrWhiteSpace(radio.CoverUrl)
                                    };
                                
                                    playlist.Add(audioTrack);
                                }
                                ApplySort(currentSortOption);
                                UpdateFilterOptionsFromPlaylist();
                                if (currentTrackIndex < 0 && playlist.Count > 0)
                                {
                                    currentTrackIndex = 0;
                                    UpdateCurrentTrackDisplay();
                                }
                                System.Diagnostics.Debug.WriteLine($"[Radio] SUCCESS: Playlist now has {playlist.Count} total tracks");
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Radio] No stations returned");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Radio] HTTP ERROR {response.StatusCode}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Radio] HTTP Error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Radio] JSON Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Radio] FATAL Error: {ex.GetType().Name} - {ex.Message}");
            }
            System.Diagnostics.Debug.WriteLine($"[Radio] END LoadRadioStationsAsync");
        }

        private void OnNowPlayingClicked(object sender, EventArgs e)
        {
            NowPlayingViewContent.IsVisible = true;
            PlaylistViewContent.IsVisible = false;
            PlaylistSettingsViewContent.IsVisible = false;
            SettingsViewContent.IsVisible = false;

            UpdateTabVisualState(NowPlayingTab);
        }

        private void OnPlaylistSettingsClicked(object sender, EventArgs e)
        {
            NowPlayingViewContent.IsVisible = false;
            PlaylistViewContent.IsVisible = true;
            PlaylistSettingsViewContent.IsVisible = true;
            SettingsViewContent.IsVisible = false;

            UpdateTabVisualState(PlaylistSettingsTab);
        }

        public void OnMissingCoverToggleClicked(object sender, EventArgs e)
        {
            missingCoverVisible = !missingCoverVisible;
            MissingCoverView.IsVisible = missingCoverVisible;
            UpdateMissingCoverToggleText();
        }

        private void OnSettingsClicked(object sender, EventArgs e)
        {
            NowPlayingViewContent.IsVisible = false;
            PlaylistViewContent.IsVisible = false;
            PlaylistSettingsViewContent.IsVisible = false;
            SettingsViewContent.IsVisible = true;

            UpdateTabVisualState(SettingsTab);
        }

        public async Task CheckApiStatusAsync()
        {
            if (ApiStatusLabel == null)
            {
                return;
            }

            ApiStatusLabel.Text = AppResources.ApiStatusChecking;

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                client.DefaultRequestHeaders.ExpectContinue = false;

                var response = await client.GetAsync($"{apiBaseUrl}/api/status");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var hostUsed = response.RequestMessage?.RequestUri?.Host ?? AppResources.UnknownTitle;
                    ApiStatusLabel.Text = string.Format(AppResources.ApiStatusOk, apiBaseUrl, hostUsed, json);
                }
                else
                {
                    ApiStatusLabel.Text = string.Format(AppResources.ApiStatusFail, (int)response.StatusCode, apiBaseUrl);
                }
            }
            catch (Exception ex)
            {
                ApiStatusLabel.Text = string.Format(AppResources.ApiStatusError, ex.Message);
            }
        }

        public void OnPlayPauseClicked(object sender, EventArgs e)
        {
            if (isPlaying)
            {
                PausePlayback();
            }
            else
            {
                StartOrResumePlayback();
            }
        }

        private void StartOrResumePlayback()
        {
            if (currentTrackIndex < 0 && playlist.Count > 0)
            {
                currentTrackIndex = 0;
            }

            if (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count)
            {
                var track = playlist[currentTrackIndex];
                isPlaying = true;
                UpdatePlaybackUI();
                UpdateCurrentTrackDisplay();
                StartProgressTimer();
                
                
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[Playback] Playing: {track.Title} - {track.Url}");
                        await AudioPlayer.Instance.PlayAsync(track.Url);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Playback] Error: {ex.Message}");
                        await DisplayAlert(AppResources.PlaybackErrorTitle, string.Format(AppResources.PlaybackErrorMessage, ex.Message), AppResources.OkButton);
                        isPlaying = false;
                        UpdatePlaybackUI();
                    }
                });
            }
        }
        
        private void PausePlayback()
        {
            isPlaying = false;
            AudioPlayer.Instance.Pause();
            UpdatePlaybackUI();
            StopProgressTimer();
        }

        public void OnStopClicked(object sender, EventArgs e)
        {
            isPlaying = false;
            AudioPlayer.Instance.Stop();
            currentTrackIndex = -1;
            ProgressSlider.Value = 0;
            TimeElapsed.Text = "0:00";
            UpdatePlaybackUI();
            CurrentSongTitle.Text = AppResources.NoSongPlaying;
            CurrentSongArtist.Text = AppResources.UnknownArtist;
            StopProgressTimer();
        }

        public void OnPreviousClicked(object sender, EventArgs e)
        {
            if (currentTrackIndex > 0)
            {
                currentTrackIndex--;
                UpdateCurrentTrackDisplay();
                if (isPlaying)
                {
                    StartOrResumePlayback();
                }
            }
        }

        public void OnNextClicked(object sender, EventArgs e)
        {
            if (currentTrackIndex < playlist.Count - 1)
            {
                currentTrackIndex++;
                UpdateCurrentTrackDisplay();
                if (isPlaying)
                {
                    StartOrResumePlayback();
                }
            }
        }

        private void OnAddFilesClicked(object sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert(AppResources.AddFilesTitle, AppResources.AddFilesMessage, AppResources.OkButton);
            });
        }

        private void UpdatePlaybackUI()
        {
            PlayPauseBtn.Text = isPlaying ? "‖" : "►";
            PlayPauseBtn.FontAttributes = isPlaying ? FontAttributes.Bold : FontAttributes.None;
            PlayPauseBtn.BackgroundColor = isPlaying ? neutralSurface : primaryAccent;
            PlayPauseBtn.TextColor = isPlaying ? primaryAccent : Colors.White;
            PlayPauseBtn.Opacity = 1.0;

            StopBtn.IsEnabled = isPlaying || currentTrackIndex >= 0;
            StopBtn.Opacity = StopBtn.IsEnabled ? 1.0 : 0.6;

            PreviousBtn.IsEnabled = true;
            NextBtn.IsEnabled = true;
        }

        private void UpdateCurrentTrackDisplay()
        {
            if (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count)
            {
                var track = playlist[currentTrackIndex];
                CurrentSongTitle.Text = string.IsNullOrWhiteSpace(track.Title) ? AppResources.UnknownTitle : track.Title;
                CurrentSongArtist.Text = string.IsNullOrWhiteSpace(track.Artist) ? AppResources.UnknownArtist : track.Artist;
                CurrentSongGenre.Text = string.IsNullOrWhiteSpace(track.Genre) ? AppResources.UnknownGenre : track.Genre;
                CurrentSongBitrate.Text = $"{track.Bitrate} kbps";
                AlbumCover.Source = track.CoverUrl;
                TimeDuration.Text = track.Duration;
                ProgressSlider.Maximum = track.DurationSeconds;
            }
        }

        private void ThemePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            isDarkMode = ThemePicker.SelectedIndex == 1; 
            ApplyTheme();
        }

        private void LanguagePicker_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var targetCulture = LanguagePicker.SelectedIndex == 1 ? "en" : "es";
            if (LocalizationService.CurrentCulture.TwoLetterISOLanguageName.Equals(targetCulture, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            LocalizationService.SetCulture(targetCulture);
            Application.Current.MainPage = new AppShell();
        }

        private void OnSortPickerChanged(object? sender, EventArgs e)
        {
            var idx = SortPicker.SelectedIndex;
            if (idx < 0 || idx >= sortOptionCodes.Length)
            {
                idx = 0;
            }
            currentSortOption = sortOptionCodes[idx];
            ApplySort(currentSortOption);
        }

        private void ApplySort(string sortOption)
        {
            if (playlist.Count == 0)
            {
                return;
            }

            var currentTrack = (currentTrackIndex >= 0 && currentTrackIndex < playlist.Count)
                ? playlist[currentTrackIndex]
                : null;

            IEnumerable<AudioTrack> sorted = sortOption switch
            {
                "bitrateHigh" => playlist.OrderByDescending(t => t.Bitrate).ThenBy(t => t.Title),
                "country" => playlist.OrderBy(t => t.CountryCode ?? t.Artist).ThenBy(t => t.Title),
                "genre" => playlist.OrderBy(t => t.Genre).ThenBy(t => t.Title),
                "title" => playlist.OrderBy(t => t.Title),
                _ => playlist.OrderBy(t => t.LoadOrder)
            };

            var sortedList = sorted.ToList();

            playlist.Clear();
            foreach (var track in sortedList)
            {
                playlist.Add(track);
            }

            UpdateFilteredViews();

            if (currentTrack != null)
            {
                var newIndex = playlist
                    .Select((track, idx) => new { track, idx })
                    .FirstOrDefault(x => x.track.Url == currentTrack.Url)?.idx ?? -1;

                if (newIndex >= 0)
                {
                    currentTrackIndex = newIndex;
                    UpdateCurrentTrackDisplay();
                }
            }

        }

        private bool IsPlaceholderCover(string? coverUrl)
        {
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                return true;
            }

            return coverUrl.Contains("placeholder.com", StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesFilters(AudioTrack track)
        {
            bool genreMatch = selectedGenres.Count == 0 || selectedGenres.Contains(track.Genre ?? string.Empty);
            bool bitrateMatch = !selectedMinBitrate.HasValue || track.Bitrate >= selectedMinBitrate.Value;
            return genreMatch && bitrateMatch;
        }

        private void UpdateFilteredViews()
        {
            visiblePlaylist.Clear();
            missingCoverPlaylist.Clear();

            foreach (var track in playlist)
            {
                if (!MatchesFilters(track))
                {
                    continue;
                }

                visiblePlaylist.Add(track);

                if (!track.HasCover || IsPlaceholderCover(track.CoverUrl))
                {
                    missingCoverPlaylist.Add(track);
                }
            }

            UpdateMissingCoverToggleText();
        }

        private void UpdateFilterOptionsFromPlaylist()
        {
            var genres = playlist
                .Where(t => !string.IsNullOrWhiteSpace(t.Genre))
                .Select(t => t.Genre!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g)
                .ToList();

            BuildGenreButtons(genres);
            BuildBitrateButtons();
            UpdateFilteredViews();
        }

        private void BuildGenreButtons(List<string> genres)
        {
            if (GenreFlex == null)
            {
                return;
            }

            genreButtons.Clear();
            GenreFlex.Children.Clear();

            foreach (var genre in genres)
            {
                var btn = new Button
                {
                    Text = genre,
                    CornerRadius = 14,
                    Padding = new Thickness(14, 8),
                    BackgroundColor = neutralSurface,
                    TextColor = primaryAccent,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 8, 8)
                };
                btn.Clicked += (s, e) => ToggleGenreFilter(genre, (Button)s!);
                genreButtons.Add(btn);
                GenreFlex.Children.Add(btn);
            }
        }

        private void BuildBitrateButtons()
        {
            if (BitrateFlex == null)
            {
                return;
            }

            bitrateButtons.Clear();
            BitrateFlex.Children.Clear();

            var thresholds = new[] { 192, 320 };
            foreach (var threshold in thresholds)
            {
                if (!playlist.Any(t => t.Bitrate >= threshold))
                {
                    continue;
                }

                var btn = new Button
                {
                    Text = $"{threshold} kbps+",
                    CornerRadius = 14,
                    Padding = new Thickness(14, 8),
                    BackgroundColor = neutralSurface,
                    TextColor = primaryAccent,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 8, 8),
                    CommandParameter = threshold
                };
                btn.Clicked += (s, e) => ToggleBitrateFilter((int)((Button)s!).CommandParameter);
                bitrateButtons.Add(btn);
                BitrateFlex.Children.Add(btn);
            }
        }

        private void ToggleGenreFilter(string genre, Button btn)
        {
            if (selectedGenres.Contains(genre))
            {
                selectedGenres.Remove(genre);
            }
            else
            {
                selectedGenres.Add(genre);
            }

            ApplyFilterButtonState(btn, selectedGenres.Contains(genre));
            UpdateFilteredViews();
        }

        private void ToggleBitrateFilter(int threshold)
        {
            if (selectedMinBitrate.HasValue && selectedMinBitrate.Value == threshold)
            {
                selectedMinBitrate = null;
            }
            else
            {
                selectedMinBitrate = threshold;
            }

            foreach (var btn in bitrateButtons)
            {
                var th = (int)btn.CommandParameter;
                ApplyFilterButtonState(btn, selectedMinBitrate.HasValue && selectedMinBitrate.Value == th);
            }

            UpdateFilteredViews();
        }

        private void ApplyFilterButtonState(Button btn, bool isActive)
        {
            btn.BackgroundColor = isActive ? primaryAccent : neutralSurface;
            btn.TextColor = isActive ? Colors.White : primaryAccent;
        }





        private string FormatTime(double totalSeconds)
        {
            var minutes = (int)(totalSeconds / 60);
            var seconds = (int)(totalSeconds % 60);
            return $"{minutes}:{seconds:D2}";
        }

        public void OnPlaylistItemSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection == null || e.CurrentSelection.Count == 0)
            {
                return;
            }

            if (e.CurrentSelection[0] is not AudioTrack selectedTrack)
            {
                return;
            }

            currentTrackIndex = playlist.IndexOf(selectedTrack);
            UpdateCurrentTrackDisplay();
            OnNowPlayingClicked(this, EventArgs.Empty);
            StartOrResumePlayback();
            PlaylistView.SelectedItem = null;
            MissingCoverView.SelectedItem = null;
        }

        private void OnVolumeChanged(object? sender, ValueChangedEventArgs e)
        {
            ApplyVolumeFromSlider(e.NewValue);
        }

        private void ApplyVolumeFromSlider(double sliderValue)
        {
            VolumeValueLabel.Text = $"{sliderValue:0}%";
            AudioPlayer.Instance.SetVolume(sliderValue / 100.0);
        }

        private void UpdateMissingCoverToggleText()
        {
            MissingCoverToggle.Text = string.Format(AppResources.MissingCoverToggle, missingCoverPlaylist.Count);
        }

        private void StartProgressTimer()
        {
            progressTimer?.Stop();
            progressTickHandler = ProgressTimer_Tick;
            var dispatcher = Microsoft.Maui.Dispatching.Dispatcher.GetForCurrentThread();
            if (dispatcher == null)
            {
                return;
            }

            progressTimer = dispatcher.CreateTimer();
            progressTimer.Interval = TimeSpan.FromMilliseconds(500);
            progressTimer.IsRepeating = true;
            progressTimer.Tick += progressTickHandler;
            progressTimer.Start();
        }

        private void StopProgressTimer()
        {
            if (progressTimer != null)
            {
                if (progressTickHandler != null)
                {
                    progressTimer.Tick -= progressTickHandler;
                }
                progressTimer.Stop();
                progressTimer = null;
            }
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            UpdateProgressFromPlayer();
        }

        private void UpdateProgressFromPlayer()
        {
            try
            {
                var player = AudioPlayer.Instance;
                if (!isPlaying || !player.IsPlaying || currentTrackIndex < 0 || currentTrackIndex >= playlist.Count)
                {
                    return;
                }

                var pos = player.PositionSeconds;
                var duration = player.DurationSeconds;
                var trackDuration = playlist[currentTrackIndex].DurationSeconds;

                double effectiveDuration = duration > 0 ? duration : trackDuration;
                if (effectiveDuration > 0)
                {
                    ProgressSlider.Maximum = effectiveDuration;
                    TimeDuration.Text = FormatTime(effectiveDuration);
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgressSlider.Value = pos;
                    TimeElapsed.Text = FormatTime(pos);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Playback] Progress update failed: {ex.Message}");
            }
        }

        private void UpdateTabVisualState(Button activeTab)
        {
            var tabs = new[] { NowPlayingTab, PlaylistSettingsTab, SettingsTab };
            foreach (var tab in tabs)
            {
                bool isActive = tab == activeTab;
                tab.BackgroundColor = isActive ? primaryAccent : neutralSurface;
                tab.TextColor = isActive ? Colors.White : primaryAccent;
            }
        }

        private void OnPlaybackCompleted()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StopProgressTimer();
                isPlaying = false;
                UpdatePlaybackUI();

                
                if (currentTrackIndex < playlist.Count - 1)
                {
                    currentTrackIndex++;
                    UpdateCurrentTrackDisplay();
                    StartOrResumePlayback();
                }
            });
        }

        private void ApplyTheme()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var app = Application.Current;
                if (app == null)
                {
                    return;
                }

                var resources = app.Resources;

                if (isDarkMode)
                {
                    
                    resources["PageBackground"] = Color.FromArgb("#141218");
                    resources["CardBackground"] = Color.FromArgb("#1e1b23");
                    resources["PrimaryText"] = Color.FromArgb("#e6e0e9");
                    resources["SecondaryText"] = Color.FromArgb("#cac4d0");
                    resources["TertiaryText"] = Color.FromArgb("#b3adba");
                    resources["BorderColor"] = Color.FromArgb("#49454f");
                    resources["AccentColor"] = Color.FromArgb("#d0bcff");
                    resources["ButtonBackground"] = Color.FromArgb("#49454f");
                    resources["SliderMaxColor"] = Color.FromArgb("#49454f");
                    resources["Surface"] = Color.FromArgb("#141218");
                    resources["OnSurface"] = Color.FromArgb("#e6e0e9");
                    resources["SurfaceVariant"] = Color.FromArgb("#49454f");
                    resources["OnSurfaceVariant"] = Color.FromArgb("#cac4d0");
                    resources["PrimaryContainer"] = Color.FromArgb("#4f378b");
                    resources["OnPrimaryContainer"] = Color.FromArgb("#eaddff");
                }
                else
                {
                    
                    resources["PageBackground"] = Color.FromArgb("#fffbfe");
                    resources["CardBackground"] = Color.FromArgb("#ffffff");
                    resources["PrimaryText"] = Color.FromArgb("#1c1b1f");
                    resources["SecondaryText"] = Color.FromArgb("#49454f");
                    resources["TertiaryText"] = Color.FromArgb("#625b71");
                    resources["BorderColor"] = Color.FromArgb("#e7e0ec");
                    resources["AccentColor"] = Color.FromArgb("#6750A4");
                    resources["ButtonBackground"] = Color.FromArgb("#e7e0ec");
                    resources["SliderMaxColor"] = Color.FromArgb("#e7e0ec");
                    resources["Surface"] = Color.FromArgb("#fffbfe");
                    resources["OnSurface"] = Color.FromArgb("#1c1b1f");
                    resources["SurfaceVariant"] = Color.FromArgb("#e7e0ec");
                    resources["OnSurfaceVariant"] = Color.FromArgb("#49454f");
                    resources["PrimaryContainer"] = Color.FromArgb("#eaddff");
                    resources["OnPrimaryContainer"] = Color.FromArgb("#21005d");
                }
            });
        }
    }

    public class AudioTrack
    {
        public int Id { get; set; }
        public string? StationUuid { get; set; }
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Duration { get; set; } = "";
        public int DurationSeconds { get; set; } = 0;
        public string Genre { get; set; } = "";
        public string Url { get; set; } = "";
        public string CoverUrl { get; set; } = "";
        public int Bitrate { get; set; } = 128;
        public int LoadOrder { get; set; } = 0;
        public string? CountryCode { get; set; }
        public bool HasCover { get; set; } = true;
    }

    public class Radio
    {
        public string? StationUuid { get; set; }
        public string? CheckUuid { get; set; }
        public string? ClickUuid { get; set; }
        public string? CountryCode { get; set; }
        public string? Title { get; set; }
        public string? Artist { get; set; }
        public string? Duration { get; set; }
        public int DurationSeconds { get; set; }
        public string? Genre { get; set; }
        public string? Url { get; set; }
        public string? CoverUrl { get; set; }
        public int Bitrate { get; set; }
        public string? Format { get; set; }
        public bool IsRadio { get; set; }
        public int ClickCount { get; set; }
        public bool HasCover { get; set; }
    }
}
