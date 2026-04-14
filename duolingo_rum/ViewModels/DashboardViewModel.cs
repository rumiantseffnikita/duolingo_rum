// ViewModels/DashboardViewModel.cs
using duolingo_rum.Models;
using duolingo_rum.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace duolingo_rum.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly MainViewModel _mainVM;
        private readonly WordService _wordService;

        private string _userName;
        private int _streakDays;
        private int _totalXp;
        private int _wordsLearned;
        private int _todayGoal;
        private int _todayProgress;
        private List<Word> _wordsToReview;
        private bool _isLoading;
        private string _dailyTip;

        public DashboardViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _wordService = new WordService();

            StartLearningCommand = ReactiveCommand.CreateFromTask(StartLearning);
            RefreshCommand = ReactiveCommand.CreateFromTask(Refresh);

            LoadDashboardData();
        }

        public string UserName
        {
            get => _userName;
            set => this.RaiseAndSetIfChanged(ref _userName, value);
        }

        public int StreakDays
        {
            get => _streakDays;
            set => this.RaiseAndSetIfChanged(ref _streakDays, value);
        }

        public int TotalXp
        {
            get => _totalXp;
            set => this.RaiseAndSetIfChanged(ref _totalXp, value);
        }

        public int WordsLearned
        {
            get => _wordsLearned;
            set => this.RaiseAndSetIfChanged(ref _wordsLearned, value);
        }

        public int TodayGoal
        {
            get => _todayGoal;
            set => this.RaiseAndSetIfChanged(ref _todayGoal, value);
        }

        public int TodayProgress
        {
            get => _todayProgress;
            set => this.RaiseAndSetIfChanged(ref _todayProgress, value);
        }

        public List<Word> WordsToReview
        {
            get => _wordsToReview;
            set => this.RaiseAndSetIfChanged(ref _wordsToReview, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string DailyTip
        {
            get => _dailyTip;
            set => this.RaiseAndSetIfChanged(ref _dailyTip, value);
        }

        public ReactiveCommand<Unit, Unit> StartLearningCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        // В DashboardViewModel.cs добавьте этот метод и вызовите его в LoadDashboardData

        private async void LoadDashboardData()
        {
            IsLoading = true;

            try
            {
                UserName = _user.Name;
                StreakDays = _user.StreakDays ?? 0;
                TotalXp = _user.TotalXp ?? 0;
                TodayGoal = _user.DailyGoalWords ?? 10;

                // ОТЛАДКА: выводим ID пользователя
                Debug.WriteLine($"=== Dashboard Loaded for User ID: {_user.Id} ===");
                Debug.WriteLine($"User Name: {_user.Name}");

                // Считаем выученные слова
                WordsLearned = await _wordService.GetLearnedWordsCount(_user.Id);
                Debug.WriteLine($"WordsLearned: {WordsLearned}");

                // Считаем прогресс за сегодня
                TodayProgress = await _wordService.GetTodayProgress(_user.Id);
                Debug.WriteLine($"TodayProgress: {TodayProgress}");

                // Слова для повторения
                WordsToReview = await _wordService.GetWordsToReview(_user.Id, 5);
                Debug.WriteLine($"WordsToReview count: {WordsToReview?.Count ?? 0}");

                // Генерируем AI-совет
                DailyTip = await GenerateDailyTip();
                Debug.WriteLine($"DailyTip: {DailyTip}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadDashboardData ERROR: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                DailyTip = $"📝 Начни урок, чтобы получить персонализированный совет!";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task<string> GenerateDailyTip()
        {
            var aiService = new AIService();
            return await aiService.GenerateDailyTip(_user, WordsLearned, StreakDays);
        }

        private async Task StartLearning()
        {
            _mainVM.CurrentView = new LessonViewModel(_user, _mainVM);
        }

        private async Task Refresh()
        {
            await Task.Run(() => LoadDashboardData());
        }

    }
}