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
    public class ProfileViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly MainViewModel _mainVM;
        private readonly WordService _wordService;

        private string _name = string.Empty;
        private string _email = string.Empty;
        private int _dailyGoalWords;
        private int _dailyGoalMinutes;
        private bool _isLoading;
        private string _saveMessage = string.Empty;

        private Language? _selectedTargetLanguage;
        private Language? _selectedNativeLanguage;
        private string _selectedDifficulty = "beginner";
        private List<Language> _languages = new();
        public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

        public ProfileViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _wordService = new WordService();

            // Заполняем текущими данными
            Name = user.Name ?? string.Empty;
            Email = user.Email ?? string.Empty;
            DailyGoalWords = user.DailyGoalWords ?? 10;
            DailyGoalMinutes = user.DailyGoalMinutes ?? 15;

            SaveCommand = ReactiveCommand.CreateFromTask(SaveProfile);
            BackCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
            });

            LogoutCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new LoginViewModel(new AuthService(new _43pRumiantsefContext()), _mainVM);
            });

            // Загружаем языки при создании ViewModel
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

        public int DailyGoalWords
        {
            get => _dailyGoalWords;
            set => this.RaiseAndSetIfChanged(ref _dailyGoalWords, value);
        }

        public int DailyGoalMinutes
        {
            get => _dailyGoalMinutes;
            set => this.RaiseAndSetIfChanged(ref _dailyGoalMinutes, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string SaveMessage
        {
            get => _saveMessage;
            set => this.RaiseAndSetIfChanged(ref _saveMessage, value);
        }

        // Для отображения — только чтение
        public int TotalXp => _user.TotalXp ?? 0;
        public int StreakDays => _user.StreakDays ?? 0;
        public string MemberSince => _user.CreatedAt?.ToString("MMMM yyyy") ?? "—";

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> BackCommand { get; }

        private async Task LoadLanguages()
        {
            try
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = true;
                });

                using var context = new _43pRumiantsefContext();
                var languages = await context.Languages
                    .Where(l => l.IsActive == true)
                    .OrderBy(l => l.Name)
                    .ToListAsync();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Languages = languages;

                    SelectedTargetLanguage = Languages.FirstOrDefault(l => l.Id == _user.TargetLanguageId);
                    SelectedNativeLanguage = Languages.FirstOrDefault(l => l.Id == _user.NativeLanguageId);
                    SelectedDifficulty = _user.DifficultyLevel ?? "beginner";

                    Debug.WriteLine($"✅ Languages loaded: {Languages.Count}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ LoadLanguages ERROR: {ex.Message}");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    SaveMessage = $"Ошибка загрузки языков: {ex.Message}";
                });
            }
            finally
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    IsLoading = false;
                });
            }
        }

        private async Task SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                SaveMessage = "❌ Имя не может быть пустым";
                return;
            }

            IsLoading = true;
            SaveMessage = string.Empty;

            try
            {
                using var context = new _43pRumiantsefContext();

                var user = await context.Users
                    .FirstOrDefaultAsync(u => u.Id == _user.Id);

                if (user != null)
                {
                    user.Name = Name.Trim();
                    user.DailyGoalWords = Math.Clamp(DailyGoalWords, 1, 100);
                    user.DailyGoalMinutes = Math.Clamp(DailyGoalMinutes, 1, 120);
                    user.UpdatedAt = DateTime.UtcNow;

                    if (SelectedTargetLanguage != null)
                        user.TargetLanguageId = SelectedTargetLanguage.Id;
                    if (SelectedNativeLanguage != null)
                        user.NativeLanguageId = SelectedNativeLanguage.Id;

                    // ✅ Валидация difficulty_level - только допустимые значения
                    string validDifficulty = SelectedDifficulty?.ToLower() switch
                    {
                        "beginner" => "beginner",
                        "intermediate" => "intermediate",
                        "advanced" => "advanced",
                        _ => "beginner"  // значение по умолчанию
                    };
                    user.DifficultyLevel = validDifficulty;

                    Debug.WriteLine($"Saving difficulty: {validDifficulty} (original: {SelectedDifficulty})");

                    int saved = await context.SaveChangesAsync();
                    Debug.WriteLine($"✅ Saved {saved} changes");

                    // Обновляем локальный объект
                    _user.Name = user.Name;
                    _user.DailyGoalWords = user.DailyGoalWords;
                    _user.DailyGoalMinutes = user.DailyGoalMinutes;
                    _user.TargetLanguageId = user.TargetLanguageId;
                    _user.NativeLanguageId = user.NativeLanguageId;
                    _user.DifficultyLevel = user.DifficultyLevel;

                    SaveMessage = "✅ Профиль сохранён!";

                    await Task.Delay(3000);
                    if (SaveMessage == "✅ Профиль сохранён!")
                        SaveMessage = string.Empty;
                }
                else
                {
                    SaveMessage = "❌ Пользователь не найден";
                }
            }
            catch (DbUpdateException dbEx)
            {
                var innerEx = dbEx.InnerException;
                SaveMessage = $"❌ Ошибка БД: {innerEx?.Message ?? dbEx.Message}";
                Debug.WriteLine($"❌ DbUpdateException: {dbEx.Message}");
                if (innerEx != null)
                    Debug.WriteLine($"Inner: {innerEx.Message}");
            }
            catch (Exception ex)
            {
                SaveMessage = $"❌ Ошибка: {ex.Message}";
                Debug.WriteLine($"❌ SaveProfile ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}