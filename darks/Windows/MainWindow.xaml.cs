using darks.Classes;
using darks.Windows;
using Npgsql;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls; // Добавлено для Visibility
using System.Windows.Input;

namespace darks.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Отображаем данные пользователя
            TxtUser.Text = CurrentSession.User.FullName;
            TxtRole.Text = CurrentSession.User.Role.ToUpper();

            // Применяем права доступа
            ApplyRolePermissions();

            // Стартовая страница
            MainFrame.Navigate(new DashboardPage());
        }

        private void ApplyRolePermissions()
        {
            string role = CurrentSession.User.Role.ToLower();

            // 1. MANAGER (Админ) - видит всё, ничего не скрываем
            if (role == "manager") return;

            // 2. PICKER (Сборщик)
            if (role == "picker")
            {
                // Скрываем админские разделы
                BtnNavEmployees.Visibility = Visibility.Collapsed; // Управление штатом
                BtnNavSupply.Visibility = Visibility.Collapsed;    // Приемка поставок

                // Остальное доступно: Заказы, Карта, Товары, Смены
            }

            // 3. COURIER (Курьер)
            else if (role == "courier")
            {
                // Курьеру нужен минимум
                BtnNavMap.Visibility = Visibility.Collapsed;       // Ему не нужно искать товары на полках
                BtnNavProducts.Visibility = Visibility.Collapsed;  // Ему не нужно редактировать товары
                BtnNavSupply.Visibility = Visibility.Collapsed;
                BtnNavEmployees.Visibility = Visibility.Collapsed;

                // Доступно: Дашборд, Заказы (смотреть статус), Смены (ЗП), Настройки
            }

            // В методе ApplyRolePermissions() не забудьте настроить видимость:
            // ...
            else if (role == "courier")
            {
                // ...
                BtnNavDelivery.Visibility = Visibility.Visible;
                // ...
            }
        }

        // --- Навигация ---
        private void Nav_Dashboard(object sender, RoutedEventArgs e) => MainFrame.Navigate(new DashboardPage());
        private void Nav_Orders(object sender, RoutedEventArgs e) => MainFrame.Navigate(new OrdersPage());
        private void Nav_Delivery(object sender, RoutedEventArgs e) => MainFrame.Navigate(new DeliveryPage());
        private void Nav_Products(object sender, RoutedEventArgs e) => MainFrame.Navigate(new ProductsPage());
        private void Nav_Supply(object sender, RoutedEventArgs e) => MainFrame.Navigate(new SupplyPage());
        private void Nav_Employees(object sender, RoutedEventArgs e) => MainFrame.Navigate(new EmployeesPage());
        private void Nav_Shifts(object sender, RoutedEventArgs e) => MainFrame.Navigate(new ShiftsPage());
        private void Nav_Settings(object sender, RoutedEventArgs e) => MainFrame.Navigate(new SettingsPage());
        private void Nav_Map(object sender, RoutedEventArgs e) => MainFrame.Navigate(new MapPage());

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            this.Close();
        }

        // --- Уведомления ---
        private void BtnNotif_Click(object sender, RoutedEventArgs e)
        {
            PopNotif.IsOpen = true;
            LoadNotifications();
        }

        private void LoadNotifications()
        {
            var list = new List<Notification>();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = "SELECT title, message FROM notifications ORDER BY created_at DESC LIMIT 10";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(new Notification { Title = r["title"].ToString(), Message = r["message"].ToString() });
                    }
                }
                ListNotifs.ItemsSource = list;
            }
            catch { }
        }

        // --- Глобальный поиск ---
        private void TxtGlobalSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string query = TxtGlobalSearch.Text.Trim();
                if (string.IsNullOrEmpty(query)) return;

                if (int.TryParse(query, out int orderId))
                {
                    Nav_Orders(null, null);
                    MessageBox.Show($"Поиск заказа #{orderId}");
                }
                else
                {
                    // Если курьер ищет товар, а доступа нет - предупредим или перенаправим
                    if (CurrentSession.User.Role == "courier")
                    {
                        MessageBox.Show("У вас нет доступа к базе товаров.");
                        return;
                    }

                    Nav_Products(null, null);
                    MessageBox.Show($"Поиск товара '{query}'");
                }
                TxtGlobalSearch.Clear();
            }
        }
    }
}