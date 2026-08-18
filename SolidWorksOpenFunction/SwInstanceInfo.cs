namespace SolidWorksOpenFunction
{
	public sealed class SwInstanceInfo
	{
		public SwInstanceInfo(int year, string exePath, int? processId)
		{
			Year = year;
			ExePath = exePath;
			ProcessId = processId;
		}

		public int Year { get; }

		public string ExePath { get; }

		public int? ProcessId { get; }

		public bool IsRunning => ProcessId.HasValue;

		public string Key => IsRunning ? "pid:" + ProcessId.Value : "year:" + Year;

		public string DisplayName
		{
			get
			{
				string name = Year > 0 ? "SOLIDWORKS " + Year : "SOLIDWORKS";
				return IsRunning ? name + " (PID " + ProcessId.Value + ")" : name;
			}
		}
	}
}
