using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;
using System.Reflection;

namespace MyApp.Logging
{
	public class LoggingService
	{
		public static event EventHandler<string> LogMessageWritten;

		public static NLog.Logger ConfigureLogger(string logDirectory = null)
		{
			try
			{
				// Use provided log directory or default to DLL's directory
				string baseDirectory = logDirectory ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string logsFolder = Path.Combine(baseDirectory, "logs");

				// Ensure logs folder exists
				Directory.CreateDirectory(logsFolder);

				// Create a log file name with the current date
				string logFileName = $"logger_{DateTime.Now:yyyyMMdd}.log";
				string logFilePath = Path.Combine(logsFolder, logFileName);

				// Configure NLog
				var config = new LoggingConfiguration();
				var fileTarget = new FileTarget
				{
					FileName = logFilePath,
					Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}"
				};
				config.AddTarget("file", fileTarget);
				config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

				var customEventTarget = new CustomEventTarget
				{
					Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}"
				};
				config.AddTarget("customEvent", customEventTarget);
				config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, customEventTarget));

				LogManager.Configuration = config;

				// Subscribe to custom event target
				CustomEventTarget.LogMessageWritten += (sender, logMessage) =>
				{
					LogMessageWritten?.Invoke(sender, logMessage);
				};

				return LogManager.GetCurrentClassLogger();
			}
			catch (Exception ex)
			{
				// Handle configuration errors (e.g., log to console or throw)
				Console.WriteLine($"Failed to configure logger: {ex.Message}");
				throw;
			}
		}
	}

	// Example implementation of CustomEventTarget
	public class CustomEventTarget : TargetWithLayout
	{
		public static event EventHandler<string> LogMessageWritten;

		protected override void Write(LogEventInfo logEvent)
		{
			string logMessage = Layout.Render(logEvent);
			LogMessageWritten?.Invoke(this, logMessage);
		}
	}
}