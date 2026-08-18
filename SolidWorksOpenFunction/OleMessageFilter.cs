using System;
using System.Runtime.InteropServices;
using NLog;

namespace SolidWorksOpenFunction
{
	internal sealed class OleMessageFilter : IOleMessageFilter
	{
		private const int ServerCallIsHandled = 0;
		private const int ServerCallRetryLater = 2;
		private const int CancelCall = -1;
		private const int RetryDelayMs = 200;
		private const int PendingMsgWaitDefProcess = 2;

		private static readonly TimeSpan RetryTimeout = TimeSpan.FromMinutes(5);
		private static readonly TimeSpan LogAfter = TimeSpan.FromSeconds(5);

		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		private readonly IOleMessageFilter _previous;
		private bool _loggedThisCall;

		private OleMessageFilter(IOleMessageFilter previous)
		{
			_previous = previous;
		}

		public static IDisposable Install()
		{
			IOleMessageFilter previous;
			int hr = CoRegisterMessageFilter(new OleMessageFilter(null), out previous);
			if (hr != 0)
			{
				Log.Debug("Could not register the COM message filter (hr=0x" + hr.ToString("X8") + ").");
				return new Revoker(null, registered: false);
			}

			return new Revoker(previous, registered: true);
		}

		public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
		{
			return ServerCallIsHandled;
		}

		public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
		{
			if (dwRejectType != ServerCallRetryLater)
			{
				_loggedThisCall = false;
				return CancelCall;
			}

			if (dwTickCount > RetryTimeout.TotalMilliseconds)
			{
				Log.Debug("Gave up on a call SOLIDWORKS kept refusing for " + (dwTickCount / 1000) + "s.");
				_loggedThisCall = false;
				return CancelCall;
			}

			if (!_loggedThisCall && dwTickCount > LogAfter.TotalMilliseconds)
			{
				_loggedThisCall = true;
				Log.Debug("SOLIDWORKS is busy - retrying. If this persists it usually has a dialog open.");
			}

			return RetryDelayMs;
		}

		public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
		{
			return PendingMsgWaitDefProcess;
		}

		private sealed class Revoker : IDisposable
		{
			private readonly IOleMessageFilter _previous;
			private readonly bool _registered;
			private bool _disposed;

			public Revoker(IOleMessageFilter previous, bool registered)
			{
				_previous = previous;
				_registered = registered;
			}

			public void Dispose()
			{
				if (_disposed || !_registered)
				{
					return;
				}

				_disposed = true;

				IOleMessageFilter ignored;
				CoRegisterMessageFilter(_previous, out ignored);
			}
		}

		[DllImport("ole32.dll")]
		private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);
	}

	[ComImport]
	[Guid("00000016-0000-0000-C000-000000000046")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	internal interface IOleMessageFilter
	{
		[PreserveSig]
		int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

		[PreserveSig]
		int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

		[PreserveSig]
		int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
	}
}
