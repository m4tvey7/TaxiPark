using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Data.Entity;

namespace TaxiPark
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Entities.TaxiParkEntities Context { get; } = new Entities.TaxiParkEntities();
        // Пользователь, вошедший в систему: диспетчер или водитель
        public static Entities.Dispatcher CurrentDispatcher { get; set; }
        public static Entities.Driver CurrentDriver { get; set; }

        // Номера статусов из таблицы CarStatus
        public const int StatusFree = 1;        // Свободна
        public const int StatusBusy = 2;        // Занята
        public const int StatusService = 3;     // На ТО
        public const int StatusFaulty = 4;      // Неисправна
        public const int StatusWrittenOff = 5;  // Списана

        // Откат несохранённых изменений, если SaveChanges выдал ошибку
        public static void DiscardChanges()
        {
            foreach (var entry in Context.ChangeTracker.Entries().ToList())
            {
                if (entry.State == EntityState.Added) entry.State = EntityState.Detached;
                else if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted) entry.Reload();
            }
        }
    }
}
