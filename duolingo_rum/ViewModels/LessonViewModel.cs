using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using duolingo_rum.Models;
using duolingo_rum.Services;
using Microsoft.EntityFrameworkCore;
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
        private List<GeneratedWord> _generatedWords = new();
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
        private bool _isAIGenerating;
        private List<Achievement> _newAchievements = new();
        public ReactiveCommand<Unit, Unit> AbortLessonCommand { get; }

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
            AbortLessonCommand = ReactiveCommand.Create(AbortLesson);

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

        public bool IsAIGenerating
        {
            get => _isAIGenerating;
            set => this.RaiseAndSetIfChanged(ref _isAIGenerating, value);
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

        // ── Загрузка слов ─────────────────────────
        private void AbortLesson()
        {
            Task.Run(async () =>
            {
                try
                {
                    await _wordService.EndCurrentSession(_user.Id);
                    Debug.WriteLine("Lesson aborted by user");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"AbortLesson error: {ex.Message}");
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
                    });
                }
            });
        }
        private async Task LoadWords()
        {
            try
            {
                IsLoading = true;
                // Всегда загружаем слова из БД, пока AI не работает
                await LoadWordsFromDatabase();
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

        private async Task LoadWordsFromAI()
        {
            try
            {
                IsAIGenerating = true;

                // Получаем язык пользователя
                using var context = new _43pRumiantsefContext();
                var user = await context.Users
                    .Include(u => u.TargetLanguage)
                    .Include(u => u.NativeLanguage)
                    .FirstOrDefaultAsync(u => u.Id == _user.Id);

                var targetLanguage = user?.TargetLanguage?.Code ?? "en";
                var nativeLanguage = user?.NativeLanguage?.Code ?? "ru";
                var difficulty = user?.DifficultyLevel ?? "beginner";

                Debug.WriteLine($"Loading words: targetLang={targetLanguage}, nativeLang={nativeLanguage}, level={difficulty}");

                // Генерируем слова через AI
                _generatedWords = await _aiService.GenerateWordsForLesson(
                    targetLanguage,
                    nativeLanguage,
                    difficulty,
                    10
                );

                if (_generatedWords.Count > 0)
                {
                    // Преобразуем в формат Word для урока
                    var tempId = -1;
                    _words = _generatedWords.Select(g => new Word
                    {
                        Id = tempId--,
                        Word1 = g.Word,
                        Translation = g.Translation,
                        ExampleSentence = g.ExampleSentence,
                        ExampleTranslation = g.ExampleTranslation,
                        Transcription = g.Transcription
                    }).ToList();

                    _currentWordIndex = 0;
                    Total = _words.Count;
                    Score = 0;
                    ShowNextWord();

                    Debug.WriteLine($"✅ AI generated {_generatedWords.Count} words");
                }
                else
                {
                    Debug.WriteLine("No words generated, falling back to database");
                    await LoadWordsFromDatabase();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadWordsFromAI error: {ex.Message}");
                await LoadWordsFromDatabase();
            }
            finally
            {
                IsAIGenerating = false;
            }
        }

        private async Task LoadWordsFromDatabase()
        {
            _words = await _wordService.GetWordsForLessonSRS(_user.Id, 10);

            if (_words == null || _words.Count == 0)
                _words = await _wordService.GetAnyWords(5);

            if (_words == null || _words.Count == 0)
                _words = CreateTestWords();

            _currentWordIndex = 0;
            Total = _words.Count;
            Score = 0;
            ShowNextWord();

            Debug.WriteLine($"Loaded {_words.Count} words from database");
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

        // ── Проверка ответа ───────────────────────

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
                    var praise = await _aiService.GeneratePraise(CurrentWord);
                    Feedback = string.IsNullOrEmpty(praise)
                        ? $"✅ Правильно! {CurrentWord.Word1} — {CurrentWord.Translation}"
                        : $"✅ {praise}";
                }
                else
                {
                    Feedback = await _aiService.GenerateFeedback(CurrentWord, UserAnswer);
                }

                // ✅ ВАЖНО: Не сохраняем для временных слов (с отрицательным ID)
                if (CurrentWord.Id > 0)
                {
                    try
                    {
                        await _wordService.SaveExerciseResult(_user.Id, CurrentWord.Id, isCorrect, UserAnswer, Feedback);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Save error: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Skipping save for temporary word: {CurrentWord.Word1} (ID: {CurrentWord.Id})");
                    // Сохраняем в список для последующей записи
                    _pendingResults.Add(new PendingExerciseResult
                    {
                        WordText = CurrentWord.Word1,
                        IsCorrect = isCorrect,
                        UserAnswer = UserAnswer,
                        Feedback = Feedback
                    });
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

        // Добавь класс для хранения временных результатов
        private class PendingExerciseResult
        {
            public string WordText { get; set; } = string.Empty;
            public bool IsCorrect { get; set; }
            public string UserAnswer { get; set; } = string.Empty;
            public string Feedback { get; set; } = string.Empty;
        }

        private List<PendingExerciseResult> _pendingResults = new();

        // ── Пример предложения ────────────────────

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

        // ── Навигация ─────────────────────────────

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
            Dispatcher.UIThread.Post(() =>
            {
                _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
            });
        }

        // ── Завершение урока ──────────────────────

        private void EndLesson()
        {
            Task.Run(async () =>
            {
                try
                {
                    // Сохраняем сгенерированные слова в БД
                    if (_generatedWords.Count > 0 && _user.TargetLanguageId.HasValue)
                    {
                        var savedWords = await _aiService.SaveGeneratedWordsToDatabase(
                            _user.TargetLanguageId.Value,
                            _generatedWords
                        );

                        // Обновляем ID временных слов на реальные
                        await UpdateExerciseResultsWithRealIds(savedWords);
                    }

                    await _wordService.EndCurrentSession(_user.Id);
                    Debug.WriteLine("Session ended");

                    var earned = await _achievementService.CheckAndAwardAchievements(_user.Id);
                    Debug.WriteLine($"New achievements: {earned.Count}");

                    Dispatcher.UIThread.Post(() =>
                    {
                        if (earned.Count > 0)
                        {
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

        private async Task UpdateExerciseResultsWithRealIds(List<Word> savedWords)
        {
            try
            {
                using var context = new _43pRumiantsefContext();

                // Создаём маппинг временных ID на реальные
                var wordMap = new Dictionary<int, int>();
                for (int i = 0; i < _words.Count && i < savedWords.Count; i++)
                {
                    wordMap[_words[i].Id] = savedWords[i].Id;
                }

                var activeSession = await context.LearningSessions
                    .FirstOrDefaultAsync(s => s.UserId == _user.Id && s.FinishedAt == null);

                if (activeSession != null)
                {
                    var results = await context.ExerciseResults
                        .Where(r => r.SessionId == activeSession.Id)
                        .ToListAsync();

                    foreach (var result in results)
                    {
                        if (wordMap.ContainsKey(result.WordId))
                        {
                            result.WordId = wordMap[result.WordId];
                        }
                    }

                    await context.SaveChangesAsync();
                    Debug.WriteLine("✅ Updated exercise results with real word IDs");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateExerciseResultsWithRealIds error: {ex.Message}");
            }
        }

        // ── Тестовые слова (фолбек) ───────────────

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