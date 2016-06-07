using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace JMMServer
{
    /// <summary>
    ///     Interaction logic for HyperLinkStandard.xaml
    /// </summary>
    public partial class HyperLinkStandard : UserControl
    {
        public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register("DisplayText",
            typeof(string), typeof(HyperLinkStandard), new UIPropertyMetadata("", displayTextChangedCallback));

        public static readonly DependencyProperty URLProperty = DependencyProperty.Register("URL",
            typeof(string), typeof(HyperLinkStandard), new UIPropertyMetadata("", urlChangedCallback));

        public HyperLinkStandard()
        {
            InitializeComponent();

            hlURL.Click += hlURL_Click;
        }

        public string DisplayText
        {
            get { return (string)GetValue(DisplayTextProperty); }
            set { SetValue(DisplayTextProperty, value); }
        }

        public string URL
        {
            get { return (string)GetValue(URLProperty); }
            set { SetValue(URLProperty, value); }
        }

        private static void displayTextChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var input = (HyperLinkStandard)d;
            input.txtLink.Text = e.NewValue as string;
        }

        private static void urlChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            //HyperLinkStandard input = (HyperLinkStandard)d;
        }

        private void hlURL_Click(object sender, RoutedEventArgs e)
        {
            var uri = new Uri(URL);
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}