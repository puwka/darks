using darks.Classes;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace darks.Windows
{
    // Расширенная модель для отображения в гриде (если стандартной Employee недостаточно)
    // Но мы используем существующий класс Employee из Models.cs

    public partial class EmployeesPage : Page
    {
        public EmployeesPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            var list = new List<Employee>();
            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    // Выбираем всех сотрудников
                    string sql = "SELECT id, full_name, login, role, status FROM employees ORDER BY id";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            // Примечание: Мы временно используем существующий класс Employee.
                            // Поле Login не было в Models.cs в первой части ответа, 
                            // но оно нужно. Давайте добавим его динамически или убедитесь, 
                            // что в Models.cs есть public string Login {get;set;}

                            // Если вы не меняли Models.cs, можно просто не отображать логин в гриде, 
                            // либо добавить поле в класс Employee сейчас:

                            var emp = new Employee
                            {
                                Id = (int)r["id"],
                                FullName = r["full_name"].ToString(),
                                Role = r["role"].ToString(),
                                Status = r["status"].ToString()
                                // Login = r["login"].ToString() // Добавьте это свойство в Models.cs если хотите видеть логин
                            };
                            list.Add(emp);
                        }
                    }
                }
                GridEmployees.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message);
            }
        }

        // Показать/Скрыть панель добавления
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (AddPanel.Visibility == Visibility.Visible)
                AddPanel.Visibility = Visibility.Collapsed;
            else
                AddPanel.Visibility = Visibility.Visible;
        }

        // Сохранение нового сотрудника
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text) ||
                string.IsNullOrWhiteSpace(TxtLogin.Text) ||
                string.IsNullOrWhiteSpace(TxtPass.Text))
            {
                MessageBox.Show("Заполните все поля!");
                return;
            }

            string role = (CmbRole.SelectedItem as ComboBoxItem).Content.ToString();

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = "INSERT INTO employees (full_name, login, password, role, status) VALUES (@n, @l, @p, @r, 'active')";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("n", TxtName.Text);
                        cmd.Parameters.AddWithValue("l", TxtLogin.Text);
                        cmd.Parameters.AddWithValue("p", TxtPass.Text); // В реальном проекте здесь нужен хэш!
                        cmd.Parameters.AddWithValue("r", role);

                        cmd.ExecuteNonQuery();
                    }
                }

                // Очистка и обновление
                TxtName.Clear(); TxtLogin.Clear(); TxtPass.Clear();
                AddPanel.Visibility = Visibility.Collapsed;
                LoadData();
                MessageBox.Show("Сотрудник добавлен!");
            }
            catch (PostgresException ex)
            {
                if (ex.SqlState == "23505") // Код ошибки Unique Violation
                    MessageBox.Show("Такой логин уже существует!");
                else
                    MessageBox.Show("Ошибка БД: " + ex.Message);
            }
        }

        // Изменение статуса (Блокировка / Разблокировка)
        private void BtnBlock_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            int id = (int)btn.Tag;

            // Находим текущий статус, чтобы переключить его
            // В простом варианте без MVVM можно просто считать из выбранной строки или сделать запрос
            // Сделаем запрос для надежности

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();

                    // 1. Получаем текущий статус
                    string currentStatus = "active";
                    using (var cmdGet = new NpgsqlCommand("SELECT status FROM employees WHERE id = @id", conn))
                    {
                        cmdGet.Parameters.AddWithValue("id", id);
                        currentStatus = cmdGet.ExecuteScalar()?.ToString();
                    }

                    // 2. Меняем на противоположный
                    string newStatus = (currentStatus == "active") ? "off" : "active";

                    using (var cmdUpd = new NpgsqlCommand("UPDATE employees SET status = @s WHERE id = @id", conn))
                    {
                        cmdUpd.Parameters.AddWithValue("s", newStatus);
                        cmdUpd.Parameters.AddWithValue("id", id);
                        cmdUpd.ExecuteNonQuery();
                    }
                }
                LoadData(); // Обновляем таблицу
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }
    }
}