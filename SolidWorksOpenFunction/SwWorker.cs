using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using NLog;

namespace SolidWorksOpenFunction
{
	public sealed class SwWorker : IDisposable
	{
		private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		private readonly Dispatcher _uiDispatcher;
		private readonly Thread _thread;
		private readonly ManualResetEventSlim _started = new ManualResetEventSlim(false);

		private Dispatcher _dispatcher;
		private SwConnection _connection;
		private IDisposable _messageFilter;
		private bool _disposed;

		public SwWorker(Dispatcher uiDispatcher)
		{
			_uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));

			_thread = new Thread(ThreadMain)
			{
				Name = "SOLIDWORKS",
				IsBackground = true
			};

			_thread.SetApartmentState(ApartmentState.STA);
			_thread.Start();
			_started.Wait();
		}

		private void ThreadMain()
		{
			_dispatcher = Dispatcher.CurrentDispatcher;
			_messageFilter = OleMessageFilter.Install();
			_started.Set();
			Dispatcher.Run();
		}

		public void ConnectAsync(
			SwInstanceInfo target,
			IProgress<string> progress,
			CancellationToken cancel,
			Action<SwConnectionResult> completed)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			if (completed == null)
			{
				throw new ArgumentNullException(nameof(completed));
			}

			_dispatcher.InvokeAsync(() =>
			{
				SwConnectionResult result;

				try
				{
					_connection?.Dispose();
					_connection = new SwConnection();

					result = _connection.Connect(target, progress, cancel);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Connecting to SOLIDWORKS failed unexpectedly");
					result = SwConnectionResult.Failed("Could not connect to SOLIDWORKS: " + ex.Message);
				}

				_uiDispatcher.InvokeAsync(() => completed(result));
			});
		}

		public void DisconnectAsync(Action completed)
		{
			_dispatcher.InvokeAsync(() =>
			{
				try
				{
					_connection?.Dispose();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Disconnecting from SOLIDWORKS failed unexpectedly");
				}

				_connection = null;

				if (completed != null)
				{
					_uiDispatcher.InvokeAsync(() => completed());
				}
			});
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			Dispatcher worker = _dispatcher;
			if (worker == null)
			{
				return;
			}

			DispatcherOperation teardown = worker.InvokeAsync(() =>
			{
				_connection?.Dispose();
				_connection = null;

				_messageFilter?.Dispose();
				_messageFilter = null;

				Dispatcher.CurrentDispatcher.InvokeShutdown();
			});

			PumpUntil(teardown.Task, ShutdownTimeout);

			if (!_thread.Join(TimeSpan.FromSeconds(2)))
			{
				Log.Debug("The SOLIDWORKS thread did not stop within the shutdown window.");
			}

			_started.Dispose();
		}

		private static void PumpUntil(Task task, TimeSpan timeout)
		{
			Dispatcher ui = Dispatcher.CurrentDispatcher;
			var frame = new DispatcherFrame();

			Action release = () => ui.BeginInvoke((Action)(() => frame.Continue = false));

			task.ContinueWith(_ => release(), TaskScheduler.Default);
			Task.Delay(timeout).ContinueWith(_ => release(), TaskScheduler.Default);

			Dispatcher.PushFrame(frame);
		}
	}
}
