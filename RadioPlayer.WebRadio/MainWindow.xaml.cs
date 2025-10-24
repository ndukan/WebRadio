using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using RadioStream.Core.Models;
using RadioStream.Core.Services;

namespace RadioPlayer.WebRadio
{
    public partial class MainWindow : Window
    {
        private RadioStreamPlayer _player;
        private RadioStationManager _stationManager;
        private DispatcherTimer _debugTimer;

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            InitializeUI();
            StartDebugTimer();
        }

        private void InitializeServices()
        {
            _player = new RadioStreamPlayer();
            _stationManager = new RadioStationManager();

            StationsListBox.ItemsSource = _stationManager.Stations;
            UpdateStationCount();

            // Player event handlers
            _player.StatusChanged += (s, e) =>
                Dispatcher.Invoke(() => StatusText.Text = e.Message);

            _player.BufferLevelChanged += (s, e) =>
                Dispatcher.Invoke(() =>
                {
                    BufferProgress.Value = e.BufferLevel;
                    BufferText.Text = $"{e.BufferLevel:F1}%";
                });

            _player.DebugMessage += (s, e) =>
                System.Diagnostics.Trace.WriteLine($"UI: {e.Message}");

            _player.ErrorOccurred += (s, e) =>
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Error: {e.Exception.Message}", "Playback Error");
                    StopButton.IsEnabled = false;
                    PlayButton.IsEnabled = true;
                });


            // volume control
            _player.VolumeChanged += (s, e) =>  
                Dispatcher.Invoke(() =>
                {
                    VolumeSlider.Value = e.Volume;
                    VolumeText.Text = $"{(int)(e.Volume * 100)}%";
                });


            // favoriti
            _stationManager.StationFavorited += (s, e) => Dispatcher.Invoke(() => RefreshStationsList());
            _stationManager.StationUnfavorited += (s, e) => Dispatcher.Invoke(() => RefreshStationsList());

        }

        private void InitializeUI()
        {
            StationsListBox.SelectionChanged += StationsListBox_SelectionChanged;
        }


        private void StationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StationsListBox.SelectedItem is RadioStation selectedStation)
            {
                CurrentStationText.Text = selectedStation.Name;
                CurrentStationInfo.Text = $"{selectedStation.Genre} • {selectedStation.Country} • {selectedStation.Bitrate}kbps";

                // Automatski enable Play dugme kada se selektuje stanica
                PlayButton.IsEnabled = true;
            }
        }


        private void StartDebugTimer()
        {
            _debugTimer = new DispatcherTimer();
            _debugTimer.Interval = TimeSpan.FromSeconds(3);
            _debugTimer.Tick += (s, e) =>
            {
                if (_player != null)
                {
                    //System.Diagnostics.Trace.WriteLine($"UI Timer - Selected: {StationsListBox.SelectedItem}, Buffer: {BufferProgress.Value}%");
                }
            };
            _debugTimer.Start();
        }

        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (StationsListBox.SelectedItem is not RadioStation selectedStation)
            {
                MessageBox.Show("Please select a station first!", "No Station Selected");
                return;
            }

            PlayButton.IsEnabled = false;
            StopButton.IsEnabled = true;

            try
            {
                await _player.PlayAsync(selectedStation.StreamUrl);
                StatusText.Text = $"Playing: {selectedStation.Name}";
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Cannot play stream: {ex.Message}", "Error");
                PlayButton.IsEnabled = true;
                StopButton.IsEnabled = false;
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            PlayButton.IsEnabled = true;
            StopButton.IsEnabled = false;

            await _player.StopAsync();
            StatusText.Text = "Playback stopped";
        }

        protected override void OnClosed(EventArgs e)
        {
            _player?.Dispose();
            _debugTimer?.Stop();
            base.OnClosed(e);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_player != null)
            {
                _player.Volume = (float)e.NewValue;
                VolumeText.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }



        //private void RefreshStationsList()
        //{
        //    if (StationsTabControl.SelectedIndex == 0) // All Stations
        //    {
        //        StationsListBox.ItemsSource = _stationManager.Stations;
        //    }
        //    else // Favorites
        //    {
        //        StationsListBox.ItemsSource = _stationManager.FavoriteStations;
        //    }
        //    UpdateStationCount();
        //}



        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T found) return found;
            return FindParent<T>(parent);
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is RadioStation station)
            {
                _stationManager.ToggleFavorite(station);

                //ManualRefreshHeart(button, station);
                button.Content = station.IsFavorite ? "❤️" : "🤍";

                //RefreshStationsUI();

                Trace.WriteLine($"⭐ Favorite toggled: {station.Name} -> {station.IsFavorite}");
            }
        }

        private void HeartTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock heartText && heartText.Tag is RadioStation station)
            {
                _stationManager.ToggleFavorite(station);

                // 👇 OSVEŽI BINDING - TextBlock bolje radi sa bindingom!
                var binding = heartText.GetBindingExpression(TextBlock.TextProperty);
                binding?.UpdateTarget();

                // OSVEŽI CELE LISTE
                RefreshStationsUI();

                Trace.WriteLine($"⭐ Favorite toggled: {station.Name} -> {station.IsFavorite}");

                // Animacija (opciono)
                HeartAnimation(heartText);
            }
        }

        private void HeartAnimation(TextBlock heartText)
        {
            // Mala animacija kada se klikne
            var scaleAnimation = new DoubleAnimation(1.3, 1, TimeSpan.FromMilliseconds(200));
            heartText.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            heartText.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }



        private void UpdateStationCount()
        {
            if (StationsTabControl.SelectedIndex == 0)
            {
                StationCountText.Text = $"{_stationManager.Stations.Count} stations";
            }
            else
            {
                StationCountText.Text = $"{_stationManager.FavoriteStations.Count} favorites";
            }
        }
        
        
        
        private void RefreshStationsUI()
        {
            // 👇 NAJJEDNOSTAVNIJE - re-setuj ItemsSource
            var currentItems = StationsListBox.ItemsSource;
            StationsListBox.ItemsSource = null;
            StationsListBox.ItemsSource = currentItems;

            UpdateStationCount();
            Trace.WriteLine("🔄 UI refreshed - ItemsSource reset");
        }



        // UPDATE RefreshStationsList METODA
        private void RefreshStationsList()
        {
            if (StationsTabControl.SelectedIndex == 0) // All Stations
            {
                StationsListBox.ItemsSource = _stationManager.FilteredStations;
            }
            else // Favorites
            {
                StationsListBox.ItemsSource = _stationManager.FilteredFavorites;
            }
            UpdateStationCount();
        }



    }
}