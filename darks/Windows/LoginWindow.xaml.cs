using darks.Classes;
using darks.Windows;
using Npgsql;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace darks.Windows
{
    public partial class LoginWindow : Window
    {
        private double targetX; // Куда нужно попасть (по горизонтали)
        private const double Tolerance = 10; // Допустимая погрешность (+- 10 пикселей)

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            GenerateCaptcha();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            GenerateCaptcha();
        }

        private void GenerateCaptcha()
        {
            // 1. Сброс слайдера
            SliderCaptcha.Value = 0;

            // 2. Загрузка случайной картинки (природа/архитектура)
            // Используем Lorem Picsum для случайных фото
            string imageUrl = $"https://picsum.photos/340/150?random={Guid.NewGuid()}";
            var bitmap = new BitmapImage(new Uri(imageUrl));
            ImgBackground.Source = bitmap;
            ImgPiece.Source = bitmap; // Кусочек использует ту же картинку

            // 3. Генерация позиции пазла
            Random rnd = new Random();

            // Пазл не должен быть слишком близко к левому краю (минимум 100px), 
            // чтобы было что двигать, и не выходить за правый край.
            targetX = rnd.Next(100, 270);
            double targetY = rnd.Next(10, 80);

            // 4. Позиционирование "Дырки" (Тень на фоне)
            Canvas.SetLeft(PathHole, targetX);
            Canvas.SetTop(PathHole, targetY);

            // 5. Настройка "Кусочка"
            // Позиция Y совпадает с целью
            Canvas.SetTop(CanvasPiece, targetY);
            // Позиция X привязана к слайдеру (начинается с 0)
            Canvas.SetLeft(CanvasPiece, 0);

            // 6. МАГИЯ: Сдвигаем картинку ВНУТРИ кусочка так, 
            // чтобы она совпадала с текстурой фона в точке targetX, targetY.
            // Используем TranslateTransform
            ImgPiece.RenderTransform = new TranslateTransform(-targetX, -targetY);
        }

        private void SliderCaptcha_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Двигаем кусочек по X в зависимости от слайдера
            Canvas.SetLeft(CanvasPiece, SliderCaptcha.Value);
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            // ПРОВЕРКА: Попал ли пользователь кусочком в дырку?
            if (Math.Abs(SliderCaptcha.Value - targetX) > Tolerance)
            {
                MessageBox.Show("Пазл не совпал! Попробуйте еще раз.");
                GenerateCaptcha();
                return;
            }

            // --- Дальше стандартная логика входа ---
            string login = TxtLogin.Text.Trim();
            string pass = TxtPass.Password.Trim(); // PasswordBox требует .Password

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    string sql = "SELECT id, full_name, role, status FROM employees WHERE login = @l AND password = @p";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("l", login);
                        cmd.Parameters.AddWithValue("p", pass);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader["status"].ToString() != "active")
                                {
                                    MessageBox.Show("Сотрудник заблокирован.");
                                    return;
                                }

                                CurrentSession.User = new Employee
                                {
                                    Id = (int)reader["id"],
                                    FullName = reader["full_name"].ToString(),
                                    Role = reader["role"].ToString()
                                };
                                reader.Close();

                                // Лог
                                using (var cmdLog = new NpgsqlCommand("INSERT INTO logs (employee_id, action) VALUES (@id, 'Login')", conn))
                                {
                                    cmdLog.Parameters.AddWithValue("id", CurrentSession.User.Id);
                                    cmdLog.ExecuteNonQuery();
                                }

                                MainWindow main = new MainWindow();
                                main.Show();
                                this.Close();
                            }
                            else
                            {
                                MessageBox.Show("Неверный логин или пароль");
                                GenerateCaptcha();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения: " + ex.Message);
            }
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    }
}