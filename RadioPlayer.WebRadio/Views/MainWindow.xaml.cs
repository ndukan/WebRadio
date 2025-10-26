using System.ComponentModel;
using System.Diagnostics;
//using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using NAudio.Gui;
using RadioStream.Core.Models;
using RadioStream.Core.Services;
using NAudio.Wave;

namespace RadioPlayer.WebRadio.Views
{
    public partial class MainWindow : Window
    {
        private RadioStreamPlayer _player;
        private RadioStationManager _stationManager;
        private DispatcherTimer _debugTimer;

        private AppSettings _appSettings;

        private CancellationTokenSource _selectionCancellationToken;

        // vu metar
        private WasapiLoopbackCapture _loopbackCapture;
        private SampleAggregator _sampleAggregator;
        private DispatcherTimer _meterUpdateTimer;
        private DispatcherTimer _volumeMeterTimer;
        private Queue<double> _recentValues = new Queue<double>();
        private const int SAMPLE_WINDOW = 10;

        public MainWindow()
        {
            InitializeComponent();
            InitializeServices();
            InitializeRealVolumeMeter();
            LoadSettings();
        }


        private void InitializeRealVolumeMeter()
        {
            _meterUpdateTimer = new DispatcherTimer();
            _meterUpdateTimer.Interval = TimeSpan.FromMilliseconds(60);
            _meterUpdateTimer.Tick += OnMeterUpdateTick;
            _meterUpdateTimer.Start();
        }


