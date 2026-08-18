using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using NLog;
using SolidWorks.Interop.sldworks;

namespace SolidWorksOpenFunction
{
	public sealed class SwConnection : IDisposable
	{
		private const string ProcessName = "SLDWORKS";

		private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
		private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(10);
		private static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(8);
		private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		private ISldWorks _swApp;
		private bool _weStartedIt;
		private int _processId;

		public ISldWorks Application => _swApp;

		public SwConnectionResult Connect(SwInstanceInfo target, IProgress<string> progress, CancellationToken cancel)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			return target.IsRunning
				? Attach(target, progress, cancel)
				: Launch(target, progress, cancel);
		}

		private SwConnectionResult Attach(SwInstanceInfo target, IProgress<string> progress, CancellationToken cancel)
		{
			int pid = target.ProcessId.Value;

			Report(progress, "Connecting to " + target.DisplayName + "...");

			var clock = Stopwatch.StartNew();
			ISldWorks swApp = null;

			while (swApp == null && clock.Elapsed < AttachTimeout && !cancel.IsCancellationRequested)
			{
				swApp = SwRunningObjectTable.FindByProcessId(pid);

				if (swApp == null)
				{
					if (!IsProcessAlive(pid))
					{
						Log.Info("SOLIDWORKS (PID " + pid + ") exited before we could attach.");
						return SwConnectionResult.Failed(
							"That SOLIDWORKS instance is no longer running. Pick another from the list.");
					}

					cancel.WaitHandle.WaitOne(PollInterval);
				}
			}

			if (swApp == null)
			{
				if (cancel.IsCancellationRequested)
				{
					return SwConnectionResult.Failed("Cancelled while connecting to SOLIDWORKS.");
				}

				Log.Warn("SOLIDWORKS (PID " + pid + ") is running but has no reachable ROT entry.");
				return SwConnectionResult.Unreachable();
			}

			if (!WaitUntilReady(swApp, progress, cancel))
			{
				ReleaseComObject(swApp);
				return cancel.IsCancellationRequested
					? SwConnectionResult.Failed("Cancelled while connecting to SOLIDWORKS.")
					: SwConnectionResult.TimedOut();
			}

			_swApp = swApp;
			_weStartedIt = false;
			_processId = pid;

			Log.Info("Attached to " + target.DisplayName);
			return SwConnectionResult.Attached(swApp, target.DisplayName, pid);
		}

