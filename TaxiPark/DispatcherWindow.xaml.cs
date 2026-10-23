using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using TaxiPark.Entities;

namespace TaxiPark
{
    public partial class DispatcherWindow : Window
    {
        public DispatcherWindow()
        {
            InitializeComponent();
            TbUser.Text = "Диспетчер: " + App.CurrentDispatcher.full_name;
            LoadAll();
        }

        private void LoadAll()
        {
            LoadCars();
            LoadDrivers();
            LoadShifts();
            LoadMaintenance();
        }

        // загрузка данных
        private void LoadCars()
        {
            string q = (TbSearch.Text ?? "").Trim();

            var cars = App.Context.Car
                .Include(c => c.CarModel.CarBrand).Include(c => c.CarStatus).Include(c => c.LicenseCategory)
                .AsQueryable();

            if (q != "")
                cars = cars.Where(c => c.CarModel.CarBrand.name.Contains(q)
                                    || c.CarModel.name.Contains(q)
                                    || c.plate.Contains(q));

            DgCars.ItemsSource = cars.OrderBy(c => c.CarModel.CarBrand.name).ThenBy(c => c.CarModel.name).ToList();
        }

        private void LoadDrivers()
        {
            DgDrivers.ItemsSource = App.Context.Driver.OrderBy(d => d.full_name).ToList();
        }

        private void LoadShifts()
        {
            var shifts = App.Context.Waybill
                .Include(w => w.Driver).Include(w => w.Car.CarModel.CarBrand)
                .AsQueryable();

            if (CbOnlyComments != null && CbOnlyComments.IsChecked == true)
                shifts = shifts.Where(w => w.driver_comment != null && w.driver_comment != "");

            DgShifts.ItemsSource = shifts.OrderByDescending(w => w.start_time).ToList().Select(w => new
            {
                Start = w.start_time,
                End = w.end_time,
                DriverName = w.Driver.full_name,
                Brand = w.Car.CarModel.CarBrand.name,
                Model = w.Car.CarModel.name,
                Plate = w.Car.plate,
                Km = w.end_odometer - w.start_odometer,
                Comment = w.driver_comment
            }).ToList();
        }

        private void LoadMaintenance()
        {
            DgMaint.ItemsSource = App.Context.Maintenance
                .Include(m => m.Car.CarModel.CarBrand).Include(m => m.Dispatcher)
                .OrderByDescending(m => m.service_date).ThenByDescending(m => m.maintenance_id)
                .ToList();
        }

        private void Search_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (DgCars != null) LoadCars();
        }

        private void ShiftFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (DgShifts != null) LoadShifts();
        }

        // автомобили
        private Car SelectedCar()
        {
            var car = DgCars.SelectedItem as Car;
            if (car == null)
                MessageBox.Show("Выберите автомобиль в списке.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            return car;
        }

        private void AddCar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CarEditWindow(null) { Owner = this };
            if (dlg.ShowDialog() == true) LoadCars();
        }

        private void EditCar_Click(object sender, RoutedEventArgs e)
        {
            var car = SelectedCar();
            if (car == null) return;
            var dlg = new CarEditWindow(car) { Owner = this };
            if (dlg.ShowDialog() == true) LoadCars();
        }

        private void DeleteCar_Click(object sender, RoutedEventArgs e)
        {
            var car = SelectedCar();
            if (car == null) return;

            if (car.status_id == App.StatusBusy)
            {
                MessageBox.Show("Автомобиль сейчас в смене – удалять нельзя.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int id = car.car_id;
            bool hasHistory = App.Context.Waybill.Any(w => w.car_id == id) || App.Context.Maintenance.Any(m => m.car_id == id);
            string text = "Удалить автомобиль " + car.CarModel.CarBrand.name + " " + car.CarModel.name + " (" + car.plate + ")?";
            if (hasHistory) text += "\nБудет удалена и вся его история смен и ТО.";

            if (MessageBox.Show(text, "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                // сначала зависимые записи (внешние ключи), потом сам автомобиль
                App.Context.Maintenance.RemoveRange(App.Context.Maintenance.Where(m => m.car_id == id).ToList());
                App.Context.Waybill.RemoveRange(App.Context.Waybill.Where(w => w.car_id == id).ToList());
                App.Context.Car.Remove(car);
                App.Context.SaveChanges();
                LoadAll();
            }
            catch (Exception ex)
            {
                App.DiscardChanges();
                MessageBox.Show("Не удалось удалить: " + ex.GetBaseException().Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToService_Click(object sender, RoutedEventArgs e)
        {
            var car = SelectedCar();
            if (car == null) return;
            if (car.status_id == App.StatusBusy)
            {
                MessageBox.Show("Автомобиль в смене. Дождитесь окончания смены.", "ТО", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (car.status_id == App.StatusWrittenOff)
            {
                MessageBox.Show("Автомобиль списан.", "ТО", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            car.status_id = App.StatusService;
            try { App.Context.SaveChanges(); }
            catch (Exception ex)
            {
                App.DiscardChanges();
                MessageBox.Show("Не удалось сохранить: " + ex.GetBaseException().Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            LoadCars();
        }

        private void ServiceDone_Click(object sender, RoutedEventArgs e)
        {
            var car = SelectedCar();
            if (car == null) return;
            if (car.status_id == App.StatusBusy)
            {
                MessageBox.Show("Автомобиль в смене.", "ТО", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (car.status_id == App.StatusWrittenOff)
            {
                MessageBox.Show("Автомобиль списан.", "ТО", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dlg = new MaintenanceWindow(car) { Owner = this };
            if (dlg.ShowDialog() == true) { LoadCars(); LoadMaintenance(); }
        }

        // водители
        private Driver SelectedDriver()
        {
            var d = DgDrivers.SelectedItem as Driver;
            if (d == null)
                MessageBox.Show("Выберите водителя в списке.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            return d;
        }

        private void AddDriver_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new DriverEditWindow(null) { Owner = this };
            if (dlg.ShowDialog() == true) LoadDrivers();
        }

        private void EditDriver_Click(object sender, RoutedEventArgs e)
        {
            var d = SelectedDriver();
            if (d == null) return;
            var dlg = new DriverEditWindow(d) { Owner = this };
            if (dlg.ShowDialog() == true) LoadDrivers();
        }

        private void DeleteDriver_Click(object sender, RoutedEventArgs e)
        {
            var d = SelectedDriver();
            if (d == null) return;

            int id = d.driver_id;
            if (App.Context.Waybill.Any(w => w.driver_id == id && w.end_time == null))
            {
                MessageBox.Show("У водителя не закрыта смена. Сначала она должна быть завершена.", "Увольнение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int shifts = App.Context.Waybill.Count(w => w.driver_id == id);
            string text = "Удалить водителя " + d.full_name + "?";
            if (shifts > 0) text += "\nБудет удалена и вся его история смен (" + shifts + ").";

            if (MessageBox.Show(text, "Увольнение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                App.Context.Waybill.RemoveRange(App.Context.Waybill.Where(w => w.driver_id == id).ToList());
                d.LicenseCategory.Clear();            
                App.Context.Driver.Remove(d);
                App.Context.SaveChanges();
                LoadAll();
            }
            catch (Exception ex)
            {
                App.DiscardChanges();
                MessageBox.Show("Не удалось удалить: " + ex.GetBaseException().Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentDispatcher = null;
            new LoginWindow().Show();
            Close();
        }
    }
}
