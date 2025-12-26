using darks.Classes;
using Npgsql;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace darks.Windows
{
    public partial class MapPage : Page
    {
        public MapPage()
        {
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadMap();
        }

        private void LoadMap()
        {
            WarehouseGrid.Children.Clear();

            // Получаем данные о ячейках (А-1 до E-10)
            // В реальном проекте тут сложнее, но мы симулируем сетку 5 рядов * 10 полок
            for (char row = 'A'; row <= 'E'; row++)
            {
                for (int col = 1; col <= 10; col++)
                {
                    string location = $"{row}-{col}";
                    var cell = CreateCell(location);
                    WarehouseGrid.Children.Add(cell);
                }
            }
        }

        private Border CreateCell(string location)
        {
            // Ищем товар в этой локации
            string productName = "";
            int stock = 0;
            bool hasProduct = false;

            using (var conn = Db.GetConn())
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT name, stock FROM products WHERE shelf_location = @loc LIMIT 1", conn))
                {
                    cmd.Parameters.AddWithValue("loc", location);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            hasProduct = true;
                            productName = r["name"].ToString();
                            stock = (int)r["stock"];
                        }
                    }
                }
            }

            // Оформление ячейки
            var border = new Border
            {
                Margin = new Thickness(5),
                CornerRadius = new CornerRadius(5),
                Height = 80,
                Width = 100,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Цвет
            if (!hasProduct) border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDC3C7")); // Gray
            else if (stock < 10) border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")); // Red
            else border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")); // Green

            // Текст
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = location, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });

            if (hasProduct)
            {
                stack.Children.Add(new TextBlock { Text = productName, FontSize = 10, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 30, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2) });
                stack.Children.Add(new TextBlock { Text = $"{stock} шт.", FontSize = 10, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });

                // Тултип с деталями
                border.ToolTip = $"{productName}\nОстаток: {stock}\nЯчейка: {location}";
            }
            else
            {
                stack.Children.Add(new TextBlock { Text = "Свободно", FontSize = 9, Foreground = Brushes.White, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center });
            }

            border.Child = stack;

            // Анимация нажатия (просто сообщение)
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (hasProduct) MessageBox.Show($"Ячейка {location}\nТовар: {productName}\nНужно пополнить?", "Инфо склада");
            };

            return border;
        }
    }
}