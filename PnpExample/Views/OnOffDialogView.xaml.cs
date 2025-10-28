using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PnpExample.Views
{
    /// <summary>
    /// Interaction logic for OnOffDialogView.xaml
    /// </summary>
    public partial class OnOffDialogView : Window
    {
        public enum OnOffResult { On, Off, Cancel }

        public OnOffResult Result { get; private set; } = OnOffResult.Cancel;

        public OnOffDialogView(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void OnButton_Click(object sender, RoutedEventArgs e)
        {
            Result = OnOffResult.On;
            DialogResult = true;
        }

        private void OffButton_Click(object sender, RoutedEventArgs e)
        {
            Result = OnOffResult.Off;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = OnOffResult.Cancel;
            DialogResult = false;
        }
    }
}
