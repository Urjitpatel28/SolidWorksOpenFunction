using MyApp.UI.ViewModels;
using SolidWorksOpenFunction;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MyApp.UI.Views
{
	public partial class SolidWorksSelector : UserControl
	{
		private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(2);

		private readonly SolidWorksSelectorViewModel _viewModel;
		private readonly SwWorker _worker;

		private CancellationTokenSource _connectCancellation;
		private DispatcherTimer _connectionWatchdog;
		private int _connectedProcessId;

		public SolidWorksSelector()
		{
			InitializeComponent();

			_viewModel = new SolidWorksSelectorViewModel();
			DataContext = _viewModel;

			_worker = new SwWorker(Dispatcher);

			_viewModel.ConnectRequested += (s, target) => Connect(target);
			_viewModel.DisconnectRequested += (s, e) => Disconnect();
		}

		public void Shutdown()
		{
			StopConnectionWatchdog();
			_connectCancellation?.Cancel();
			_viewModel.Dispose();
			_worker.Dispose();
			_connectCancellation?.Dispose();
		}

		private void InstanceComboBox_DropDownOpened(object sender, EventArgs e)
		{
			_viewModel.Refresh();
		}

		private void InstanceBringToFront_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;

			var element = sender as FrameworkElement;
			_viewModel.BringToFront(element?.DataContext as SwInstanceInfo);
		}

		private void InstanceBringToFront_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			e.Handled = true;
		}

		private void Connect(SwInstanceInfo target)
		{
			_connectCancellation?.Dispose();
			_connectCancellation = new CancellationTokenSource();

			var progress = new Progress<string>(message => _viewModel.ReportProgress(message));
			_worker.ConnectAsync(target, progress, _connectCancellation.Token, OnConnected);
		}

		private void OnConnected(SwConnectionResult result)
		{
			if (!result.Succeeded)
			{
				_viewModel.HandleConnectionFailed(result.Message);
				return;
			}

			_viewModel.HandleConnected(result.Message);
			StartConnectionWatchdog(result.ProcessId);
		}

		private void Disconnect()
		{
			StopConnectionWatchdog();
			_worker.DisconnectAsync(() => _viewModel.HandleDisconnected());
		}

		private void StartConnectionWatchdog(int processId)
		{
			StopConnectionWatchdog();

			if (processId <= 0)
			{
				return;
			}

			_connectedProcessId = processId;

			_connectionWatchdog = new DispatcherTimer { Interval = WatchdogInterval };
			_connectionWatchdog.Tick += OnConnectionWatchdogTick;
			_connectionWatchdog.Start();
		}

		private void StopConnectionWatchdog()
		{
			if (_connectionWatchdog != null)
			{
				_connectionWatchdog.Stop();
				_connectionWatchdog.Tick -= OnConnectionWatchdogTick;
				_connectionWatchdog = null;
			}

			_connectedProcessId = 0;
		}

		private void OnConnectionWatchdogTick(object sender, EventArgs e)
		{
			int pid = _connectedProcessId;

			if (pid == 0 || IsProcessAlive(pid))
			{
				return;
			}

			StopConnectionWatchdog();
			_worker.DisconnectAsync(() =>
				_viewModel.HandleConnectionLost("SOLIDWORKS (PID " + pid + ") was closed - disconnected."));
		}

		private static bool IsProcessAlive(int pid)
		{
			try
			{
				using (Process process = Process.GetProcessById(pid))
				{
					return !process.HasExited;
				}
			}
			catch (ArgumentException)
			{
				return false;
			}
			catch
			{
				return true;
			}
		}
	}
}
