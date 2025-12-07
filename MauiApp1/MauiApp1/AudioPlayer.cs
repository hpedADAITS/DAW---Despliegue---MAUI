using System;
using System.Diagnostics;

#if ANDROID
using Android.Media;
#endif

namespace MauiApp1
{
    public class AudioPlayer
    {
        private static AudioPlayer? _instance;
        public static AudioPlayer Instance => _instance ??= new AudioPlayer();

        public event Action? PlaybackCompleted;
        private double volume = 0.5;

#if ANDROID
        private MediaPlayer? player;

        public async Task PlayAsync(string url)
        {
            
            try
            {
                StopInternal();

                player = new MediaPlayer();
                player.Completion += PlayerOnCompletion;
                player.SetAudioStreamType(Android.Media.Stream.Music);
                player.Error += PlayerOnError;

                await player.SetDataSourceAsync(url);

                var prepareTcs = new TaskCompletionSource<bool>();
                player.Prepared += (_, __) => prepareTcs.TrySetResult(true);
                player.PrepareAsync();

                var completed = await Task.WhenAny(prepareTcs.Task, Task.Delay(8000));
                if (completed != prepareTcs.Task)
                {
                    throw new TimeoutException("Stream prepare timed out");
                }

                ApplyVolume();
                player.Start();

                Debug.WriteLine($"[Audio] Playing via MediaPlayer: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Error: {ex.Message}");
                StopInternal();
                throw;
            }
        }

        public void Pause()
        {
            try
            {
                if (player?.IsPlaying == true)
                {
                    player.Pause();
                    Debug.WriteLine("[Audio] Paused");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Pause error: {ex.Message}");
            }
        }

        public void Stop()
        {
            StopInternal();
        }

        private void StopInternal()
        {
            try
            {
                if (player != null)
                {
                    player.Completion -= PlayerOnCompletion;
                    if (player.IsPlaying)
                    {
                        player.Stop();
                    }
                    player.Reset();
                    player.Release();
                    Debug.WriteLine("[Audio] Stopped");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Audio] Stop error: {ex.Message}");
            }
            finally
            {
                player = null;
            }
        }

        public bool IsPlaying => player?.IsPlaying ?? false;

        public double PositionSeconds => player?.CurrentPosition / 1000.0 ?? 0;

        public double DurationSeconds => player?.Duration / 1000.0 ?? 0;

        public void SetVolume(double level)
        {
            volume = Math.Clamp(level, 0.0, 1.0);
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (player != null)
            {
                player.SetVolume((float)volume, (float)volume);
            }
        }

        private void PlayerOnCompletion(object? sender, EventArgs e)
        {
            PlaybackCompleted?.Invoke();
        }

        private void PlayerOnError(object? sender, MediaPlayer.ErrorEventArgs e)
        {
            Debug.WriteLine($"[Audio] MediaPlayer error: {e.What}");
            StopInternal();
        }
#else
        
        public async Task PlayAsync(string url)
        {
            await Launcher.Default.OpenAsync(new Uri(url));
        }

        public void Pause()
        {
            Debug.WriteLine("[Audio] Pause unsupported on this platform");
        }

        public void Stop()
        {
            Debug.WriteLine("[Audio] Stop unsupported on this platform");
        }

        public bool IsPlaying => false;

        public double PositionSeconds => 0;

        public double DurationSeconds => 0;

        public void SetVolume(double level)
        {
            volume = Math.Clamp(level, 0.0, 1.0);
            Debug.WriteLine($"[Audio] Volume set to {volume:P0} (no-op for this platform)");
        }
#endif
    }
}
