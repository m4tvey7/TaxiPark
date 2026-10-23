using System;
using System.Linq;
using System.Windows;

namespace TaxiPark
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            TbLogin.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string login = TbLogin.Text.Trim();
            string pass = PbPassword.Password;

            if (login == "" || pass == "")
            {
                MessageBox.Show("Введите логин и пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // сначала ищем среди диспетчеров, потом среди водителей
                var disp = App.Context.Dispatcher.FirstOrDefault(d => d.login == login && d.password == pass);
                if (disp != null)
                {
                    App.CurrentDispatcher = disp;
                    App.CurrentDriver = null;
                    new DispatcherWindow().Show();
                    Close();
                    return;
                }

                var drv = App.Context.Driver.FirstOrDefault(d => d.login == login && d.password == pass);
                if (drv != null)
                {
                    App.CurrentDriver = drv;
                    App.CurrentDispatcher = null;
                    new DriverWindow().Show();
                    Close();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось обратиться к базе данных TaxiPark.\n" +
                    "Проверьте, что SQL Server запущен и скрипты БД выполнены.\n\n" + ex.GetBaseException().Message,
                    "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Неверный логин или пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
