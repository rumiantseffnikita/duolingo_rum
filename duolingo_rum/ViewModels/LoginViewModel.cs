using duolingo_rum.Services;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace duolingo_rum.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly MainViewModel _mainVM;

        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _status = string.Empty;
        private bool _isLoading;

        public LoginViewModel(AuthService authService, MainViewModel mainVM)
        {
            _authService = authService;
            _mainVM = mainVM;

            LoginCommand = ReactiveCommand.CreateFromTask(Login);
            GoToRegisterCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new RegisterViewModel(_authService, _mainVM);
            });
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

        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; }
        public ReactiveCommand<Unit, Unit> GoToRegisterCommand { get; }

        private async Task Login()
        {
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

            IsLoading = true;
            Status = string.Empty;

            try
            {
                var result = await _authService.Login(Email, Password);

                if (result.Success)
                {
                    _mainVM.CurrentView = new DashboardViewModel(result.User, _mainVM);
                }
                else
                {
                    Status = $"❌ {result.Message}";
                }
            }
            catch (Exception ex)
            {
                Status = $"❌ Ошибка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}