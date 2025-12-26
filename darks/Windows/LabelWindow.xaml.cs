using darks.Classes;
using System;
using System.Windows;

namespace darks.Windows
{
    public partial class LabelWindow : Window
    {
        public LabelWindow(Order order)
        {
            InitializeComponent();
            TxtOrderId.Text = $"ЗАКАЗ #{order.Id}";
            TxtDate.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}