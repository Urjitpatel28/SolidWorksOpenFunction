using MyApp.UI.Models;
using SolidWorksOpenFunction;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;

namespace MyApp.UI.ViewModels
{
	public class SolidWorksSelectorViewModel : INotifyPropertyChanged
	{
        public SolidWorksSelectorViewModel()
        {
            LoadVersions();
        }

        public ObservableCollection<VersionOption> SolidWorksVersions { get; } =
            new ObservableCollection<VersionOption>();

		private VersionOption _selectedVersion;
		public VersionOption SelectedVersion
		{
			get => _selectedVersion;
			set
			{
				if (_selectedVersion != value)
				{
					_selectedVersion = value;
					OnPropertyChanged();
					// run-on-selected logic here
				}
			}
		}

        private void LoadVersions()
        {
            SolidWorksVersions.Clear();

            var installed = SolidWorksService.GetInstalledVersions();
            var running = SolidWorksService.GetRunningInstances();

            var runningYears = running
                .Where(r => !string.IsNullOrWhiteSpace(r.Year))
                .Select(r => r.Year)
                .Distinct()
                .ToHashSet();

            // Sort installed by year desc if numeric
            var ordered = installed
                .OrderByDescending(v => {
                    int y; return int.TryParse(v.Year, out y) ? y : int.MinValue; })
                .ThenByDescending(v => v.Year)
                .ToList();

            foreach (var v in ordered)
            {
                bool isCurrent = runningYears.Contains(v.Year);
                var display = (isCurrent ? $"{v.ProductName} - CURRENT" : v.ProductName);
                SolidWorksVersions.Add(new VersionOption
                {
                    Display = display,
                    VersionKey = v.Year,
                    IsCurrent = isCurrent
                });
            }

            if (SolidWorksVersions.Count > 0)
            {
                SelectedVersion = SolidWorksVersions[0];
            }
        }

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}