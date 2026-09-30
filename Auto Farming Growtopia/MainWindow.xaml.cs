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

        private bool _suppress;

        private enum VirtualKey : uint
        {
            A = 0x41,
            D = 0x44,
            Space = 0x20
        }

        private enum FarmMode { None, Blocks, Others, Providers }

        // Timing (ms) — tune these if the game misses inputs.
        private const int PunchHold = 50;
        private const int StepHold = 50;
        private const int StepGap = 100;

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

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            FarmMode mode = FarmMode.None;

            if (block.IsChecked == true)
            {
                mode = FarmMode.Blocks;
            }
            else if (other.IsChecked == true)
            {
                mode = FarmMode.Others;
            }
            else if (provider.IsChecked == true)
            {
                mode = FarmMode.Providers;
            }

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

            _ = FarmAsync(mode);
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

        private async Task FarmAsync(FarmMode mode)
        {
            var token = _canceller.Token;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!IsWindow(_growtopiaHandle))
                    {
                        break;
                    }

                    switch (mode)
                    {
                        case FarmMode.Blocks:
                            KeyDown(movement);
                            KeyDown(VirtualKey.Space);

                            await Task.Delay(250, token);

                            KeyUp(movement);
                            KeyUp(VirtualKey.Space);
                            break;

                        case FarmMode.Others:
                            KeyDown(VirtualKey.Space);

                            await Task.Delay(500, token);

                            KeyDown(movement);

                            await Task.Delay(100, token);

                            KeyUp(movement);
                            KeyUp(VirtualKey.Space);
                            break;

                        case FarmMode.Providers:
                            // one punch
                            KeyDown(VirtualKey.Space);
                            await Task.Delay(PunchHold, token);
                            KeyUp(VirtualKey.Space);
                            await Task.Delay(StepGap, token);

                            // then three steps
                            for (int i = 0; i < 3; i++)
                            {
                                token.ThrowIfCancellationRequested();

                                KeyDown(movement);
                                await Task.Delay(StepHold, token);
                                KeyUp(movement);
                                await Task.Delay(StepGap, token);
                            }
                            break;

                        default:
                            KeyDown(movement);

                            await Task.Delay(250, token);

                            KeyUp(movement);
                            break;
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

            uint vk = (uint)key;
            uint scan = MapVirtualKey(vk, 0);

            // repeat count 1, scancode in bits 16-23
            IntPtr lParam = MakeLParam(1u | (scan << 16));

            PostMessage(_growtopiaHandle, WM_KEYDOWN, (IntPtr)vk, lParam);
        }

        private void KeyUp(VirtualKey key)
        {
            if (_growtopiaHandle == IntPtr.Zero)
                return;

            uint vk = (uint)key;
            uint scan = MapVirtualKey(vk, 0);

            // repeat count 1, scancode, previous-state bit 30, transition bit 31
            IntPtr lParam = MakeLParam(1u | (scan << 16) | (1u << 30) | (1u << 31));

            PostMessage(_growtopiaHandle, WM_KEYUP, (IntPtr)vk, lParam);
        }

        // Bit 31 puts the KEYUP value above int.MaxValue, which overflows when
        // IntPtr is 4 bytes (32-bit build). Reinterpret the bits instead.
        private static IntPtr MakeLParam(uint value)
        {
            return IntPtr.Size == 8
                ? new IntPtr((long)value)
                : new IntPtr(unchecked((int)value));
        }

        private void ReleaseKeys()
        {
            KeyUp(movement);
            KeyUp(VirtualKey.Space);
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppress) return;

            _suppress = true;
            foreach (var cb in new[] { block, other, provider })
            {
                if (!ReferenceEquals(cb, sender))
                {
                    cb.IsChecked = false;
                }
            }
            _suppress = false;
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