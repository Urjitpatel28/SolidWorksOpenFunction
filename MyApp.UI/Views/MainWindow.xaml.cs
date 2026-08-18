using System.ComponentModel;
using System.Windows;

namespace MyApp.UI
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			Selector.Shutdown();
		}
	}
}
