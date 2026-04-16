using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using duolingo_rum.Models;
using duolingo_rum.Services;
using ReactiveUI;

namespace duolingo_rum.ViewModels
{
    public class LessonViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly MainViewModel _mainVM;
        private readonly AIService _aiService;
        private readonly WordService _wordService;
        private readonly AchievementService _achievementService;

        private List<Word> _words = new();
        private int _currentWordIndex;
        private Word? _currentWord;
        private string _userAnswer = string.Empty;
        private string _feedback = string.Empty;
        private string _exampleSentence = string.Empty;
        private int _score;
        private int _total;
        private bool _showFeedback;
        private bool _isLoading;
        private bool _isLoadingExample;
        private bool _showExample;
        private bool _showNewAchievements;
        private List<Achievement> _newAchievements = new();

        public LessonViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _aiService = new AIService();
            _wordService = new WordService();
            _achievementService = new AchievementService();

            CheckAnswerCommand = ReactiveCommand.CreateFromTask(CheckAnswer);
            NextWordCommand = ReactiveCommand.Create(NextWord);
            EndLessonCommand = ReactiveCommand.Create(EndLesson);
            ShowExampleCommand = ReactiveCommand.CreateFromTask(LoadExample);
            CloseAchievementsCommand = ReactiveCommand.Create(CloseAchievements);

