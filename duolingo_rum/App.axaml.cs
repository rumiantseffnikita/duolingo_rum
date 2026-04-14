using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using duolingo_rum.Models;
using duolingo_rum.Services;
using duolingo_rum.ViewModels;
using duolingo_rum.Views;
using Microsoft.EntityFrameworkCore;

namespace duolingo_rum
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var options = new DbContextOptionsBuilder<_43pRumiantsefContext>()
                    .UseNpgsql("Host=edu.pg.ngknn.ru; Port=5442; Database=43P_Rumiantsef; Username=43P; Password=444444")
                    .Options;

                var context = new _43pRumiantsefContext(options);
                var authService = new AuthService(context);

                var mainVM = new MainViewModel();

                // стартовое окно — логин
                mainVM.CurrentView = new LoginViewModel(authService, mainVM);

                desktop.MainWindow = new MainWindow
                {
                    DataContext = mainVM
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}