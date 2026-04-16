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
    public class StatisticsViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly MainViewModel _mainVM;
        private readonly StatisticsService _statsService;
        private readonly AIService _aiService;

        private bool _isLoading;
        private int _totalXp;
        private int _streakDays;
        private int _longestStreak;
        private int _totalWordsLearned;
        private int _totalSessions;
        private int _accuracyPercent;
        private int _todayCorrect;
        private int _weeklyAnswers;
        private string _aiAnalysis = string.Empty;
        private List<LearningSession> _recentSessions = new();

        public StatisticsViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _statsService = new StatisticsService();
            _aiService = new AIService();

            BackCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
            });

            Task.Run(async () => await LoadStats());
        }

        public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
        public int TotalXp { get => _totalXp; set => this.RaiseAndSetIfChanged(ref _totalXp, value); }
        public int StreakDays { get => _streakDays; set => this.RaiseAndSetIfChanged(ref _streakDays, value); }
        public int LongestStreak { get => _longestStreak; set => this.RaiseAndSetIfChanged(ref _longestStreak, value); }
        public int TotalWordsLearned { get => _totalWordsLearned; set => this.RaiseAndSetIfChanged(ref _totalWordsLearned, value); }
        public int TotalSessions { get => _totalSessions; set => this.RaiseAndSetIfChanged(ref _totalSessions, value); }
        public int AccuracyPercent { get => _accuracyPercent; set => this.RaiseAndSetIfChanged(ref _accuracyPercent, value); }
        public int TodayCorrect { get => _todayCorrect; set => this.RaiseAndSetIfChanged(ref _todayCorrect, value); }
        public int WeeklyAnswers { get => _weeklyAnswers; set => this.RaiseAndSetIfChanged(ref _weeklyAnswers, value); }
        public string AiAnalysis { get => _aiAnalysis; set => this.RaiseAndSetIfChanged(ref _aiAnalysis, value); }
        public List<LearningSession> RecentSessions { get => _recentSessions; set => this.RaiseAndSetIfChanged(ref _recentSessions, value); }

        public ReactiveCommand<Unit, Unit> BackCommand { get; }

        private async Task LoadStats()
        {
            IsLoading = true;
            try
            {
                var stats = await _statsService.GetUserStats(_user.Id);

                TotalXp = stats.TotalXp;
                StreakDays = stats.StreakDays;
                LongestStreak = stats.LongestStreak;
                TotalWordsLearned = stats.TotalWordsLearned;
                TotalSessions = stats.TotalSessions;
                AccuracyPercent = stats.AccuracyPercent;
                TodayCorrect = stats.TodayCorrect;
                WeeklyAnswers = stats.WeeklyAnswers;
                RecentSessions = stats.RecentSessions;

                // ✅ AI анализ слабых мест
                AiAnalysis = await _aiService.GenerateWeaknessAnalysis(
                    stats.TotalCorrectAnswers,
                    stats.TotalWrongAnswers,
                    stats.StreakDays
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadStats ERROR: {ex.Message}");
                AiAnalysis = "📊 Статистика загружается...";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}