		private SwConnectionResult Launch(SwInstanceInfo target, IProgress<string> progress, CancellationToken cancel)
		{
			if (string.IsNullOrEmpty(target.ExePath) || !File.Exists(target.ExePath))
			{
				return SwConnectionResult.Failed(
					"The " + target.DisplayName + " executable could not be found. Was it uninstalled?");
			}

			Report(progress, "Starting " + target.DisplayName + "...");

			Process process;
			try
			{
				process = Process.Start(new ProcessStartInfo(target.ExePath) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Could not start " + target.DisplayName);
				return SwConnectionResult.Failed("Could not start " + target.DisplayName + ": " + ex.Message);
			}

			if (process == null)
			{
				return SwConnectionResult.Failed(target.DisplayName + " did not start.");
			}

			using (process)
			{
				int pid = process.Id;
				var clock = Stopwatch.StartNew();
				bool saidWaiting = false;
				ISldWorks swApp = null;

				while (swApp == null && clock.Elapsed < StartupTimeout)
				{
					if (cancel.IsCancellationRequested)
					{
						return SwConnectionResult.Failed("Cancelled while starting SOLIDWORKS.");
					}

					if (process.HasExited)
					{
						Log.Warn(target.DisplayName + " exited during startup (code " + process.ExitCode + ").");
						return SwConnectionResult.Failed(
							target.DisplayName + " started and then closed itself - check its own error messages.");
					}

					swApp = SwRunningObjectTable.FindByProcessId(pid);

					if (swApp == null)
					{
						if (!saidWaiting && clock.Elapsed > TimeSpan.FromSeconds(5))
						{
							saidWaiting = true;
							Report(progress, "Waiting for SOLIDWORKS to finish starting...");
						}

						cancel.WaitHandle.WaitOne(PollInterval);
					}
				}

				if (swApp == null)
				{
					Log.Warn(target.DisplayName + " did not register in the ROT within "
						+ StartupTimeout.TotalMinutes + " minutes.");
					return SwConnectionResult.TimedOut();
				}

				if (!WaitUntilReady(swApp, progress, cancel))
				{
					ReleaseComObject(swApp);
					return cancel.IsCancellationRequested
						? SwConnectionResult.Failed("Cancelled while starting SOLIDWORKS.")
						: SwConnectionResult.TimedOut();
				}

				_swApp = swApp;
				_weStartedIt = true;
				_processId = pid;

				Log.Info("Started " + target.DisplayName + " (PID " + pid + ")");
				return SwConnectionResult.Launched(swApp, target.DisplayName + " (PID " + pid + ")", pid);
			}
		}

		private static bool WaitUntilReady(ISldWorks swApp, IProgress<string> progress, CancellationToken cancel)
		{
			var clock = Stopwatch.StartNew();
			bool saidWaiting = false;

			while (clock.Elapsed < StartupTimeout)
			{
				if (cancel.IsCancellationRequested)
				{
					return false;
				}

				try
				{
					swApp.Visible = true;

					if (!string.IsNullOrEmpty(swApp.RevisionNumber()))
					{
						return true;
					}
				}
				catch (COMException)
				{
				}

				if (!saidWaiting && clock.Elapsed > TimeSpan.FromSeconds(5))
				{
					saidWaiting = true;
					Report(progress, "Waiting for SOLIDWORKS to finish starting...");
				}

				cancel.WaitHandle.WaitOne(PollInterval);
			}

			Log.Warn("SOLIDWORKS did not become ready within " + StartupTimeout.TotalMinutes + " minutes.");
			return false;
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
			catch (Exception)
			{
				return true;
			}
		}

		private static void Report(IProgress<string> progress, string message)
		{
			progress?.Report(message);
		}

		public void Release()
		{
			ISldWorks swApp = _swApp;
			_swApp = null;

			ReleaseComObject(swApp);
		}

		public void Dispose()
		{
			ISldWorks swApp = _swApp;
			bool weStartedIt = _weStartedIt;
			int pid = _processId;

			_weStartedIt = false;
			_processId = 0;

			if (swApp == null)
			{
				return;
			}

			if (weStartedIt)
			{
				try
				{
					swApp.ExitApp();
					Log.Info("Asked the SOLIDWORKS we started (PID " + pid + ") to close");
				}
				catch (Exception ex)
				{
					Log.Debug(ex, "SOLIDWORKS did not accept ExitApp cleanly.");
				}
			}

			Release();

			if (weStartedIt && pid != 0)
			{
				EnsureProcessExited(pid);
			}
		}

		private static void EnsureProcessExited(int pid)
		{
			try
			{
				using (Process process = Process.GetProcessById(pid))
				{
					if (!string.Equals(process.ProcessName, ProcessName, StringComparison.OrdinalIgnoreCase))
					{
						return;
					}

					if (process.WaitForExit((int)ExitGracePeriod.TotalMilliseconds))
					{
						return;
					}

					Log.Warn("SOLIDWORKS (PID " + pid + ") did not close within "
						+ ExitGracePeriod.TotalSeconds + "s of ExitApp - killing it.");
					process.Kill();
				}
			}
			catch (ArgumentException)
			{
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Could not confirm the SOLIDWORKS we started has exited.");
			}
		}

		private static void ReleaseComObject(ISldWorks swApp)
		{
			if (swApp != null && Marshal.IsComObject(swApp))
			{
				Marshal.FinalReleaseComObject(swApp);
			}
		}
	}
}