        private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
        {
            for (int i = 0; i < e.BytesRecorded; i += 2) // 16-bitni sample-ovi
            {
                if (i + 1 < e.BytesRecorded)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i);
                    float sample32 = sample / 32768f; // Konvertuj u float (-1.0 do 1.0)
                    _sampleAggregator.Add(sample32);
                }
            }
        }

        private void OnMeterUpdateTick(object sender, EventArgs e)
        {

        }

        private double ConvertToVolumeLevel(double maxValue)
        {
            double volumeLevel;

            volumeLevel = ((maxValue * 1000) % 1) * 90;

            volumeLevel = Math.Max(0, Math.Min(100, volumeLevel));
            return volumeLevel;

        }


        private void CleanupVolumeMeter()
        {
            _meterUpdateTimer.Stop();
        }


        private void LoadFavorites()
        {
            //foreach (var station in _stationManager.Stations)
            //{
            //    station.IsFavorite = SettingsManager.IsFavoriteStation(_appSettings, station.Name);
            //}

            StationsListBox.ItemsSource = _stationManager.Stations;

            StationsListBox.Items.Refresh();
        }


        private void LoadLastStation()
        {
            if (!string.IsNullOrEmpty(_appSettings.LastStation))
            {
                var lastStation = _stationManager.Stations.FirstOrDefault(x => x.Name == _appSettings.LastStation);

                if (lastStation != null)
                {
                    StationsListBox.SelectedItem = lastStation;

                    //PlaySelectedStation(); - auto start
                }
                else
                    StatusText.Text = "Last station not found in list";
            }

        }


        // automatski pokreni reprodukciju - nakon LoadLastStation
        private async void PlaySelectedStation()
        {
            if (StationsListBox.SelectedItem is RadioStation station)
            {
                _appSettings.LastStation = station.Name;

                PlayButton.IsEnabled = false;
                StopButton.IsEnabled = true;

                await _player.PlayAsync(station);

                StatusText.Text = $"Now playing: {station.Name}";
                CurrentStationText.Text = station.Name;
                CurrentStationInfo.Text = $"{station.Genre} • {station.Country}";
            }
            else
            {
                StatusText.Text = "Please select a station first";
            }
        }



        private void LoadSettings()
        {
            _appSettings = SettingsManager.LoadSettings();

            if (_appSettings.WindowWidth > 0 && _appSettings.WindowHeight > 0)
            {
                this.Left = _appSettings.WindowLeft;
                this.Top = _appSettings.WindowTop;
                this.Width = _appSettings.WindowWidth;
                this.Height = _appSettings.WindowHeight;
                this.WindowState = _appSettings.WindowState;
            }

            if (_appSettings.Volume > 0)
                VolumeSlider.Value = _appSettings.Volume;

            LoadFavorites();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadLastStation();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void SaveSettings()
        {
            // Save window settings (only if not maximized)
            if (this.WindowState == WindowState.Normal)
            {
                _appSettings.WindowLeft = this.Left;
                _appSettings.WindowTop = this.Top;
                _appSettings.WindowWidth = this.Width;
                _appSettings.WindowHeight = this.Height;
            }
            _appSettings.WindowState = this.WindowState;

            _appSettings.Volume = VolumeSlider.Value;

            _appSettings.LastStation = (StationsListBox.SelectedItem as RadioStation)?.Name;

            SettingsManager.SaveSettings(_appSettings);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            base.OnClosing(e);
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

            //_player.BufferLevelChanged += (s, e) =>
            //    Dispatcher.Invoke(() =>
            //    {
            //        BufferProgress.Value = e.BufferLevel;
            //        BufferText.Text = $"{e.BufferLevel:F1}%";
            //    });

            _player.BufferLevelChanged += (s, e) =>
                Dispatcher.Invoke(() => 
                {
                    BufferStatusText.Text = $"{e.BufferLevel:F1}%";
                    UpdateBufferMeter(e.BufferLevel);
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

            //_player.VolumeLevelChanged += OnVolumeLevelChanged;

            // favoriti
            _stationManager.StationFavorited += (s, e) => Dispatcher.Invoke(() => RefreshStationsList());
            _stationManager.StationUnfavorited += (s, e) => Dispatcher.Invoke(() => RefreshStationsList());
        }


        private void UpdateBufferMeter(double bufferLevel)
        {
            Dispatcher.Invoke(() =>
            {
                double maxGreen = 230;   // 60%
                double maxYellow = 40;   // 20%
                double maxRed = 30;      // 20%

                double green = Math.Min(bufferLevel, 60) / 60 * maxGreen;
                double yellow = bufferLevel > 60 ? Math.Min(bufferLevel - 60, 20) / 20 * maxYellow : 0;
                double red = bufferLevel > 80 ? (bufferLevel - 80) / 20 * maxRed : 0;

                BufferMeterGreen.Width = green;
                BufferMeterYellow.Width = yellow;
                BufferMeterRed.Width = red;

            });
        }


        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {

            if (StationsListBox.SelectedItem is not RadioStation selectedStation)
            {
                MessageBox.Show("Please select a station first!", "No Station Selected");
                return;
            }

            await AutoPlaySelectedStation(selectedStation);

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
            CleanupVolumeMeter();
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


        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T found) return found;
            return FindParent<T>(parent);
        }

        private void HeartTextBlock_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock heartText && heartText.Tag is RadioStation station)
            {
                _stationManager.ToggleFavorite(station);

                StationsListBox.Items.Refresh();
            }
        }

        
        private void RefreshStationsList()
        {
            if (StationsTabControl.SelectedIndex == 0)
                StationsListBox.ItemsSource = _stationManager.FilteredStations;
            else
                StationsListBox.ItemsSource = _stationManager.FilteredFavorites;

            StationsListBox.Items.Refresh();

            UpdateStationCount();
        }

        private async void StationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectionCancellationToken?.Cancel();
            _selectionCancellationToken = new CancellationTokenSource();

            try
            {
                await Task.Delay(150, _selectionCancellationToken.Token);

                if (e.AddedItems.Count == 0 || StationsListBox.SelectedItem is not RadioStation selectedStation)
                    return;

                //CurrentStationText.Text = selectedStation.Name;
                //CurrentStationInfo.Text = $"{selectedStation.Genre} • {selectedStation.Country}";
                StatusText.Text = $"Selected: {selectedStation.StreamUrl}";
                _appSettings.LastStation = selectedStation.Name;

                //await AutoPlaySelectedStation(selectedStation);
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }



        private async Task AutoPlaySelectedStation(RadioStation station)
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                MessageBox.Show("No internet connection available!", "Network Error");
                return;
            }

            if (_player.IsPlaying && _player.StationPlayNow.Name == station.Name)
            {
                StatusText.Text = $"{station.Name} is already playing";
                return;
            }

            try
            {
                StatusText.Text = $"Loading {station.Name}...";

                PlayButton.IsEnabled = false;
                StopButton.IsEnabled = true;

                if (_player.IsPlaying)
                {
                    await _player.StopAsync();
                    StatusText.Text = $"Switching to: {station.Name}";
                    await Task.Delay(200);
                }

                await _player.PlayAsync(station);

                StatusText.Text = $"Now playing: {station.Name}";
                CurrentStationText.Text = station.Name;
                CurrentStationInfo.Text = $"{station.Genre} • {station.Country}";

                _appSettings.LastStation = station.Name;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error playing {station.Name}: {ex.Message}";
                PlayButton.IsEnabled = true;
                StopButton.IsEnabled = false;

                MessageBox.Show($"Cannot play {station.Name}: {ex.Message}", "Playback Error");
            }
        }

        private void StationsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StationsTabControl.SelectedItem is TabItem selectedTab)
            {
                var tabHeader = selectedTab.Header as StackPanel;
                if (tabHeader != null && tabHeader.Children.Count > 1)
                {
                    var headerText = (tabHeader.Children[1] as TextBlock)?.Text;

                    if (headerText == "All Stations")
                    {
                        ShowAllStations();
                    }
                    else if (headerText == "Favorites")
                    {
                        ShowFavoriteStations();
                    }
                }
            }
        }

        private void ShowAllStations()
        {
            StationsListBox.ItemsSource = _stationManager.Stations;
            UpdateStationCount();
        }

        private void ShowFavoriteStations()
        {
            var favoriteStations = _stationManager.Stations.Where(s => s.IsFavorite).ToList();
            StationsListBox.ItemsSource = favoriteStations;
            UpdateStationCount();
        }

        private void UpdateStationCount()
        {
            var count = StationsListBox.Items.Count;
            StationCountText.Text = $"{count} stations";

            if (StationsTabControl.SelectedIndex == 1)
                StationCountText.Text = $"{count} favorite stations";
            else
                StationCountText.Text = $"{count} stations";
        }

        private void AddStationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addDialog = new AddStationDialog();
                addDialog.Owner = this;
                addDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                if (addDialog.ShowDialog() == true)
                {
                    var newStation = new RadioStation
                    {
                        Name = addDialog.StationName,
                        StreamUrl = addDialog.StationUrl,
                        Genre = "Unknown",
                        Country = "Unknown",
                        IsFavorite = false
                    };

                    _stationManager.Stations.Add(newStation);

                    RefreshStationList();

                    _stationManager.SaveStations();

                    StatusText.Text = $"Station '{newStation.Name}' added successfully!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding station: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveStationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (StationsListBox.SelectedItem is RadioStation selectedStation)
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to remove '{selectedStation.Name}'?",
                        "Confirm Removal",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        _stationManager.Stations.Remove(selectedStation);

                        if (_player.StationPlayNow == selectedStation)
                        {
                            StopButton_Click(sender, e);
                        }

                        RefreshStationList();

                        _stationManager.SaveStations();

                        StatusText.Text = $"Station '{selectedStation.Name}' removed.";
                    }
                }
                else
                {
                    MessageBox.Show("Please select a station to remove.", "No Station Selected",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing station: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleFavoriteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (StationsListBox.SelectedItem is RadioStation selectedStation)
            {
                selectedStation.IsFavorite = !selectedStation.IsFavorite;

                StationsListBox.Items.Refresh();

                RefreshStationList();

                StatusText.Text = $"Station '{selectedStation.Name}' {(selectedStation.IsFavorite ? "added to" : "removed from")} favorites.";
            }
        }

        // Pomocna metoda za osvežavanje liste
        private void RefreshStationList()
        {
            if (StationsTabControl.SelectedIndex == 0)
                ShowAllStations();
            else if (StationsTabControl.SelectedIndex == 1)
                ShowFavoriteStations();
        }

        private void OnDebugMessage(string message)
        {
            System.Diagnostics.Trace.WriteLine($"MainWindow: {message}");

            // Opciono: prikaz u status baru
            Dispatcher.Invoke(() =>
            {
                if (message.Contains("volume", StringComparison.OrdinalIgnoreCase))
                {
                    StatusText.Text = $"Volume Meter: {message}";
                }
            });
        }


        public class SampleAggregator
        {
            private double _maxValue;
            private readonly object _lock = new object();

            public void Add(float value)
            {
                lock (_lock)
                {
                    _maxValue = Math.Max(_maxValue, Math.Abs(value));
                }
            }

            public double GetMaxValue()
            {
                lock (_lock)
                {
                    double value = _maxValue;
                    _maxValue = 0; // Resetuj za sledeću meru
                    return value;
                }
            }

            public void Reset()
            {
                lock (_lock)
                {
                    _maxValue = 0;
                }
            }
        }

    }
}