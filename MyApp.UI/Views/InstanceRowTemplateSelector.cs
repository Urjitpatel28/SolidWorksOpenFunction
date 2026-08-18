using System.Windows;
using System.Windows.Controls;
using SolidWorksOpenFunction;

namespace MyApp.UI.Views
{
	public class InstanceRowTemplateSelector : DataTemplateSelector
	{
		public DataTemplate RowTemplate { get; set; }
		public DataTemplate SelectionTemplate { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			if (!(item is SwInstanceInfo))
			{
				return null;
			}

			return (container as FrameworkElement)?.TemplatedParent is ComboBoxItem
				? RowTemplate
				: SelectionTemplate;
		}
	}
}
