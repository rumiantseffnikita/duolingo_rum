using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using duolingo_rum.Models;
using duolingo_rum.Services;
using duolingo_rum.ViewModels;
using duolingo_rum.Views;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace duolingo_rum
{
    public partial class App : Application
    {
        // Статический конструктор для настройки SSL до инициализации приложения
        static App()
        {
            // Отключаем проверку SSL сертификатов для всех запросов (необходимо для GigaChat)
            ServicePointManager.ServerCertificateValidationCallback += ValidateServerCertificate;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
            ServicePointManager.Expect100Continue = true;
        }

        // Метод валидации сертификата (принимаем все сертификаты для GigaChat)
        private static bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        {
            // Для разработки и работы с GigaChat принимаем все сертификаты
            // В production это не рекомендуется, но для GigaChat API это необходимо
            System.Diagnostics.Debug.WriteLine($"SSL Validation: {sslPolicyErrors}");
            System.Diagnostics.Debug.WriteLine($"Certificate subject: {certificate?.Subject}");

            // Принимаем все сертификаты для GigaChat
            return true;
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var options = new DbContextOptionsBuilder<_43pRumiantsefContext>()
                    .UseNpgsql("Host=edu.pg.ngknn.ru; Port=5442; Database=43P_Rumiantsef; Username=43P; Password=444444; Include Error Detail=true")
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