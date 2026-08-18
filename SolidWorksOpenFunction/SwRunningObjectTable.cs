using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using SolidWorks.Interop.sldworks;

namespace SolidWorksOpenFunction
{
	internal static class SwRunningObjectTable
	{
		private const string MonikerPrefix = "SolidWorks_PID_";

		public static ISldWorks FindByProcessId(int processId)
		{
			string monikerName = MonikerPrefix + processId;

			IBindCtx bindCtx = null;
			IRunningObjectTable rot = null;
			IEnumMoniker monikers = null;

			try
			{
				if (CreateBindCtx(0, out bindCtx) != 0)
				{
					return null;
				}

				bindCtx.GetRunningObjectTable(out rot);
				rot.EnumRunning(out monikers);

				var buffer = new IMoniker[1];
				while (monikers.Next(1, buffer, IntPtr.Zero) == 0)
				{
					IMoniker moniker = buffer[0];
					if (moniker == null)
					{
						continue;
					}

					try
					{
						string name = null;
						try
						{
							moniker.GetDisplayName(bindCtx, null, out name);
						}
						catch (Exception)
						{
						}

						if (!string.Equals(name, monikerName, StringComparison.OrdinalIgnoreCase))
						{
							continue;
						}

						object candidate;
						rot.GetObject(moniker, out candidate);

						var swApp = candidate as ISldWorks;
						if (swApp == null && candidate != null && Marshal.IsComObject(candidate))
						{
							Marshal.ReleaseComObject(candidate);
						}

						return swApp;
					}
					finally
					{
						Marshal.ReleaseComObject(moniker);
					}
				}

				return null;
			}
			finally
			{
				if (monikers != null) Marshal.ReleaseComObject(monikers);
				if (rot != null) Marshal.ReleaseComObject(rot);
				if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
			}
		}

		[DllImport("ole32.dll")]
		private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);
	}
}
