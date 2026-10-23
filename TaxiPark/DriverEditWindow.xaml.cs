using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using TaxiPark.Entities;

namespace TaxiPark
{
    public partial class DriverEditWindow : Window
    {
        private readonly Driver _driver;
        private readonly bool _isNew;
        private readonly List<CheckBox> _checks = new List<CheckBox>();

        public DriverEditWindow(Driver driver)
        {
            InitializeComponent();
            _isNew = driver == null;
            _driver = driver ?? new Driver();

            // по одному флажку на каждую категорию из справочника LicenseCategory
            foreach (var cat in App.Context.LicenseCategory.OrderBy(c => c.code).ToList())
            {
                var cb = new CheckBox
                {
                    Content = cat.code + " – " + cat.description,
                    Tag = cat,
                    Margin = new Thickness(0, 0, 0, 3),
                    IsChecked = !_isNew && _driver.LicenseCategory.Any(x => x.category_id == cat.category_id)
                };
                if (_isNew && cat.code == "B") cb.IsChecked = true;
                _checks.Add(cb);
                PnlCategories.Children.Add(cb);
            }

            if (_isNew)
            {
                Title = "Новый водитель";
            }
            else
            {
                Title = "Редактирование водителя";
                TbName.Text = _driver.full_name;
                TbPhone.Text = _driver.phone;
                TbExp.Text = _driver.experience_years.ToString();
                TbLogin.Text = _driver.login;
                TbPassword.Text = _driver.password;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string name = TbName.Text.Trim();
            string phone = TbPhone.Text.Trim();
            string login = TbLogin.Text.Trim();
            string pass = TbPassword.Text.Trim();

            if (name == "" || login == "" || pass == "")
            { Error("Заполните ФИО, логин и пароль."); return; }
            if (name.Length > 100 || login.Length > 50 || pass.Length > 50)
            { Error("Слишком длинное значение: ФИО – до 100 символов, логин и пароль – до 50."); return; }

            // пробелы, дефисы и скобки разрешены (+7 916 111-22-33)
            string compact = Regex.Replace(phone, @"[\s\-\(\)]", "");
            if (phone.Length > 20 || !Regex.IsMatch(compact, @"^\+?\d{10,15}$"))
            { Error("Телефон: от 10 до 15 цифр, можно с «+», пробелами и дефисами (не более 20 символов)."); return; }

            int exp;
            if (!int.TryParse(TbExp.Text, out exp) || exp < 0 || exp > 60)
            { Error("Стаж – целое число от 0 до 60."); return; }

            var selected = _checks.Where(c => c.IsChecked == true).Select(c => (LicenseCategory)c.Tag).ToList();
            if (selected.Count == 0)
            { Error("Отметьте хотя бы одну категорию прав."); return; }

            int id = _driver.driver_id;
            if (App.Context.Driver.Any(d => d.login == login && d.driver_id != id)
                || App.Context.Dispatcher.Any(d => d.login == login))
            { Error("Такой логин уже занят."); return; }

            _driver.full_name = name;
            _driver.phone = phone;
            _driver.experience_years = (byte)exp;
            _driver.login = login;
            _driver.password = pass;

            // синхронизируем набор категорий
            var cats = _driver.LicenseCategory;
            foreach (var old in cats.ToList())
                if (!selected.Any(s => s.category_id == old.category_id)) cats.Remove(old);
            foreach (var s in selected)
                if (!cats.Any(x => x.category_id == s.category_id)) cats.Add(s);

            if (_isNew)
            {
                _driver.hire_date = DateTime.Today;
                App.Context.Driver.Add(_driver);
            }

            try
            {
                App.Context.SaveChanges();
            }
            catch (Exception ex)
            {
                App.DiscardChanges();
                Error("Не удалось сохранить: " + ex.GetBaseException().Message);
                return;
            }
            DialogResult = true;
        }

        private void Error(string text)
        {
            MessageBox.Show(text, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
