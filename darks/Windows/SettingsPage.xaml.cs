using darks.Classes;
using Npgsql;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;

namespace darks.Windows
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            TxtName.Text = CurrentSession.User.FullName;
            TxtRole.Text = CurrentSession.User.Role.ToUpper();
        }

        private void BtnChangePass_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtNewPass.Text))
            {
                MessageBox.Show("Введите новый пароль");
                return;
            }

            try
            {
                using (var conn = Db.GetConn())
                {
                    conn.Open();
                    using (var cmd = new NpgsqlCommand("UPDATE employees SET password = @p WHERE id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("p", TxtNewPass.Text);
                        cmd.Parameters.AddWithValue("id", CurrentSession.User.Id);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Пароль успешно изменен");
                TxtNewPass.Clear();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }
    }
}