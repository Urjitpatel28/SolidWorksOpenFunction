using MyApp.UI.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MyApp.UI.ViewModels
{
	public class SolidWorksSelectorViewModel : INotifyPropertyChanged
	{
		public SolidWorksSelectorViewModel()
		{
			SelectedVersion = SolidWorksVersions[0]; // Sets the first item as default
		}

		public ObservableCollection<VersionOption> SolidWorksVersions { get; } =
			new ObservableCollection<VersionOption>
			{
				new VersionOption { Display = "SOLIDWORKS 2024", VersionKey = "2024" },
				new VersionOption { Display = "SOLIDWORKS 2023", VersionKey = "2023" },
				new VersionOption { Display = "SOLIDWORKS 2023 - CURRENT", VersionKey = "2023", IsCurrent = true }
			};

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

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}