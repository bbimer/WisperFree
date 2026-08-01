using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FreeFlowWin.App
{
    public partial class RecordingOverlayWindow : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        private double _smoothedVol = 0.0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public RecordingOverlayWindow()
        {
            InitializeComponent();
            Loaded += RecordingOverlayWindow_Loaded;
        }

        private void RecordingOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Позиционируем окно в нижней части основного экрана по центру (над доком/таскбаром)
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = (screenWidth - this.Width) / 2;
            this.Top = screenHeight - this.Height - 140;

            // Получаем HWND дескриптор окна
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            // Добавляем стиль WS_EX_TRANSPARENT, чтобы клики мыши проходили сквозь окно
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);

            // Задаем начальные масштабы полосок спектра
            ResetVolume();
        }

        public void UpdateVolume(float volume)
        {
            Dispatcher.Invoke(() =>
            {
                // Moderate boost (3x) with sqrt curve for smooth, natural response
                double boostedVol = Math.Sqrt(volume) * 3.0;
                double targetVol = Math.Min(1.0, Math.Max(0.0, boostedVol));
                
                // Smooth interpolation (lerp) for cinematic fluidity
                _smoothedVol = _smoothedVol + (targetVol - _smoothedVol) * 0.25;

                // Each bar has a different base and multiplier for a wave-like pattern
                // Outer bars stay shorter, center bar grows tallest
                Scale1.ScaleY = 0.3 + _smoothedVol * 0.9;   // short outer
                Scale2.ScaleY = 0.4 + _smoothedVol * 1.4;   // medium
                Scale3.ScaleY = 0.3 + _smoothedVol * 1.8;   // tallest center
                Scale4.ScaleY = 0.5 + _smoothedVol * 1.2;   // medium
                Scale5.ScaleY = 0.3 + _smoothedVol * 0.7;   // short outer
            });
        }

        private void ResetVolume()
        {
            Scale1.ScaleY = 0.3;
            Scale2.ScaleY = 0.4;
            Scale3.ScaleY = 0.3;
            Scale4.ScaleY = 0.5;
            Scale5.ScaleY = 0.3;
        }
    }
}
