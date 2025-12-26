using darks.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace darks.Windows
{
    public partial class ShiftsPage : Page
    {
        private int? currentShiftId = null;
        private DispatcherTimer timer;
        private DateTime currentStartTime;

        public ShiftsPage()
        {
            InitializeComponent();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            var diff = DateTime.Now - currentStartTime;
            TxtTimer.Text = diff.ToString(@"hh\:mm\:ss");
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Сначала проверяем, есть ли активная смена, потом грузим историю
            CheckActiveShift();
            LoadHistory();
        }

        private void CheckActiveShift()
        {
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    // Ищем смену, у которой нет времени окончания (end_time IS NULL)
                    string sql = "SELECT id, start_time FROM shifts WHERE employee_id = @eid AND end_time IS NULL LIMIT 1";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                currentShiftId = (int)r["id"];
                                currentStartTime = Convert.ToDateTime(r["start_time"]);
                                SetShiftActiveUI(true);
                            }
                            else
                            {
                                SetShiftActiveUI(false);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка проверки смены: " + ex.Message);
            }
        }

        private void LoadHistory()
        {
            var list = new List<Shift>();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    // Загружаем историю смен
                    string sql = "SELECT * FROM shifts WHERE employee_id = @eid ORDER BY start_time DESC";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                list.Add(new Shift
                                {
                                    Id = (int)r["id"],
                                    StartTime = Convert.ToDateTime(r["start_time"]),
                                    // Проверка на NULL для времени окончания
                                    EndTime = r["end_time"] == DBNull.Value ? null : (DateTime?)r["end_time"],
                                    // Проверка на NULL для часов
                                    TotalHours = r["total_hours"] == DBNull.Value ? 0 : Convert.ToDouble(r["total_hours"])
                                });
                            }
                        }
                    }
                }
                // Присваиваем источник данных
                GridShifts.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки истории: " + ex.Message);
            }
        }

        private void SetShiftActiveUI(bool isActive)
        {
            if (isActive)
            {
                BtnShiftToggle.Content = "Завершить смену";
                BtnShiftToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B")); // Красный
                timer.Start();
            }
            else
            {
                BtnShiftToggle.Content = "Начать смену";
                BtnShiftToggle.Background = (Brush)Application.Current.Resources["AccentBrush"]; // Синий
                timer.Stop();
                // Не сбрасываем текст таймера в 00:00:00 тут же, чтобы пользователь видел итог
                currentShiftId = null;
            }
        }

        private void BtnShiftToggle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();

                    if (currentShiftId == null) // НАЧАЛО СМЕНЫ
                    {
                        using (var cmd = new NpgsqlCommand("INSERT INTO shifts (employee_id, start_time) VALUES (@eid, NOW()) RETURNING id", conn))
                        {
                            cmd.Parameters.AddWithValue("eid", CurrentSession.User.Id);
                            currentShiftId = (int)cmd.ExecuteScalar();
                        }
                        currentStartTime = DateTime.Now;
                        SetShiftActiveUI(true);
                    }
                    else // ЗАВЕРШЕНИЕ СМЕНЫ
                    {
                        double totalHours = (DateTime.Now - currentStartTime).TotalHours;

                        using (var cmd = new NpgsqlCommand("UPDATE shifts SET end_time = NOW(), total_hours = @th WHERE id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("th", totalHours);
                            cmd.Parameters.AddWithValue("id", currentShiftId);
                            cmd.ExecuteNonQuery();
                        }

                        SetShiftActiveUI(false);
                        TxtTimer.Text = "00:00:00"; // Сбрасываем таймер после сохранения

                        LoadHistory(); // ОБЯЗАТЕЛЬНО обновляем таблицу
                        MessageBox.Show($"Смена закрыта. Отработано часов: {totalHours:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message);
            }
        }
    }
}