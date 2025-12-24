using System.Diagnostics;
using System.Windows.Navigation;
using DaxStudio.UI.Controls;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for VisualQueryPlanView.xaml
    /// </summary>
    public partial class VisualQueryPlanView : ZoomableUserControl
    {
        public VisualQueryPlanView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens hyperlinks in the default browser.
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
