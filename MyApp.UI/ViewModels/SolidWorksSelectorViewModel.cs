using SolidWorksOpenFunction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace MyApp.UI.ViewModels
{
	public enum SwConnectionState
	{
		Disconnected,
		Connecting,
		Connected,
		Disconnecting
	}

	public enum SwStatusKind
	{
		Info,
		Success,
		Error
	}

	public sealed class SolidWorksSelectorViewModel : INotifyPropertyChanged, IDisposable
	{
		private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);

		private readonly DispatcherTimer _refreshTimer;
		private SwInstanceInfo _selectedInstance;
		private SwConnectionState _state;
		private string _statusMessage;
		private SwStatusKind _statusKind;

		public SolidWorksSelectorViewModel()
		{
			ConnectOrDisconnectCommand = new RelayCommand(ConnectOrDisconnect, CanConnectOrDisconnect);
			Refresh();

			_refreshTimer = new DispatcherTimer { Interval = RefreshInterval };
			_refreshTimer.Tick += (s, e) => Refresh();
			_refreshTimer.Start();
		}

		public ObservableCollection<SwInstanceInfo> Instances { get; } =
			new ObservableCollection<SwInstanceInfo>();

		public SwInstanceInfo SelectedInstance
		{
			get => _selectedInstance;
			set
			{
				if (_selectedInstance != value)
				{
					_selectedInstance = value;
					OnPropertyChanged();
					ConnectOrDisconnectCommand.RaiseCanExecuteChanged();
				}
			}
		}

		public SwConnectionState State
		{
			get => _state;
			private set
			{
				if (_state != value)
				{
					_state = value;
					OnPropertyChanged();
					OnPropertyChanged(nameof(CanPick));
					ConnectOrDisconnectCommand.RaiseCanExecuteChanged();
				}
			}
		}

		public bool CanPick => State == SwConnectionState.Disconnected;

		public string StatusMessage
		{
			get => _statusMessage;
			private set
			{
				if (_statusMessage != value)
				{
					_statusMessage = value;
					OnPropertyChanged();
				}
			}
		}

		public bool StatusIsError => _statusKind == SwStatusKind.Error;

		public bool StatusIsSuccess => _statusKind == SwStatusKind.Success;

		public RelayCommand ConnectOrDisconnectCommand { get; }

		public event EventHandler<SwInstanceInfo> ConnectRequested;
		public event EventHandler DisconnectRequested;

		private bool CanConnectOrDisconnect()
		{
			return State == SwConnectionState.Connected
				|| (State == SwConnectionState.Disconnected && SelectedInstance != null);
		}

		private void ConnectOrDisconnect()
		{
			if (State == SwConnectionState.Connected)
			{
				State = SwConnectionState.Disconnecting;
				SetStatus("Disconnecting...", SwStatusKind.Info);
				DisconnectRequested?.Invoke(this, EventArgs.Empty);
			}
			else if (State == SwConnectionState.Disconnected && SelectedInstance != null)
			{
				State = SwConnectionState.Connecting;
				SetStatus("Connecting to " + SelectedInstance.DisplayName + "...", SwStatusKind.Info);
				ConnectRequested?.Invoke(this, SelectedInstance);
			}
		}

		public void ReportProgress(string message)
		{
			SetStatus(message, SwStatusKind.Info);
		}

		public void HandleConnected(string message)
		{
			State = SwConnectionState.Connected;
			SetStatus(message, SwStatusKind.Success);
			Refresh();
		}

		public void HandleConnectionFailed(string message)
		{
			State = SwConnectionState.Disconnected;
			SetStatus(message, SwStatusKind.Error);
			Refresh();
		}

		public void HandleDisconnected()
		{
			State = SwConnectionState.Disconnected;
			SetStatus("Disconnected.", SwStatusKind.Error);
			Refresh();
		}

		public void HandleConnectionLost(string message)
		{
			State = SwConnectionState.Disconnected;
			SetStatus(message, SwStatusKind.Error);
			Refresh();
		}

		public void Refresh()
		{
			IReadOnlyList<SwInstanceInfo> found;
			try
			{
				found = SolidWorksService.FindInstances();
			}
			catch
			{
				return;
			}

			if (SameAsCurrent(found))
			{
				return;
			}

			string selectedKey = SelectedInstance != null ? SelectedInstance.Key : null;

			Instances.Clear();
			foreach (SwInstanceInfo instance in found)
			{
				Instances.Add(instance);
			}

			SwInstanceInfo reselected = selectedKey == null
				? null
				: found.FirstOrDefault(instance => instance.Key == selectedKey);

			SelectedInstance = reselected
				?? found.Where(instance => !instance.IsRunning)
						.OrderByDescending(instance => instance.Year)
						.FirstOrDefault()
				?? found.FirstOrDefault();

			if (found.Count == 0 && State == SwConnectionState.Disconnected)
			{
				SetStatus("No SOLIDWORKS installation was found on this machine.", SwStatusKind.Error);
			}
		}

		private bool SameAsCurrent(IReadOnlyList<SwInstanceInfo> found)
		{
			if (found.Count != Instances.Count)
			{
				return false;
			}

			for (int i = 0; i < found.Count; i++)
			{
				if (found[i].Key != Instances[i].Key)
				{
					return false;
				}
			}

			return true;
		}

		public void BringToFront(SwInstanceInfo instance)
		{
			if (instance == null || instance.ProcessId == null)
			{
				return;
			}

			WindowHelper.BringWindowToFront(instance.ProcessId.Value);
		}

		private void SetStatus(string message, SwStatusKind kind)
		{
			StatusMessage = message;

			if (_statusKind != kind)
			{
				_statusKind = kind;
				OnPropertyChanged(nameof(StatusIsError));
				OnPropertyChanged(nameof(StatusIsSuccess));
			}
		}

		public void Dispose()
		{
			_refreshTimer.Stop();
		}

		public event PropertyChangedEventHandler PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
