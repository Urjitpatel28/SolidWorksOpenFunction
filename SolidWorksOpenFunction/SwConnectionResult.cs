using SolidWorks.Interop.sldworks;

namespace SolidWorksOpenFunction
{
	public enum SwConnectionOutcome
	{
		Attached,
		Launched,
		NotInstalled,
		Unreachable,
		TimedOut,
		Failed
	}

	public sealed class SwConnectionResult
	{
		private SwConnectionResult(SwConnectionOutcome outcome, ISldWorks application, string message, int processId)
		{
			Outcome = outcome;
			Application = application;
			Message = message;
			ProcessId = processId;
		}

		public SwConnectionOutcome Outcome { get; }

		public ISldWorks Application { get; }

		public int ProcessId { get; }

		public string Message { get; }

		public bool Succeeded => Application != null;

		public bool WeStartedIt => Outcome == SwConnectionOutcome.Launched;

		public static SwConnectionResult Attached(ISldWorks app, string instanceName, int processId)
		{
			return new SwConnectionResult(
				SwConnectionOutcome.Attached, app, "Connected to " + instanceName + ".", processId);
		}

		public static SwConnectionResult Launched(ISldWorks app, string instanceName, int processId)
		{
			return new SwConnectionResult(
				SwConnectionOutcome.Launched, app, "Started " + instanceName + ".", processId);
		}

		public static SwConnectionResult Unreachable()
		{
			return new SwConnectionResult(
				SwConnectionOutcome.Unreachable,
				null,
				"SOLIDWORKS is running but won't accept a connection. This usually means one of the two " +
				"is running as administrator and the other isn't - start both the same way and try again.",
				0);
		}

		public static SwConnectionResult TimedOut()
		{
			return new SwConnectionResult(
				SwConnectionOutcome.TimedOut,
				null,
				"SOLIDWORKS didn't finish starting in time. Open it yourself, wait for it to load, then retry.",
				0);
		}

		public static SwConnectionResult Failed(string message)
		{
			return new SwConnectionResult(SwConnectionOutcome.Failed, null, message, 0);
		}
	}
}
