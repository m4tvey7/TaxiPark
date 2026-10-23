using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TaxiPark.Entities;

namespace TaxiPark
{
    public partial class CarEditWindow : Window
    {
        private readonly Car _car;
        private readonly bool _isNew;

        public CarEditWindow(Car car)
        {
            InitializeComponent();
            _isNew = car == null;
            _car = car ?? new Car();

            CbBrand.ItemsSource = App.Context.CarBrand.OrderBy(b => b.name).Select(b => b.name).ToList();
            // при вводе/выборе марки список моделей подстраивается под неё
            CbBrand.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(Brand_TextChanged));

            var categories = App.Context.LicenseCategory.OrderBy(c => c.code).ToList();
            CbCategory.ItemsSource = categories;

            var statuses = App.Context.CarStatus.OrderBy(s => s.status_id).ToList();
            // статус «Занята» выставляется только сменой, вручную его выбрать нельзя
            if (_isNew || _car.status_id != App.StatusBusy)
                statuses = statuses.Where(s => s.status_id != App.StatusBusy).ToList();
            CbStatus.ItemsSource = statuses;

            if (_isNew)
            {
                Title = "Новый автомобиль";
                CbCategory.SelectedValue = categories.Where(c => c.code == "B").Select(c => c.category_id).FirstOrDefault();
                CbStatus.SelectedValue = App.StatusFree;
                TbMileage.Text = "0";
            }
            else
            {
                Title = "Редактирование автомобиля";
                CbBrand.Text = _car.CarModel.CarBrand.name;
                CbModel.Text = _car.CarModel.name;
                TbYear.Text = _car.year_made.ToString();
                TbPlate.Text = _car.plate;
                TbColor.Text = _car.color;
                TbMileage.Text = _car.mileage.ToString();
                CbCategory.SelectedValue = _car.required_category_id;
                CbStatus.SelectedValue = _car.status_id;

                if (_car.status_id == App.StatusBusy)
                    CbStatus.IsEnabled = false;   // статус занятой машины меняет только смена
            }
        }

        private void Brand_TextChanged(object sender, TextChangedEventArgs e)
        {
            string brand = (CbBrand.Text ?? "").Trim();
            string keep = CbModel.Text;
            CbModel.ItemsSource = App.Context.CarModel
                .Where(m => m.CarBrand.name == brand)
                .OrderBy(m => m.name).Select(m => m.name).ToList();
            CbModel.Text = keep;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string brandName = (CbBrand.Text ?? "").Trim();
            string modelName = (CbModel.Text ?? "").Trim();
            string plate = TbPlate.Text.Trim().ToUpper();
            string color = TbColor.Text.Trim();

            if (brandName == "" || modelName == "" || plate == "" || color == "")
            { Error("Заполните марку, модель, госномер и цвет."); return; }
            if (brandName.Length > 50 || modelName.Length > 50)
            { Error("Марка и модель – не более 50 символов."); return; }
            if (plate.Length > 12)
            { Error("Госномер – не более 12 символов."); return; }
            if (color.Length > 30)
            { Error("Цвет – не более 30 символов."); return; }

            int year, mileage;
            if (!int.TryParse(TbYear.Text, out year) || year < 1990 || year > DateTime.Now.Year + 1)
            { Error("Некорректный год выпуска (1990 – текущий год + 1)."); return; }
            if (!int.TryParse(TbMileage.Text, out mileage) || mileage < 0)
            { Error("Пробег должен быть неотрицательным числом."); return; }

            if (!_isNew && _car.status_id == App.StatusBusy && mileage != _car.mileage)
            { Error("Нельзя изменить пробег: автомобиль занят. Пробег обновится при завершении смены."); return; }

            if (CbCategory.SelectedValue == null || CbStatus.SelectedValue == null)
            { Error("Выберите категорию и статус."); return; }

            int carId = _car.car_id;
            if (App.Context.Car.Any(c => c.plate == plate && c.car_id != carId))
            { Error("Автомобиль с таким госномером уже есть."); return; }

            // марка и модель: существующие или новые записи справочников
            var brand = App.Context.CarBrand.FirstOrDefault(b => b.name == brandName);
            if (brand == null) brand = new CarBrand { name = brandName };

            CarModel model = null;
            if (brand.brand_id != 0)
            {
                int bid = brand.brand_id;
                model = App.Context.CarModel.FirstOrDefault(m => m.brand_id == bid && m.name == modelName);
            }
            if (model == null) model = new CarModel { CarBrand = brand, name = modelName };

            _car.CarModel = model;
            _car.year_made = (short)year;
            _car.plate = plate;
            _car.color = color;
            _car.mileage = mileage;
            _car.required_category_id = (int)CbCategory.SelectedValue;
            _car.status_id = (int)CbStatus.SelectedValue;

            if (_isNew) App.Context.Car.Add(_car);

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