            Task.Run(async () => await LoadWords());
        }

        // ── Свойства ──────────────────────────────

        public Word? CurrentWord
        {
            get => _currentWord;
            set => this.RaiseAndSetIfChanged(ref _currentWord, value);
        }

        public string UserAnswer
        {
            get => _userAnswer;
            set => this.RaiseAndSetIfChanged(ref _userAnswer, value);
        }

        public string Feedback
        {
            get => _feedback;
            set => this.RaiseAndSetIfChanged(ref _feedback, value);
        }

        public string ExampleSentence
        {
            get => _exampleSentence;
            set => this.RaiseAndSetIfChanged(ref _exampleSentence, value);
        }

        public int Score
        {
            get => _score;
            set => this.RaiseAndSetIfChanged(ref _score, value);
        }

        public int Total
        {
            get => _total;
            set => this.RaiseAndSetIfChanged(ref _total, value);
        }

        public bool ShowFeedback
        {
            get => _showFeedback;
            set => this.RaiseAndSetIfChanged(ref _showFeedback, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public bool IsLoadingExample
        {
            get => _isLoadingExample;
            set => this.RaiseAndSetIfChanged(ref _isLoadingExample, value);
        }

        public bool ShowExample
        {
            get => _showExample;
            set => this.RaiseAndSetIfChanged(ref _showExample, value);
        }

        public bool ShowNewAchievements
        {
            get => _showNewAchievements;
            set => this.RaiseAndSetIfChanged(ref _showNewAchievements, value);
        }

        public List<Achievement> NewAchievements
        {
            get => _newAchievements;
            set => this.RaiseAndSetIfChanged(ref _newAchievements, value);
        }

        public bool IsAnswered => ShowFeedback;
        public bool IsLastWord => _currentWordIndex >= (_words?.Count ?? 0) - 1;

        // ── Команды ───────────────────────────────

        public ReactiveCommand<Unit, Unit> CheckAnswerCommand { get; }
        public ReactiveCommand<Unit, Unit> NextWordCommand { get; }
        public ReactiveCommand<Unit, Unit> EndLessonCommand { get; }
        public ReactiveCommand<Unit, Unit> ShowExampleCommand { get; }
        public ReactiveCommand<Unit, Unit> CloseAchievementsCommand { get; }

        // ── Логика ────────────────────────────────

        private async Task LoadWords()
        {
            try
            {
                IsLoading = true;

                // ✅ SRS: умный подбор слов
                _words = await _wordService.GetWordsForLessonSRS(_user.Id, 10);

                if (_words == null || _words.Count == 0)
                    _words = await _wordService.GetAnyWords(5);

                if (_words == null || _words.Count == 0)
                    _words = CreateTestWords();

                _currentWordIndex = 0;
                Total = _words.Count;
                Score = 0;
                ShowNextWord();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadWords error: {ex.Message}");
                Feedback = $"Ошибка загрузки: {ex.Message}";
                ShowFeedback = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ShowNextWord()
        {
            if (_words != null && _currentWordIndex < _words.Count)
            {
                CurrentWord = _words[_currentWordIndex];
                UserAnswer = string.Empty;
                Feedback = string.Empty;
                ExampleSentence = string.Empty;
                ShowFeedback = false;
                ShowExample = false;
                this.RaisePropertyChanged(nameof(IsAnswered));
                this.RaisePropertyChanged(nameof(IsLastWord));
            }
        }

        private async Task CheckAnswer()
        {
            if (CurrentWord == null || string.IsNullOrWhiteSpace(UserAnswer))
            {
                Feedback = string.IsNullOrWhiteSpace(UserAnswer) ? "Введите ответ!" : "Ошибка: слово не загружено";
                ShowFeedback = true;
                return;
            }

            IsLoading = true;

            try
            {
                bool isCorrect = UserAnswer.Trim()
                    .Equals(CurrentWord.Translation, StringComparison.OrdinalIgnoreCase);

                if (isCorrect)
                {
                    Score++;
                    // ✅ AI похвала при правильном ответе (иногда)
                    var praise = await _aiService.GeneratePraise(CurrentWord);
                    Feedback = string.IsNullOrEmpty(praise)
                        ? $"✅ Правильно! {CurrentWord.Word1} — {CurrentWord.Translation}"
                        : $"✅ {praise}";
                }
                else
                {
                    // ✅ AI фидбек при ошибке
                    Feedback = await _aiService.GenerateFeedback(CurrentWord, UserAnswer);
                }

                try
                {
                    await _wordService.SaveExerciseResult(_user.Id, CurrentWord.Id, isCorrect, UserAnswer, Feedback);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Save error: {ex.Message}");
                }

                ShowFeedback = true;
                this.RaisePropertyChanged(nameof(IsAnswered));
                this.RaisePropertyChanged(nameof(IsLastWord));
            }
            catch (Exception ex)
            {
                Feedback = $"Ошибка: {ex.Message}";
                ShowFeedback = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// ✅ Загрузка AI-примера прямо в уроке по кнопке.
        /// </summary>
        private async Task LoadExample()
        {
            if (CurrentWord == null) return;
            IsLoadingExample = true;
            ShowExample = false;
            try
            {
                ExampleSentence = await _aiService.GenerateExampleSentence(CurrentWord);
                ShowExample = true;
            }
            catch (Exception ex)
            {
                ExampleSentence = $"Ошибка: {ex.Message}";
                ShowExample = true;
            }
            finally
            {
                IsLoadingExample = false;
            }
        }

        private void NextWord()
        {
            _currentWordIndex++;
            if (_currentWordIndex < (_words?.Count ?? 0))
                ShowNextWord();
            else
                EndLesson();
        }

        private void CloseAchievements()
        {
            ShowNewAchievements = false;
            // Переходим на дашборд после закрытия попапа достижений
            Dispatcher.UIThread.Post(() =>
            {
                _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
            });
        }

        /// <summary>
        /// ✅ Конец урока: закрываем сессию → проверяем достижения → показываем попап или переходим.
        /// </summary>
        private void EndLesson()
        {
            Task.Run(async () =>
            {
                try
                {
                    await _wordService.EndCurrentSession(_user.Id);
                    Debug.WriteLine("Session ended");

                    // ✅ Проверяем и выдаём достижения
                    var earned = await _achievementService.CheckAndAwardAchievements(_user.Id);
                    Debug.WriteLine($"New achievements: {earned.Count}");

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (earned.Count > 0)
                        {
                            // Показываем попап достижений — переход на дашборд из CloseAchievements
                            NewAchievements = earned;
                            ShowNewAchievements = true;
                        }
                        else
                        {
                            _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"EndLesson error: {ex.Message}");
                    Dispatcher.UIThread.Post(() =>
                    {
                        _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
                    });
                }
            });
        }

        private List<Word> CreateTestWords() => new()
        {
            new Word { Id = 1, Word1 = "Hello", Translation = "Привет" },
            new Word { Id = 2, Word1 = "World", Translation = "Мир" },
            new Word { Id = 3, Word1 = "Good", Translation = "Хороший" },
            new Word { Id = 4, Word1 = "Bad", Translation = "Плохой" },
            new Word { Id = 5, Word1 = "Love", Translation = "Любовь" }
        };
    }
}