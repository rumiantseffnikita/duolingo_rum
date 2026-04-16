using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using duolingo_rum.Models;
using duolingo_rum.Services;
using ReactiveUI;

namespace duolingo_rum.ViewModels
{
    public class AchievementItem
    {
        public Achievement Achievement { get; set; } = null!;
        public bool IsEarned { get; set; }
        public DateTime? EarnedAt { get; set; }
        public string EarnedText => IsEarned
            ? $"Получено {EarnedAt?.ToString("dd.MM.yyyy") ?? ""}"
            : "Не получено";
        public double Opacity => IsEarned ? 1.0 : 0.4;
    }

    public class AchievementsViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly MainViewModel _mainVM;
        private readonly AchievementService _achievementService;

        private bool _isLoading;
        private List<AchievementItem> _achievements = new();
        private int _earnedCount;
        private int _totalCount;

        public AchievementsViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _achievementService = new AchievementService();

            BackCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
            });

            Task.Run(async () => await LoadAchievements());
        }

        public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
        public List<AchievementItem> Achievements { get => _achievements; set => this.RaiseAndSetIfChanged(ref _achievements, value); }
        public int EarnedCount { get => _earnedCount; set => this.RaiseAndSetIfChanged(ref _earnedCount, value); }
        public int TotalCount { get => _totalCount; set => this.RaiseAndSetIfChanged(ref _totalCount, value); }

        public ReactiveCommand<Unit, Unit> BackCommand { get; }

        private async Task LoadAchievements()
        {
            IsLoading = true;
            try
            {
                var data = await _achievementService.GetAllAchievementsWithStatus(_user.Id);

                var items = new List<AchievementItem>();
                int earned = 0;

                foreach (var (achievement, isEarned, earnedAt) in data)
                {
                    items.Add(new AchievementItem
                    {
                        Achievement = achievement,
                        IsEarned = isEarned,
                        EarnedAt = earnedAt
                    });
                    if (isEarned) earned++;
                }

                Achievements = items;
                EarnedCount = earned;
                TotalCount = items.Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadAchievements ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}