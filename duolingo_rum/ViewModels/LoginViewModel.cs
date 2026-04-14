using duolingo_rum.Services;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace duolingo_rum.ViewModels
{
    public class LoginViewModel : ReactiveObject
    {
        private readonly AuthService _authService;
        private readonly MainViewModel _mainVM;

        public LoginViewModel(AuthService authService, MainViewModel mainVM)
        {
            _authService = authService;
            _mainVM = mainVM;

            LoginCommand = ReactiveCommand.CreateFromTask(Login);
            RegisterCommand = ReactiveCommand.CreateFromTask(Register);

            RegisterCommand.ThrownExceptions.Subscribe(ex =>
            {
                Status = ex.Message;
            });

            LoginCommand.ThrownExceptions.Subscribe(ex =>
            {
                Status = ex.Message;
            });
        }

        private string _email;
        public string Email
        {
            get => _email;
            set => this.RaiseAndSetIfChanged(ref _email, value);
        }

        private string _password;
        public string Password
        {
            get => _password;
            set => this.RaiseAndSetIfChanged(ref _password, value);
        }

        private string _name;
        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        private string _status;
        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public ReactiveCommand<Unit, Unit> LoginCommand { get; }
        public ReactiveCommand<Unit, Unit> RegisterCommand { get; }


        private async Task Login()
        {
            var result = await _authService.Login(Email, Password);

            Status = result.Message;

            if (result.Success)
            {
                _mainVM.CurrentView = new DashboardViewModel(result.User, _mainVM);
            }
        }

        private async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Name) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                Status = "Заполни все поля";
                return;
            }

            try
            {
                var result = await _authService.Register(Name, Email, Password);

                Status = result.Message;

                if (result.Success)
                {
                    _mainVM.CurrentView = new DashboardViewModel(result.User, _mainVM);
                }
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }
    }
}