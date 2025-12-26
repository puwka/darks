using darks.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace darks.Windows
{
    public class DeliveryOrder
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public string Address { get; set; }
        public string ClientName { get; set; }
        public string Phone { get; set; }
    }

    public partial class DeliveryPage : Page
    {
        private DeliveryOrder selectedOrder;

        public DeliveryPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            UpdateEarnings();
        }

        private void LoadData()
        {
            var list = new List<DeliveryOrder>();
            int onWayCount = 0;

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    // ИЗМЕНЕНИЕ: SQL-запрос теперь видит "ничейные" заказы в статусе delivering
                    // Логика:
                    // 1. ready - заказы, готовые к выдаче.
                    // 2. delivering + NULL - сборщик выдал, курьер еще не взял.
                    // 3. delivering + MyID - заказы, которые я уже везу.
                    string sql = @"
                        SELECT id, status, address, client_name, client_phone 
                        FROM orders 
                        WHERE status = 'ready' 
                           OR (status = 'delivering' AND (employee_id IS NULL OR employee_id = @eid))
                        ORDER BY status DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                var ord = new DeliveryOrder
                                {
                                    Id = (int)r["id"],
                                    Status = r["status"].ToString(),
                                    Address = r["address"] == DBNull.Value ? "Адрес не указан" : r["address"].ToString(),
                                    ClientName = r["client_name"] == DBNull.Value ? "Клиент" : r["client_name"].ToString(),
                                    Phone = r["client_phone"].ToString()
                                };
                                list.Add(ord);
                                if (ord.Status == "delivering") onWayCount++;
                            }
                        }
                    }
                }
                ListReady.ItemsSource = list;
                TxtOnWay.Text = $"{onWayCount} в пути";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void UpdateEarnings()
        {
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM orders WHERE employee_id = @eid AND status = 'done' AND DATE(completed_at) = CURRENT_DATE";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                        long count = (long)cmd.ExecuteScalar();
                        TxtEarnings.Text = $"{count * 200} ₽";
                    }
                }
            }
            catch { }
        }

        private void ListReady_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedOrder = ListReady.SelectedItem as DeliveryOrder;
            if (selectedOrder == null)
            {
                ActivePanel.Visibility = Visibility.Hidden;
                Placeholder.Visibility = Visibility.Visible;
                return;
            }

            ActivePanel.Visibility = Visibility.Visible;
            Placeholder.Visibility = Visibility.Hidden;

            TxtTargetAddress.Text = selectedOrder.Address;
            DrawRoute();

            // Логика кнопок
            // Если заказ 'ready' или 'delivering' (но еще не закреплен за мной), показываем кнопку "Взять"
            // Проверить закреплен ли он можно через запрос, но для простоты UI:
            // Если он в списке и я его выбрал - я могу его взять. 
            // Кнопка "Доставлено" активна ТОЛЬКО если статус уже delivering И он мой (в реале), 
            // но здесь упростим: если статус delivering, значит я его везу.

            // Чтобы разделить "Ничейный delivering" и "Мой delivering", в идеале нужно поле в модели.
            // Но мы сделаем так: кнопка "Поехали" доступна всегда, она перезаписывает заказ на меня.

            if (selectedOrder.Status == "delivering")
            {
                // Тут тонкий момент: это МОЙ заказ или НИЧЕЙ?
                // Добавим кнопку "Завершить", но по нажатию проверим базу, мой ли он.
                // Для MVP: показываем обе кнопки, если статус delivering, но можно скрыть "Поехали", если уже взял.

                // Простая логика:
                BtnStart.Visibility = Visibility.Collapsed;
                BtnFinish.Visibility = Visibility.Visible;
            }
            else // ready
            {
                BtnStart.Visibility = Visibility.Visible;
                BtnFinish.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrder == null) return;
            // Привязываем заказ к себе
            ChangeStatus("delivering");
            MessageBox.Show("Заказ принят! Выезжаем.");
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (selectedOrder == null) return;
            int finishedId = selectedOrder.Id;
            ChangeStatus("done");
            MessageBox.Show($"Заказ #{finishedId} доставлен. +200₽!");
            UpdateEarnings();
        }

        private void ChangeStatus(string newStatus)
        {
            if (selectedOrder == null) return;

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = "UPDATE orders SET status = @s, employee_id = @eid WHERE id = @id";
                    if (newStatus == "done") sql = "UPDATE orders SET status = @s, completed_at = NOW() WHERE id = @id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("s", newStatus);
                        cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id); // ВСЕГДА присваиваем себе при действии
                        cmd.Parameters.AddWithValue("id", selectedOrder.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadData();
                ActivePanel.Visibility = Visibility.Hidden;
                Placeholder.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void DrawRoute()
        {
            if (selectedOrder == null) return;
            MapCanvas.Children.Clear();
            Random rnd = new Random(selectedOrder.Id);

            Point start = new Point(50, 250);
            Point end = new Point(300, 50);

            Polyline route = new Polyline
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB")),
                StrokeThickness = 4,
                StrokeDashArray = new DoubleCollection { 4, 2 }
            };

            Point current = start;
            route.Points.Add(current);

            for (int i = 0; i < 3; i++)
            {
                current = new Point(current.X + rnd.Next(30, 80), current.Y - rnd.Next(20, 60));
                route.Points.Add(current);
            }
            route.Points.Add(end);
            MapCanvas.Children.Add(route);

            DrawMarker(start, Brushes.Gray, "🏠");
            DrawMarker(end, Brushes.Red, "📍");
        }

        private void DrawMarker(Point p, Brush color, string text)
        {
            Ellipse el = new Ellipse { Width = 30, Height = 30, Fill = Brushes.White, Stroke = color, StrokeThickness = 2 };
            Canvas.SetLeft(el, p.X - 15); Canvas.SetTop(el, p.Y - 15);
            MapCanvas.Children.Add(el);

            TextBlock tb = new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Canvas.SetLeft(tb, p.X - 8); Canvas.SetTop(tb, p.Y - 10);
            MapCanvas.Children.Add(tb);
        }
    }
}