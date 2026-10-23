using System;
using System.Globalization;
using System.Windows;
using TaxiPark.Entities;

namespace TaxiPark
{
    public partial class MaintenanceWindow : Window
    {
        private readonly Car _car;

        public MaintenanceWindow(Car car)
        {
            InitializeComponent();
            _car = car;
            TbCar.Text = car.CarModel.CarBrand.name + " " + car.CarModel.name + " (" + car.plate + "), пробег " + car.mileage + " км";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string desc = TbDescription.Text.Trim();
            if (desc == "")
            { Warn("Опишите выполненные работы."); return; }
            if (desc.Length > 300)
            { Warn("Описание слишком длинное (максимум 300 символов)."); return; }

            decimal cost;
            string costText = TbCost.Text.Trim().Replace(',', '.');
            if (!decimal.TryParse(costText, NumberStyles.Number, CultureInfo.InvariantCulture, out cost)
                || cost < 0 || cost > 99999999m)
            { Warn("Стоимость – неотрицательное число (например, 4500 или 4500,50)."); return; }

            App.Context.Maintenance.Add(new Maintenance
            {
                car_id = _car.car_id,
                dispatcher_id = App.CurrentDispatcher.dispatcher_id,
                service_date = DateTime.Today,
                mileage_at_service = _car.mileage,
                description = desc,
                cost = Math.Round(cost, 2)
            });

            // после ТО машина возвращается в парк (из статусов «На ТО» и «Неисправна»)
            if (_car.status_id == App.StatusService || _car.status_id == App.StatusFaulty)
                _car.status_id = App.StatusFree;

            try
            {
                App.Context.SaveChanges();
            }
            catch (Exception ex)
            {
                App.DiscardChanges();
                Warn("Не удалось сохранить: " + ex.GetBaseException().Message);
                return;
            }
            DialogResult = true;
        }

        private void Warn(string text)
        {
            MessageBox.Show(text, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
