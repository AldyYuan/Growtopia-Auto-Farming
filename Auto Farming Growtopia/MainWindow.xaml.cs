using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Auto_Farming_Growtopia
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _canceller;

    private IntPtr _growtopiaHandle = IntPtr.Zero;

        private VirtualKey movement = VirtualKey.D;

        private enum VirtualKey : uint
        {
            A = 0x41,
            D = 0x44,
            Space = 0x20
        }

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;

        [DllImport("user32.dll")]
        private static extern bool PostMessage(
            IntPtr hWnd,
            uint Msg,
            IntPtr wParam,
            IntPtr lParam
        );

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            bool blocks = block.IsChecked == true;
            bool others = other.IsChecked == true;

            if (left.IsChecked == true)
            {
                movement = VirtualKey.A;
            }
            else if (right.IsChecked == true)
            {
                movement = VirtualKey.D;
            }

            var process = Process.GetProcessesByName("Growtopia")
                .FirstOrDefault();

            if (process == null || process.MainWindowHandle == IntPtr.Zero)
            {
                MessageBox.Show(
                    "Open Growtopia and enter your world first.",
                    "Growtopia Auto Farming",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            _growtopiaHandle = process.MainWindowHandle;

            _canceller = new CancellationTokenSource();

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;

            _ = FarmAsync(blocks, others);
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            await StopAsync();
        }

        private async Task StopAsync()
        {
            if (_canceller != null)
            {
                _canceller.Cancel();
            }

            ReleaseKeys();

            btnStop.IsEnabled = false;
            btnStart.IsEnabled = true;

            await Task.Delay(100);
        }

        private async Task FarmAsync(bool blocks, bool others)
        {
            try
            {
                while (!_canceller.Token.IsCancellationRequested)
                {
                    if (!IsWindow(_growtopiaHandle))
                    {
                        break;
                    }

                    if (blocks)
                    {
                        KeyDown(movement);
                        KeyDown(VirtualKey.Space);

                        await Task.Delay(250, _canceller.Token);

                        KeyUp(movement);
                        KeyUp(VirtualKey.Space);
                    }
                    else if (others)
                    {
                        KeyDown(VirtualKey.Space);

                        await Task.Delay(500, _canceller.Token);

                        KeyDown(movement);

                        await Task.Delay(100, _canceller.Token);

                        KeyUp(movement);
                        KeyUp(VirtualKey.Space);
                    }
                    else
                    {
                        KeyDown(movement);

                        await Task.Delay(250, _canceller.Token);

                        KeyUp(movement);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping.
            }
            finally
            {
                ReleaseKeys();

                Dispatcher.Invoke(() =>
                {
                    btnStop.IsEnabled = false;
                    btnStart.IsEnabled = true;
                });
            }
        }

        private void KeyDown(VirtualKey key)
        {
            if (_growtopiaHandle == IntPtr.Zero)
                return;

            PostMessage(
                _growtopiaHandle,
                WM_KEYDOWN,
                (IntPtr)key,
                IntPtr.Zero
            );
        }

        private void KeyUp(VirtualKey key)
        {
            if (_growtopiaHandle == IntPtr.Zero)
                return;

            PostMessage(
                _growtopiaHandle,
                WM_KEYUP,
                (IntPtr)key,
                IntPtr.Zero
            );
        }

        private void ReleaseKeys()
        {
            KeyUp(movement);
            KeyUp(VirtualKey.Space);
        }

        private void block_Checked(object sender, RoutedEventArgs e)
        {
            other.IsChecked = false;
        }

        private void other_Checked(object sender, RoutedEventArgs e)
        {
            block.IsChecked = false;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if ((bool)Properties.Settings.Default["FirstRun"] == true)
            {
                Properties.Settings.Default["FirstRun"] = false;
                Properties.Settings.Default.Save();

                MessageBox.Show(
                    "You must allow Spacebar for punch in Growtopia first",
                    "Growtopia Auto Farming Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
    }

}
