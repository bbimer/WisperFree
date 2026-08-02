using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FreeFlowWin.App
{
    public partial class RecordingOverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private double _smoothedVol = 0.0;
        private double _phase = 0.0;
        private readonly DispatcherTimer _animTimer;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public RecordingOverlayWindow()
        {
            InitializeComponent();
            
            _animTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _animTimer.Tick += AnimTimer_Tick;
            
            Loaded += RecordingOverlayWindow_Loaded;
            Unloaded += RecordingOverlayWindow_Unloaded;
        }

        private void RecordingOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Position overlay at bottom center of screen
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = screenHeight - this.Height - 140;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);

            _animTimer.Start();
        }

        private void RecordingOverlayWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _animTimer.Stop();
        }

        private void AnimTimer_Tick(object? sender, EventArgs e)
        {
            _phase += 0.18;
            if (_phase > Math.PI * 200) _phase = 0;

            double wave1 = Math.Sin(_phase);
            double wave2 = Math.Sin(_phase + 1.2);
            double wave3 = Math.Sin(_phase + 2.4);
            double wave4 = Math.Sin(_phase + 3.6);
            double wave5 = Math.Sin(_phase + 4.8);

            // Base breathing wave + dynamic voice volume amplification
            double baseAmp = 0.2 + _smoothedVol * 1.5;

            Scale1.ScaleY = 0.35 + baseAmp * 0.5 * (0.6 + 0.4 * wave1);
            Scale2.ScaleY = 0.40 + baseAmp * 0.8 * (0.6 + 0.4 * wave2);
            Scale3.ScaleY = 0.35 + baseAmp * 1.1 * (0.6 + 0.4 * wave3);
            Scale4.ScaleY = 0.45 + baseAmp * 0.7 * (0.6 + 0.4 * wave4);
            Scale5.ScaleY = 0.35 + baseAmp * 0.4 * (0.6 + 0.4 * wave5);

            // Decay smoothed volume gradually
            _smoothedVol *= 0.92;
        }

        public void UpdateVolume(float volume)
        {
            Dispatcher.Invoke(() =>
            {
                double boostedVol = Math.Sqrt(volume) * 3.5;
                double targetVol = Math.Min(1.0, Math.Max(0.0, boostedVol));
                _smoothedVol = _smoothedVol + (targetVol - _smoothedVol) * 0.35;
            });
        }
    }
}
