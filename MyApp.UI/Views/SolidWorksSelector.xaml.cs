using MyApp.UI.ViewModels;
using System.Windows.Controls;

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
	}
}
