using duolingo_rum.Services;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;

namespace duolingo_rum.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly MainViewModel _mainVM;

        private string _name = string.Empty;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;
        private bool _isLoading;
        private string _status = string.Empty;

        public RegisterViewModel(AuthService authService, MainViewModel mainVM)
        {
            _authService = authService;
            _mainVM = mainVM;

            RegisterCommand = ReactiveCommand.CreateFromTask(Register);
            GoToLoginCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new LoginViewModel(_authService, _mainVM);
            });
        }

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Email
        {
            get => _email;
            set => this.RaiseAndSetIfChanged(ref _email, value);
        }

        public string Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => this.RaiseAndSetIfChanged(ref _confirmPassword, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToLoginCommand { get; }

        private async Task Register()
        {
            // Валидация
            if (string.IsNullOrWhiteSpace(Name))
            {
                Status = "❌ Введите имя";
                return;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                Status = "❌ Введите email";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                Status = "❌ Введите пароль";
                return;
            }

            if (Password != ConfirmPassword)
            {
                Status = "❌ Пароли не совпадают";
                return;
            }

            if (Password.Length < 4)
            {
                Status = "❌ Пароль должен быть не менее 4 символов";
                return;
            }

            IsLoading = true;
            Status = string.Empty;

            try
            {
                // ✅ Переходим к выбору языка (без регистрации, передаём данные)
                _mainVM.CurrentView = new LanguageSelectionViewModel(_authService, _mainVM, Name, Email, Password);
            }
            catch (Exception ex)
            {
                Status = $"❌ Ошибка: {ex.Message}";
                Debug.WriteLine($"Register error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}