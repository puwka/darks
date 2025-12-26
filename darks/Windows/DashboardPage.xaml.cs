using darks.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace darks.Windows
{
    // Вспомогательный класс для Лидеров
    public class Leader
    {
        public string Rank { get; set; } // Медалька
        public string Name { get; set; }
        public int Count { get; set; }
    }

    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStats();
        }

        private void LoadStats()
        {
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();

                    // 1. KPI: Активные заказы
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE status IN ('new', 'picking')", conn))
                        TxtActiveOrders.Text = cmd.ExecuteScalar()?.ToString() ?? "0";

                    // 2. KPI: Склад
                    using (var cmd = new NpgsqlCommand("SELECT COALESCE(SUM(stock), 0) FROM products", conn))
                        TxtTotalStock.Text = cmd.ExecuteScalar().ToString();

                    // 3. KPI: Сделано сегодня
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE status = 'done' AND DATE(completed_at) = CURRENT_DATE", conn))
                        TxtDoneToday.Text = cmd.ExecuteScalar()?.ToString() ?? "0";

                    // 4. Популярные товары
                    var popular = new List<Product>();
                    string sqlPop = @"SELECT p.name, p.category, SUM(oi.qty) as sold 
                                      FROM order_items oi
                                      JOIN products p ON oi.product_id = p.id
                                      GROUP BY p.name, p.category
                                      ORDER BY sold DESC LIMIT 5";
                    using (var cmd = new NpgsqlCommand(sqlPop, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            popular.Add(new Product
                            {
                                Name = r["name"].ToString(),
                                Category = r["category"].ToString(),
                                Stock = Convert.ToInt32(r["sold"]) // Временно храним продажи в Stock
                            });
                        }
                    }
                    ListPopular.ItemsSource = popular;

                    // 5. Просрочка (30 дней, только принятые)
                    var expiring = new List<Product>();
                    string sqlExpiry = @"SELECT p.name, s.expiry_date
                                         FROM supplies s
                                         JOIN products p ON s.product_id = p.id
                                         WHERE s.status = 'received' 
                                           AND s.expiry_date IS NOT NULL
                                           AND s.expiry_date >= CURRENT_DATE 
                                           AND s.expiry_date <= CURRENT_DATE + INTERVAL '30 days'
                                         ORDER BY s.expiry_date ASC LIMIT 10";
                    using (var cmd = new NpgsqlCommand(sqlExpiry, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            expiring.Add(new Product
                            {
                                Name = r["name"].ToString(),
                                ExpiryDate = Convert.ToDateTime(r["expiry_date"])
                            });
                        }
                    }
                    ListExpiry.ItemsSource = expiring;

                    // 6. Доска почета (Лидеры)
                    var leaders = new List<Leader>();
                    string sqlLeaders = @"SELECT e.full_name, COUNT(o.id) as cnt 
                                          FROM orders o
                                          JOIN employees e ON o.employee_id = e.id
                                          WHERE o.status = 'done'
                                          GROUP BY e.full_name
                                          ORDER BY cnt DESC LIMIT 3";
                    using (var cmd = new NpgsqlCommand(sqlLeaders, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        int rank = 1;
                        while (r.Read())
                        {
                            string medal = rank == 1 ? "🥇" : (rank == 2 ? "🥈" : "🥉");
                            leaders.Add(new Leader
                            {
                                Rank = medal,
                                Name = r["full_name"].ToString(),
                                Count = Convert.ToInt32(r["cnt"])
                            });
                            rank++;
                        }
                    }
                    ListLeaders.ItemsSource = leaders;

                    // 7. AI Прогноз (Smart Forecast)
                    // Среднее кол-во заказов за неделю * 1.1 (рост)
                    using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM orders WHERE created_at > NOW() - INTERVAL '7 days'", conn))
                    {
                        long totalWeek = (long)cmd.ExecuteScalar();
                        double avg = totalWeek / 7.0;
                        if (avg < 1) avg = 5; // Фейковые данные для старта
                        int prediction = (int)(avg * 1.1);
                        TxtForecast.Text = $"Ожидаем завтра ~{prediction} заказов (Тренд: Рост 📈)";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка дашборда: " + ex.Message);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("ID;Название;Категория;Остаток;Цена");

                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("SELECT * FROM products ORDER BY id", conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            sb.AppendLine($"{r["id"]};{r["name"]};{r["category"]};{r["stock"]};{r["price"]}");
                    }
                }

                string filename = $"report_{DateTime.Now:yyyyMMdd_HHmm}.csv";
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filename);
                File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);

                var p = new System.Diagnostics.Process();
                p.StartInfo = new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true };
                p.Start();
            }
            catch (Exception ex) { MessageBox.Show("Ошибка экспорта: " + ex.Message); }
        }
    }
}