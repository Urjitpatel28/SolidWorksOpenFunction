using Microsoft.Win32;
using MyApp.Logging;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;

namespace SolidWorksOpenFunction
{
	internal class Program
	{
		public static Logger logger = LoggingService.ConfigureLogger();

		static void Main(string[] args)
		{

			// 1) Installed versions
			var installed = GetInstalledSolidWorksVersions();
			Console.WriteLine("== INSTALLED SOLIDWORKS VERSIONS ==");
			//logger.Warn("== INSTALLED SOLIDWORKS VERSIONS ==");
			if (installed.Count == 0)

			{
				Console.WriteLine("No installations found via registry.");
			}
			else
			{
				// Sort by numeric year desc (fallback to string)
				installed.Sort((a, b) =>
				{
					int ay, by;
					bool ap = int.TryParse(a.Year, out ay);
					bool bp = int.TryParse(b.Year, out by);
					if (ap && bp) return by.CompareTo(ay);
					return string.Compare(b.Year, a.Year, StringComparison.OrdinalIgnoreCase);
				});

				foreach (var v in installed)
				{
					Console.WriteLine(
						$"Year: {v.Year,-6} Product: {v.ProductName,-18} Build: {v.ProductVersion,-12} Path: {v.ExePath}");
				}
			}

			Console.WriteLine();

			// 2) Running instances
			var running = GetRunningSolidWorksInstances();
			Console.WriteLine("== RUNNING SOLIDWORKS INSTANCES ==");
			if (running.Count == 0)
			{
				Console.WriteLine("No running instances found.");
			}
			else
			{
				foreach (var r in running)
				{
					Console.WriteLine(
						$"PID: {r.PID,-7} Year: {r.Year,-6} Product: {r.ProductName,-18} Build: {r.Build,-12} Path: {r.Path}");
				}
			}

			Console.WriteLine();
			Console.WriteLine($"Found {running.Count} SolidWorks instance(s).");

			// Get the window upfront
			Console.Write($"Enter PID : ");
			int pid = Convert.ToInt32(Console.ReadLine());
			bool result = WindowHelper.BringWindowToFront(pid);
			Console.WriteLine($"Operation {(result ? "succeeded" : "failed")}.");
		}

		// -------- Models --------
		class SWInstall
		{
			public string Year;
			public string ExePath;
			public string ProductName;
			public string ProductVersion;
		}

		class SWInstance
		{
			public int PID;
			public string Year;
			public string ProductName;
			public string Build;
			public string Path;
		}

		// -------- Installed (Registry) --------
		// Reads both HKLM and HKCU, in both 64-bit and 32-bit views, and checks:
		// SOFTWARE\SolidWorks\SOLIDWORKS <YEAR>\Setup\{ "SolidWorks Folder" | "InstallDir" }
		private static List<SWInstall> GetInstalledSolidWorksVersions()
		{
			var list = new List<SWInstall>();
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
										var v = setupKey.GetValue(valueName) as string;
										if (string.IsNullOrWhiteSpace(v)) continue;

										var candidate = Path.Combine(v, "SLDWORKS.exe");
										if (File.Exists(candidate))
										{
											exePath = candidate;
											break;
										}
									}

									if (string.IsNullOrEmpty(exePath) || seenPaths.Contains(exePath))
										continue;

									seenPaths.Add(exePath);

									// Read file version info from disk
									string productName = "SOLIDWORKS " + year;
									string productVersion = "<unknown>";
									try
									{
										var fvi = FileVersionInfo.GetVersionInfo(exePath);
										if (!string.IsNullOrEmpty(fvi.ProductName)) productName = fvi.ProductName;
										if (!string.IsNullOrEmpty(fvi.ProductVersion)) productVersion = fvi.ProductVersion;
									}
									catch { /* ignore */ }

									list.Add(new SWInstall
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
					catch
					{
						// Ignore view/hive access errors; continue scanning others.
					}
				}
			}

			return list;
		}

		// -------- Running (WMI) --------
		// Uses Win32_Process to get PID + ExecutablePath (works from 32-bit to inspect 64-bit processes).
		// Then reads FileVersionInfo from disk and extracts year.
		private static List<SWInstance> GetRunningSolidWorksInstances()
		{
			var list = new List<SWInstance>();
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
						catch
						{
							// Safe fallback if file is locked or inaccessible
						}

						list.Add(new SWInstance
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
			catch (Exception ex)
			{
				Console.WriteLine("WMI query failed: " + ex.Message);
			}

			return list;
		}
	}
}