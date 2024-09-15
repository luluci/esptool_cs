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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace esptool_cs
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        double tabHeight = 0;
        int tabPrevIndex = -1;

        public MainWindow()
        {
            InitializeComponent();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                vm.Init();
            }
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {

        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //var ctrl = sender as TabControl;
            //if (ctrl is null)
            //{
            //    return;
            //}

            //var h_diff = (tabHeight - ctrl.ActualHeight);
            //tabHeight = ctrl.ActualHeight;
            //switch (tabPrevIndex)
            //{
            //    case 0:
            //        this.Height += h_diff;
            //        break;

            //    case 1:
            //        this.Height += h_diff;
            //        break;
            //}
            //tabPrevIndex = ctrl.SelectedIndex;
        }

    }
}
