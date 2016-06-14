using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

namespace JMMServer
{
	/// <summary>
	/// Interaction logic for HyperLinkStandard.xaml
	/// </summary>
	public partial class HyperLinkStandard : UserControl
	{
		public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register("DisplayText",
			typeof(string), typeof(HyperLinkStandard), new UIPropertyMetadata("", displayTextChangedCallback));

		public static readonly DependencyProperty URLProperty = DependencyProperty.Register("URL",
			typeof(string), typeof(HyperLinkStandard), new UIPropertyMetadata("", urlChangedCallback));

		public string DisplayText
		{
			get { return (string)GetValue(DisplayTextProperty); }
			set { SetValue(DisplayTextProperty, value); }
		}

		private static void displayTextChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			HyperLinkStandard input = (HyperLinkStandard)d;
			input.txtLink.Text = e.NewValue as string;
		}

		public string URL
		{
			get { return (string)GetValue(URLProperty); }
			set { SetValue(URLProperty, value); }
		}

		private static void urlChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			//HyperLinkStandard input = (HyperLinkStandard)d;
			
		}

		public HyperLinkStandard()
		{
			InitializeComponent();

			hlURL.Click += new RoutedEventHandler(hlURL_Click);
		}

		void hlURL_Click(object sender, RoutedEventArgs e)
		{
			Uri uri = new Uri(URL);
			Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
			e.Handled = true;
		}
	}
}
