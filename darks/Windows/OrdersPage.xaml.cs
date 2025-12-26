using darks.Classes;
using darks.Windows;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace darks.Windows
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string EmployeeName { get; set; }
        public int ItemsCount { get; set; }
        public decimal TotalPrice { get; set; }

        public Brush StatusColor
        {
            get
            {
                switch (Status)
                {
                    case "new": return (Brush)new BrushConverter().ConvertFrom("#F1C40F");
                    case "picking": return (Brush)new BrushConverter().ConvertFrom("#E67E22");
                    case "ready": return (Brush)new BrushConverter().ConvertFrom("#2ECC71");
                    case "delivering": return (Brush)new BrushConverter().ConvertFrom("#3498DB"); // Синий для доставки
                    case "done": return (Brush)new BrushConverter().ConvertFrom("#95A5A6");
                    default: return Brushes.Gray;
                }
            }
        }
    }

    public class OrderItemViewModel
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public decimal Total => Price * Stock;
    }

    public partial class OrdersPage : Page
    {
        private string currentFilter = "all";
        private List<OrderViewModel> allOrders = new List<OrderViewModel>();
        private OrderViewModel selectedOrder;

        public OrdersPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Если курьер - показываем готовые и те, что в пути
            if (CurrentSession.User.Role == "courier") currentFilter = "ready";
            LoadOrders();
        }

        private void LoadOrders()
        {
            allOrders.Clear();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = @"
                        SELECT o.id, o.status, o.created_at, e.full_name,
                               COUNT(oi.id) as items_count,
                               COALESCE(SUM(p.price * oi.qty), 0) as total_price
                        FROM orders o
                        LEFT JOIN employees e ON o.employee_id = e.id
                        LEFT JOIN order_items oi ON o.id = oi.order_id
                        LEFT JOIN products p ON oi.product_id = p.id
                        GROUP BY o.id, e.full_name
                        ORDER BY o.created_at DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            allOrders.Add(new OrderViewModel
                            {
                                Id = (int)r["id"],
                                Status = r["status"].ToString(),
                                CreatedAt = Convert.ToDateTime(r["created_at"]),
                                EmployeeName = r["full_name"] == DBNull.Value ? "---" : r["full_name"].ToString(),
                                ItemsCount = Convert.ToInt32(r["items_count"]),
                                TotalPrice = Convert.ToDecimal(r["total_price"])
                            });
                        }
                    }
                }
                ApplyFilters();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        private void ApplyFilters()
        {
            string search = TxtSearch.Text.Trim().ToLower();

            var filtered = allOrders.Where(o =>
                (currentFilter == "all" || o.Status == currentFilter || (currentFilter == "ready" && o.Status == "delivering")) &&
                (o.Id.ToString().Contains(search))
            ).ToList();

            ListOrders.ItemsSource = filtered;
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            currentFilter = (sender as Button).Tag.ToString();
            ApplyFilters();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ListOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedOrder = ListOrders.SelectedItem as OrderViewModel;
            if (selectedOrder == null)
            {
                DetailsPanel.Visibility = Visibility.Hidden;
                TxtPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            DetailsPanel.Visibility = Visibility.Visible;
            TxtPlaceholder.Visibility = Visibility.Hidden;

            TxtOrderId.Text = $"Заказ #{selectedOrder.Id}";
            TxtOrderTotal.Text = $"{selectedOrder.TotalPrice:N0} ₽";

            UpdateStepper(selectedOrder.Status);
            LoadOrderItems(selectedOrder.Id);
            ConfigureActionButton();
        }

        private void UpdateStepper(string status)
        {
            var gray = (Brush)new BrushConverter().ConvertFrom("#E0E0E0");
            var grayCircle = (Brush)new BrushConverter().ConvertFrom("#BDC3C7");
            var activeColor = (Brush)new BrushConverter().ConvertFrom("#2ECC71");
            var activeOrange = (Brush)new BrushConverter().ConvertFrom("#E67E22");
            var activeBlue = (Brush)new BrushConverter().ConvertFrom("#3498DB");

            // Сброс
            StepNew.Background = grayCircle; StepPick.Background = grayCircle; StepReady.Background = grayCircle; StepDone.Background = grayCircle;
            Line1.Fill = gray; Line2.Fill = gray; Line3.Fill = gray;

            if (status == "new") { StepNew.Background = activeOrange; }
            else if (status == "picking") { StepNew.Background = activeColor; Line1.Fill = activeColor; StepPick.Background = activeOrange; }
            else if (status == "ready") { StepNew.Background = activeColor; Line1.Fill = activeColor; StepPick.Background = activeColor; Line2.Fill = activeColor; StepReady.Background = activeColor; }
            else if (status == "delivering") { StepNew.Background = activeColor; Line1.Fill = activeColor; StepPick.Background = activeColor; Line2.Fill = activeColor; StepReady.Background = activeColor; Line3.Fill = activeBlue; StepDone.Background = activeBlue; } // Синий пока едет
            else if (status == "done") { StepNew.Background = activeColor; Line1.Fill = activeColor; StepPick.Background = activeColor; Line2.Fill = activeColor; StepReady.Background = activeColor; Line3.Fill = activeColor; StepDone.Background = activeColor; }
        }

        private void LoadOrderItems(int orderId)
        {
            var items = new List<OrderItemViewModel>();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = @"SELECT p.name, p.price, oi.qty 
                                   FROM order_items oi
                                   JOIN products p ON oi.product_id = p.id
                                   WHERE oi.order_id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("id", orderId);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                items.Add(new OrderItemViewModel
                                {
                                    Name = r["name"].ToString(),
                                    Price = Convert.ToDecimal(r["price"]),
                                    Stock = Convert.ToInt32(r["qty"])
                                });
                            }
                        }
                    }
                }
                GridItems.ItemsSource = items;
            }
            catch { }
        }

        private void ConfigureActionButton()
        {
            BtnAction.Visibility = Visibility.Visible;
            BtnAction.Background = (Brush)new BrushConverter().ConvertFrom("#3498DB");

            if (selectedOrder.Status == "new")
            {
                BtnAction.Content = "Взять в работу (Сборка)";
                BtnAction.Background = (Brush)new BrushConverter().ConvertFrom("#E67E22");
            }
            else if (selectedOrder.Status == "picking")
            {
                BtnAction.Content = "Собрано (Готов к выдаче)";
                BtnAction.Background = (Brush)new BrushConverter().ConvertFrom("#2ECC71");
            }
            else if (selectedOrder.Status == "ready")
            {
                // ИЗМЕНЕНИЕ: Теперь это не "Завершить", а "Передать в доставку"
                BtnAction.Content = "Передать в доставку ->";
                BtnAction.Background = (Brush)new BrushConverter().ConvertFrom("#3498DB");
            }
            else
            {
                // Если статус delivering или done - сборщику тут делать нечего
                BtnAction.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrder == null) return;
            string newStatus = "";
            bool releaseEmployee = false;

            if (selectedOrder.Status == "new") newStatus = "picking";
            else if (selectedOrder.Status == "picking") newStatus = "ready";
            else if (selectedOrder.Status == "ready")
            {
                // ИЗМЕНЕНИЕ: Переводим в статус 'delivering'
                newStatus = "delivering";
                // И освобождаем заказ от сборщика (чтобы курьер мог его забрать)
                releaseEmployee = true;
            }

            if (!string.IsNullOrEmpty(newStatus))
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql;

                    if (releaseEmployee)
                    {
                        // Снимаем сборщика, ставим статус "в пути"
                        sql = "UPDATE orders SET status = @s, employee_id = NULL WHERE id = @id";
                    }
                    else
                    {
                        // Обычное обновление статуса (привязываем к себе)
                        sql = "UPDATE orders SET status = @s, employee_id = @eid WHERE id = @id";
                    }

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("s", newStatus);
                        cmd.Parameters.AddWithValue("id", selectedOrder.Id);
                        if (!releaseEmployee)
                            cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);

                        cmd.ExecuteNonQuery();
                    }
                }
                LoadOrders();
                DetailsPanel.Visibility = Visibility.Hidden;
                TxtPlaceholder.Visibility = Visibility.Visible;
                ListOrders.SelectedItem = null;
            }
        }

        private void BtnSendNote_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrder == null || string.IsNullOrWhiteSpace(TxtNote.Text)) return;
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("INSERT INTO order_notes (order_id, employee_id, note_text) VALUES (@oid, @eid, @txt)", conn))
                    {
                        cmd.Parameters.AddWithValue("oid", selectedOrder.Id);
                        cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                        cmd.Parameters.AddWithValue("txt", TxtNote.Text);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Заметка сохранена");
                TxtNote.Clear();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrder == null) return;
            var simpleOrder = new Order { Id = selectedOrder.Id };
            new LabelWindow(simpleOrder).ShowDialog();
        }
    }
}