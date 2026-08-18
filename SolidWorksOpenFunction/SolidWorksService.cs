using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;

namespace SolidWorksOpenFunction
{
	public static class SolidWorksService
	{
		public class InstalledVersion
		{
			public string Year;
			public string ExePath;
			public string ProductName;
			public string ProductVersion;
		}

		public class RunningInstance
		{
			public int PID;
			public string Year;
			public string ProductName;
			public string Build;
			public string Path;
		}

		public static List<SwInstanceInfo> FindInstances()
		{
			var instances = new List<SwInstanceInfo>();
			instances.AddRange(ToInstalledInstanceInfo());
			instances.AddRange(ToRunningInstanceInfo());
			return instances;
		}

		private static List<SwInstanceInfo> ToInstalledInstanceInfo()
		{
			var installed = new List<SwInstanceInfo>();
			foreach (var version in GetInstalledVersions())
			{
				int year;
				if (!int.TryParse(version.Year, out year))
				{
					year = 0;
				}

				installed.Add(new SwInstanceInfo(year, version.ExePath, null));
			}

			installed.Sort((a, b) => a.Year.CompareTo(b.Year));
			return installed;
		}

		private static List<SwInstanceInfo> ToRunningInstanceInfo()
		{
			const int versionYearOffset = 1992;
			var running = new List<SwInstanceInfo>();

			Process[] processes;
			try
			{
				processes = Process.GetProcessesByName("SLDWORKS");
			}
			catch
			{
				return running;
			}

			foreach (Process process in processes)
			{
				using (process)
				{
					int year = 0;
					string exePath = null;

					try
					{
						exePath = process.MainModule != null ? process.MainModule.FileName : null;
						if (exePath != null)
						{
							int major = FileVersionInfo.GetVersionInfo(exePath).FileMajorPart;
							if (major > 0)
							{
								year = major + versionYearOffset;
							}
						}
					}
					catch
					{
					}

					running.Add(new SwInstanceInfo(year, exePath, process.Id));
				}
			}

			running.Sort((a, b) =>
			{
				int yearCompare = a.Year.CompareTo(b.Year);
				if (yearCompare != 0)
				{
					return yearCompare;
				}

				int aPid = a.ProcessId ?? 0;
				int bPid = b.ProcessId ?? 0;
				return aPid.CompareTo(bPid);
			});

			return running;
		}

		public static List<InstalledVersion> GetInstalledVersions()
		{
			var list = new List<InstalledVersion>();
			var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string[] valueNames = { "SolidWorks Folder", "InstallDir" };

			foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
			{
				foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
				{
					try
					{
						using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
						using (var swKey = baseKey.OpenSubKey(@"SOFTWARE\SolidWorks"))
						{
							if (swKey == null) continue;

							foreach (var subKeyName in swKey.GetSubKeyNames())
							{
								if (!subKeyName.StartsWith("SOLIDWORKS ", StringComparison.OrdinalIgnoreCase))
									continue;

								string year = subKeyName.Replace("SOLIDWORKS ", "").Trim();
								string setupKeyPath = $@"SOFTWARE\SolidWorks\{subKeyName}\Setup";
								using (var setupKey = baseKey.OpenSubKey(setupKeyPath))
								{
									if (setupKey == null) continue;

									string exePath = null;
									foreach (var valueName in valueNames)
									{
										var installDir = setupKey.GetValue(valueName) as string;
										if (string.IsNullOrWhiteSpace(installDir)) continue;
										var candidate = Path.Combine(installDir, "SLDWORKS.exe");
										if (File.Exists(candidate))
										{
											exePath = candidate;
											break;
										}
									}

									if (string.IsNullOrEmpty(exePath) || seenPaths.Contains(exePath))
										continue;
									seenPaths.Add(exePath);

									string productName = "SOLIDWORKS " + year;
									string productVersion = "<unknown>";
									try
									{
										var fvi = FileVersionInfo.GetVersionInfo(exePath);
										if (!string.IsNullOrEmpty(fvi.ProductName)) productName = fvi.ProductName;
										if (!string.IsNullOrEmpty(fvi.ProductVersion)) productVersion = fvi.ProductVersion;
									}
									catch { }

									list.Add(new InstalledVersion
									{
										Year = year,
										ExePath = exePath,
										ProductName = productName,
										ProductVersion = productVersion
									});
								}
							}
						}
					}
					catch { }
				}
			}

			return list;
		}

		public static List<RunningInstance> GetRunningInstances()
		{
			var list = new List<RunningInstance>();
			try
			{
				var q = new SelectQuery(
					"SELECT ProcessId, Name, ExecutablePath FROM Win32_Process WHERE Name='SLDWORKS.exe'");
				using (var searcher = new ManagementObjectSearcher(q))
				{
					foreach (ManagementObject mo in searcher.Get())
					{
						var pidObj = mo["ProcessId"];
						var path = mo["ExecutablePath"] as string;
						int pid = pidObj != null ? Convert.ToInt32(pidObj) : -1;
						string productName = "<unknown>";
						string build = "<unknown>";
						string year = "<unknown>";
						try
						{
							if (!string.IsNullOrEmpty(path))
							{
								var fvi = FileVersionInfo.GetVersionInfo(path);
								if (!string.IsNullOrEmpty(fvi.ProductName)) productName = fvi.ProductName;
								if (!string.IsNullOrEmpty(fvi.ProductVersion)) build = fvi.ProductVersion;
								var m = Regex.Match(productName ?? "", @"\b(20\d{2})\b");
								if (m.Success) year = m.Groups[1].Value;
							}
						}
						catch { }

						list.Add(new RunningInstance
						{
							PID = pid,
							Year = year,
							ProductName = productName,
							Build = build,
							Path = path ?? "<unknown>"
						});
					}
				}
			}
			catch { }

			return list;
		}
	}
}


