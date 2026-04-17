using duolingo_rum.Models;
using duolingo_rum.Services;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace duolingo_rum.ViewModels
{
    public class LanguageSelectionViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly MainViewModel _mainVM;
        private readonly string _name;
        private readonly string _email;
        private readonly string _password;

        private List<Language> _languages = new();
        private Language? _selectedTargetLanguage;
        private Language? _selectedNativeLanguage;
        private string _selectedDifficulty = "beginner";
        private bool _isLoading;
        private string _status = string.Empty;

        // ✅ Конструктор принимает данные для регистрации
        public LanguageSelectionViewModel(AuthService authService, MainViewModel mainVM,
            string name, string email, string password)
        {
            _authService = authService;
            _mainVM = mainVM;
            _name = name;
            _email = email;
            _password = password;

            RegisterCommand = ReactiveCommand.CreateFromTask(CompleteRegistration);
            BackCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new RegisterViewModel(_authService, _mainVM);
            });

            Task.Run(async () => await LoadLanguages());
        }

        public List<Language> Languages
        {
            get => _languages;
            set => this.RaiseAndSetIfChanged(ref _languages, value);
        }

        public Language? SelectedTargetLanguage
        {
            get => _selectedTargetLanguage;
            set => this.RaiseAndSetIfChanged(ref _selectedTargetLanguage, value);
        }

        public Language? SelectedNativeLanguage
        {
            get => _selectedNativeLanguage;
            set => this.RaiseAndSetIfChanged(ref _selectedNativeLanguage, value);
        }

        public string SelectedDifficulty
        {
            get => _selectedDifficulty;
            set => this.RaiseAndSetIfChanged(ref _selectedDifficulty, value);
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
        public ReactiveCommand<Unit, Unit> BackCommand { get; }

        private async Task LoadLanguages()
        {
            try
            {
                IsLoading = true;

                var languages = await _authService.GetAllLanguages();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Languages = languages;

                    // Выбираем английский и русский по умолчанию
                    SelectedTargetLanguage = Languages.FirstOrDefault(l => l.Code == "en");
                    SelectedNativeLanguage = Languages.FirstOrDefault(l => l.Code == "ru");

                    Debug.WriteLine($"✅ Languages loaded: {Languages.Count}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ LoadLanguages ERROR: {ex.Message}");
                Status = $"Ошибка загрузки языков: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CompleteRegistration()
        {
            if (SelectedTargetLanguage == null)
            {
                Status = "❌ Выберите язык для изучения";
                return;
            }

            if (SelectedNativeLanguage == null)
            {
                Status = "❌ Выберите родной язык";
                return;
            }

            IsLoading = true;
            Status = string.Empty;

            try
            {
                // ✅ Регистрируем пользователя с выбранными языками
                var result = await _authService.RegisterWithLanguage(
                    _name,
                    _email,
                    _password,
                    SelectedTargetLanguage.Id,
                    SelectedNativeLanguage.Id,
                    SelectedDifficulty
                );

                if (result.Success)
                {
                    Status = "✅ Регистрация успешна!";
                    await Task.Delay(500);
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
                Debug.WriteLine($"CompleteRegistration error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}