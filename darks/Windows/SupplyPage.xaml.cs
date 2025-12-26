using darks.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace darks.Windows
{
    // Вспомогательный класс модели для этой таблицы
    public class SupplyItem
    {
        public int Id { get; set; }
        public string SupplierName { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Qty { get; set; }
        public string Status { get; set; }
        public DateTime Date { get; set; }

        // Свойство для видимости кнопки (если статус не 'pending', кнопка скрыта)
        public Visibility IsPendingVisible => Status == "pending" ? Visibility.Visible : Visibility.Collapsed;
    }

    public partial class SupplyPage : Page
    {
        public SupplyPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e) => LoadData();

        private void LoadData()
        {
            var list = new List<SupplyItem>();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = @"
                        SELECT s.id, sup.name as sup_name, p.name as prod_name, s.qty, s.status, s.delivery_date, s.product_id
                        FROM supplies s
                        JOIN suppliers sup ON s.supplier_id = sup.id
                        JOIN products p ON s.product_id = p.id
                        ORDER BY s.status DESC, s.delivery_date DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new SupplyItem
                            {
                                Id = (int)r["id"],
                                SupplierName = r["sup_name"].ToString(),
                                ProductName = r["prod_name"].ToString(),
                                ProductId = (int)r["product_id"],
                                Qty = (int)r["qty"],
                                Status = r["status"].ToString(),
                                Date = Convert.ToDateTime(r["delivery_date"])
                            });
                        }
                    }
                }
                GridSupplies.ItemsSource = list;
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnReceive_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).Tag as SupplyItem;
            if (item == null) return;

            if (MessageBox.Show($"Принять поставку '{item.ProductName}' (+{item.Qty} шт)?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = Db.GetConn())
                    {
                        conn.Open();
                        using (var trans = conn.BeginTransaction())
                        {
                            try
                            {
                                // 1. Обновляем статус поставки
                                using (var cmd = new NpgsqlCommand("UPDATE supplies SET status = 'received' WHERE id = @id", conn))
                                {
                                    cmd.Parameters.AddWithValue("id", item.Id);
                                    cmd.ExecuteNonQuery();
                                }

                                // 2. Увеличиваем остаток товара
                                using (var cmd = new NpgsqlCommand("UPDATE products SET stock = stock + @qty WHERE id = @pid", conn))
                                {
                                    cmd.Parameters.AddWithValue("qty", item.Qty);
                                    cmd.Parameters.AddWithValue("pid", item.ProductId);
                                    cmd.ExecuteNonQuery();
                                }

                                // 3. Лог
                                using (var cmd = new NpgsqlCommand("INSERT INTO logs (employee_id, action) VALUES (@eid, @act)", conn))
                                {
                                    cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                                    cmd.Parameters.AddWithValue("act", $"Received Supply #{item.Id}");
                                    cmd.ExecuteNonQuery();
                                }

                                trans.Commit();
                                MessageBox.Show("Товар принят на склад!");
                            }
                            catch
                            {
                                trans.Rollback();
                                throw;
                            }
                        }
                    }
                    LoadData();
                }
                catch (Exception ex) { MessageBox.Show("Ошибка транзакции: " + ex.Message); }
            }
        }
    }
}