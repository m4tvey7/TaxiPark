using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using TaxiPark.Entities;

namespace TaxiPark
{
    public partial class DriverWindow : Window
    {
        private readonly Driver _me;
        private Waybill _active;   // открытый путевой лист водителя (end_time == null)

        public DriverWindow()
        {
            InitializeComponent();
            _me = App.CurrentDriver;
            TbUser.Text = "Водитель: " + _me.full_name + "   (категории: " + string.Join(", ", _me.LicenseCategory.Select(cat => cat.code).OrderBy(cat => cat)) + ")";
            RefreshAll();
        }

        private void RefreshAll()
        {
            DgCars.ItemsSource = App.Context.Car
                .Include(c => c.CarModel.CarBrand).Include(c => c.CarStatus).Include(c => c.LicenseCategory)
                .ToList().OrderBy(c => c.CarModel.CarBrand.name).ThenBy(c => c.CarModel.name).ToList();

            int myId = _me.driver_id;
            _active = App.Context.Waybill.Include(w => w.Car.CarModel.CarBrand)
                .FirstOrDefault(w => w.driver_id == myId && w.end_time == null);
            BtnStart.IsEnabled = _active == null;

            if (_active != null)
            {
                PnlActive.Visibility = Visibility.Visible;
                TbActive.Text = "Активная смена: " + _active.Car.CarModel.CarBrand.name + " " + _active.Car.CarModel.name +
                                " (" + _active.Car.plate + "), начата " +
                                _active.start_time.ToString("dd.MM.yyyy HH:mm") +
                                ", спидометр на старте: " + _active.start_odometer + " км";
            }
            else
            {
                PnlActive.Visibility = Visibility.Collapsed;
            }

            var history = App.Context.Waybill.Include(w => w.Car.CarModel.CarBrand)
                .Where(w => w.driver_id == myId && w.end_time != null)
                .ToList().OrderByDescending(w => w.start_time)
                .Select(w => new
                {
                    Start = w.start_time,
                    End = w.end_time,
                    CarName = w.Car.CarModel.CarBrand.name + " " + w.Car.CarModel.name,
                    Plate = w.Car.plate,
                    StartOdo = w.start_odometer,
                    EndOdo = w.end_odometer,
                    Km = w.end_odometer - w.start_odometer,
                    Comment = w.driver_comment
                }).ToList();
            DgHistory.ItemsSource = history;
            TbTotal.Text = "Смен: " + history.Count + ",  общий пробег: " + history.Sum(h => h.Km ?? 0) + " км";
        }

        // начало смены
        private void StartShift_Click(object sender, RoutedEventArgs e)
        {
            var car = DgCars.SelectedItem as Car;
            if (car == null)
            { Warn("Выберите свободный автомобиль в списке."); return; }
            if (car.status_id != App.StatusFree)
            { Warn("Этот автомобиль недоступен (статус: " + car.CarStatus.name + ")."); return; }

            // у водителя может быть несколько категорий: нужна та, что требуется автомобилю
            bool hasCategory = _me.LicenseCategory.Any(c => c.category_id == car.required_category_id);
            if (!hasCategory)
            { Warn("Для этого автомобиля нужна категория «" + car.LicenseCategory.code + "». У вас: " + string.Join(", ", _me.LicenseCategory.Select(cat => cat.code).OrderBy(cat => cat)) + "."); return; }

            DateTime now = DateTime.Now;
            App.Context.Waybill.Add(new Waybill
            {
                car_id = car.car_id,
                driver_id = _me.driver_id,
                shift_date = now.Date,
                start_time = now,
                start_odometer = car.mileage
            });
            car.status_id = App.StatusBusy;

            try { App.Context.SaveChanges(); }
            catch (Exception ex)
            {
                App.DiscardChanges();
                Warn("Не удалось начать смену: " + ex.GetBaseException().Message);
                RefreshAll();
                return;
            }

            RefreshAll();
            MessageBox.Show("Смена начата. Путевой лист создан.", "Смена", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // завершение смены
        private void EndShift_Click(object sender, RoutedEventArgs e)
        {
            if (_active == null) return;

            int endOdo;
            if (!int.TryParse(TbEndOdo.Text, out endOdo))
            { Warn("Введите конечные показания спидометра числом."); return; }
            if (endOdo < _active.start_odometer)
            { Warn("Конечные показания не могут быть меньше начальных (" + _active.start_odometer + ")."); return; }

            string comment = TbComment.Text.Trim();
            if (comment.Length > 500)
            { Warn("Комментарий слишком длинный (максимум 500 символов)."); return; }

            int mileage = endOdo - _active.start_odometer;   // пробег за смену

            DateTime end = DateTime.Now;
            if (end <= _active.start_time) end = _active.start_time.AddSeconds(1);   // в БД конец смены должен быть строго позже начала

            _active.end_time = end;
            _active.end_odometer = endOdo;
            _active.driver_comment = comment == "" ? null : comment;

            var car = _active.Car;
            car.mileage = endOdo;                  // общий пробег автомобиля
            car.status_id = App.StatusFree;        // машина свободна

            try { App.Context.SaveChanges(); }
            catch (Exception ex)
            {
                App.DiscardChanges();
                Warn("Не удалось завершить смену: " + ex.GetBaseException().Message);
                RefreshAll();
                return;
            }

            TbEndOdo.Clear();
            TbComment.Clear();
            RefreshAll();
            MessageBox.Show("Смена завершена. Пробег за смену: " + mileage + " км.",
                "Смена", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Warn(string text)
        {
            MessageBox.Show(text, "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentDriver = null;
            new LoginWindow().Show();
            Close();
        }
    }
}
