using darks.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace darks.Windows
{
    public partial class ProductsPage : Page
    {
        public ProductsPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        // --- НОВАЯ ЛОГИКА: Загружаем только СВОБОДНЫЕ ячейки ---
        private void LoadFreeLocations()
        {
            var freeLocations = new List<string>();
            var takenLocations = new HashSet<string>();

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    // 1. Получаем список всех занятых ячеек
                    using (var cmd = new NpgsqlCommand("SELECT shelf_location FROM products WHERE shelf_location IS NOT NULL", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            takenLocations.Add(r["shelf_location"].ToString());
                        }
                    }
                }

                // 2. Генерируем полную сетку (A-1 ... E-10) и проверяем, занята ли ячейка
                char[] rows = { 'A', 'B', 'C', 'D', 'E' };
                foreach (var row in rows)
                {
                    for (int i = 1; i <= 10; i++)
                    {
                        string loc = $"{row}-{i}";
                        // Добавляем в список только если НЕ занято
                        if (!takenLocations.Contains(loc))
                        {
                            freeLocations.Add(loc);
                        }
                    }
                }

                CmbLocation.ItemsSource = freeLocations;

                if (freeLocations.Count == 0)
                {
                    CmbLocation.ItemsSource = new List<string> { "Нет мест" };
                    CmbLocation.IsEnabled = false;
                }
                else
                {
                    CmbLocation.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки мест: " + ex.Message);
            }
        }

        private void LoadData(string search = "")
        {
            var list = new List<Product>();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = "SELECT * FROM products";
                    if (!string.IsNullOrEmpty(search))
                        sql += $" WHERE LOWER(name) LIKE '%{search.ToLower()}%'";

                    sql += " ORDER BY id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new Product
                            {
                                Id = (int)r["id"],
                                Name = r["name"].ToString(),
                                Category = r["category"].ToString(),
                                Price = (decimal)r["price"],
                                Stock = (int)r["stock"],
                                Barcode = r["shelf_location"] != DBNull.Value ? r["shelf_location"].ToString() : "-"
                            });
                        }
                    }
                }
                GridProducts.ItemsSource = list;
            }
            catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
        }

        // --- Управление Модальным окном ---
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            ModalAddProduct.Visibility = Visibility.Visible;

            // ОБНОВЛЯЕМ список свободных мест при каждом открытии окна
            LoadFreeLocations();

            // Сброс полей
            TxtName.Clear();
            TxtPrice.Clear();
            TxtStock.Text = "0";
            CmbCategory.SelectedIndex = 0;
            CmbLocation.SelectedIndex = -1;
            DpExpiry.SelectedDate = DateTime.Now.AddMonths(1);
        }

        private void BtnCloseModal_Click(object sender, RoutedEventArgs e)
        {
            ModalAddProduct.Visibility = Visibility.Collapsed;
        }

        // --- Логика Сохранения ---
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text) || string.IsNullOrWhiteSpace(TxtPrice.Text))
            {
                MessageBox.Show("Введите название и цену");
                return;
            }

            string location = CmbLocation.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(location) || location == "Нет мест")
            {
                MessageBox.Show("Выберите свободную ячейку!");
                return;
            }

            try
            {
                string name = TxtName.Text;
                string category = CmbCategory.Text;
                decimal price = decimal.Parse(TxtPrice.Text);
                int stock = int.Parse(TxtStock.Text);
                DateTime? expiry = DpExpiry.SelectedDate;

                using (var conn = Db.GetConn())
                {
                    conn.Open();

                    // --- ПРОВЕРКА: Точно ли ячейка свободна? (Защита от дублей) ---
                    using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM products WHERE shelf_location = @loc", conn))
                    {
                        checkCmd.Parameters.AddWithValue("loc", location);
                        long count = (long)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            MessageBox.Show($"Ошибка! Ячейка {location} уже занята другим товаром.");
                            // Перезагружаем список, чтобы убрать занятую ячейку из UI
                            LoadFreeLocations();
                            return;
                        }
                    }
                    // ---------------------------------------------------------------

                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            // 1. Создаем товар
                            int newProdId;
                            string sqlProd = "INSERT INTO products (name, category, price, stock, shelf_location, barcode) " +
                                             "VALUES (@n, @c, @p, @s, @loc, @bar) RETURNING id";

                            using (var cmd = new NpgsqlCommand(sqlProd, conn))
                            {
                                cmd.Parameters.AddWithValue("n", name);
                                cmd.Parameters.AddWithValue("c", category);
                                cmd.Parameters.AddWithValue("p", price);
                                cmd.Parameters.AddWithValue("s", stock);
                                cmd.Parameters.AddWithValue("loc", location);
                                cmd.Parameters.AddWithValue("bar", Guid.NewGuid().ToString().Substring(0, 8));

                                newProdId = (int)cmd.ExecuteScalar();
                            }

                            // 2. Создаем поставку (для учета срока годности)
                            if (stock > 0 && expiry.HasValue)
                            {
                                string sqlSupply = "INSERT INTO supplies (supplier_id, product_id, qty, status, delivery_date, expiry_date) " +
                                                   "VALUES (1, @pid, @qty, 'received', CURRENT_DATE, @exp)";

                                using (var cmd = new NpgsqlCommand(sqlSupply, conn))
                                {
                                    cmd.Parameters.AddWithValue("pid", newProdId);
                                    cmd.Parameters.AddWithValue("qty", stock);
                                    cmd.Parameters.AddWithValue("exp", expiry.Value);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            trans.Commit();
                            MessageBox.Show("Товар успешно создан!");
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback();
                            throw ex;
                        }
                    }
                }

                ModalAddProduct.Visibility = Visibility.Collapsed;
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить товар?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                int id = (int)(sender as Button).Tag;
                try
                {
                    using (var conn = Db.GetConn())
                    {
                        conn.Open();
                        // Каскадное удаление вручную (безопаснее для SQLite/Postgres без настроенных FK)
                        new NpgsqlCommand($"DELETE FROM order_items WHERE product_id={id}", conn).ExecuteNonQuery();
                        new NpgsqlCommand($"DELETE FROM supplies WHERE product_id={id}", conn).ExecuteNonQuery();
                        new NpgsqlCommand($"DELETE FROM products WHERE id={id}", conn).ExecuteNonQuery();
                    }
                    LoadData();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            LoadData(TxtSearch.Text);
        }
    }
}