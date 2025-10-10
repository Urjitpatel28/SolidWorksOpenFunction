using MyApp.UI.ViewModels;
using System.Windows.Controls;
using System.Diagnostics;
using System.Linq;
using SolidWorksOpenFunction;

namespace MyApp.UI.Views
{
	/// <summary>
	/// Interaction logic for SolidWorksSelector.xaml
	/// </summary>
	public partial class SolidWorksSelector : UserControl
	{
		public SolidWorksSelector()
		{
			InitializeComponent();
			DataContext = new SolidWorksSelectorViewModel();

		}

		private void OnOpenClick(object sender, System.Windows.RoutedEventArgs e)
		{
			var vm = DataContext as SolidWorksSelectorViewModel;
			if (vm == null || vm.SelectedVersion == null) return;

			var running = SolidWorksService.GetRunningInstances();
			var match = running.FirstOrDefault(r => r.Year == vm.SelectedVersion.VersionKey);
			if (match != null && match.PID > 0)
			{
				// Focus running instance
				WindowHelper.BringWindowToFront(match.PID);
				return;
			}

			// Launch the selected version from installed list
			var installed = SolidWorksService.GetInstalledVersions();
			var chosen = installed.FirstOrDefault(v => v.Year == vm.SelectedVersion.VersionKey);
			if (chosen != null)
			{
				try { Process.Start(chosen.ExePath); } catch { }
			}
		}
	}
}
