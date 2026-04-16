using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using duolingo_rum.Models;
using duolingo_rum.Services;
using ReactiveUI;

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
        }

        public string Name { get => _name; set => this.RaiseAndSetIfChanged(ref _name, value); }
        public string Email { get => _email; set => this.RaiseAndSetIfChanged(ref _email, value); }
        public int DailyGoalWords { get => _dailyGoalWords; set => this.RaiseAndSetIfChanged(ref _dailyGoalWords, value); }
        public int DailyGoalMinutes { get => _dailyGoalMinutes; set => this.RaiseAndSetIfChanged(ref _dailyGoalMinutes, value); }
        public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
        public string SaveMessage { get => _saveMessage; set => this.RaiseAndSetIfChanged(ref _saveMessage, value); }

        // Для отображения — только чтение
        public int TotalXp => _user.TotalXp ?? 0;
        public int StreakDays => _user.StreakDays ?? 0;
        public string MemberSince => _user.CreatedAt?.ToString("MMMM yyyy") ?? "—";

        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> BackCommand { get; }

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
                var user = await context.Users.FindAsync(_user.Id);
                if (user != null)
                {
                    user.Name = Name.Trim();
                    user.DailyGoalWords = Math.Clamp(DailyGoalWords, 1, 100);
                    user.DailyGoalMinutes = Math.Clamp(DailyGoalMinutes, 1, 120);
                    user.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();

                    // Обновляем локальный объект
                    _user.Name = user.Name;
                    _user.DailyGoalWords = user.DailyGoalWords;
                    _user.DailyGoalMinutes = user.DailyGoalMinutes;

                    SaveMessage = "✅ Профиль сохранён!";
                    Debug.WriteLine("Profile saved successfully");
                }
            }
            catch (Exception ex)
            {
                SaveMessage = $"❌ Ошибка: {ex.Message}";
                Debug.WriteLine($"SaveProfile ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}