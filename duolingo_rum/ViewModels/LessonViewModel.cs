// ViewModels/LessonViewModel.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
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

        private List<Word> _words = new List<Word>();
        private int _currentWordIndex;
        private Word? _currentWord;
        private string _userAnswer = string.Empty;
        private string _feedback = string.Empty;
        private int _score;
        private int _total;
        private bool _showFeedback;
        private bool _isLoading;

        public LessonViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _aiService = new AIService();
            _wordService = new WordService();

            CheckAnswerCommand = ReactiveCommand.CreateFromTask(CheckAnswer);
            NextWordCommand = ReactiveCommand.Create(NextWord);
            EndLessonCommand = ReactiveCommand.Create(EndLesson);

            // Загружаем слова
            Task.Run(async () => await LoadWords());
        }

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

        // Свойства для управления кнопками
        public bool IsAnswered => ShowFeedback;
        public bool IsLastWord => _currentWordIndex >= (_words?.Count ?? 0) - 1;

        public ReactiveCommand<Unit, Unit> CheckAnswerCommand { get; }
        public ReactiveCommand<Unit, Unit> NextWordCommand { get; }
        public ReactiveCommand<Unit, Unit> EndLessonCommand { get; }

        private async Task LoadWords()
        {
            try
            {
                IsLoading = true;

                _words = await _wordService.GetWordsForLesson(_user.Id);

                if (_words == null || _words.Count == 0)
                {
                    _words = await _wordService.GetAnyWords(5);
                }

                if (_words == null || _words.Count == 0)
                {
                    _words = CreateTestWords();
                }

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

        private List<Word> CreateTestWords()
        {
            return new List<Word>
            {
                new Word { Id = 1, Word1 = "Hello", Translation = "Привет" },
                new Word { Id = 2, Word1 = "World", Translation = "Мир" },
                new Word { Id = 3, Word1 = "Good", Translation = "Хороший" },
                new Word { Id = 4, Word1 = "Bad", Translation = "Плохой" },
                new Word { Id = 5, Word1 = "Love", Translation = "Любовь" }
            };
        }

        private void ShowNextWord()
        {
            if (_words != null && _currentWordIndex < _words.Count)
            {
                CurrentWord = _words[_currentWordIndex];
                UserAnswer = string.Empty;
                Feedback = string.Empty;
                ShowFeedback = false; // Скрываем фидбек, показываем поле ввода
                this.RaisePropertyChanged(nameof(IsAnswered));
                this.RaisePropertyChanged(nameof(IsLastWord));
            }
        }

        private async Task CheckAnswer()
        {
            if (CurrentWord == null)
            {
                Feedback = "Ошибка: слово не загружено";
                ShowFeedback = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(UserAnswer))
            {
                Feedback = "Введите ответ!";
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
                    Feedback = $"✅ Правильно! {CurrentWord.Word1} - {CurrentWord.Translation}";
                }
                else
                {
                    Feedback = await _aiService.GenerateFeedback(CurrentWord, UserAnswer);
                }

                // Сохраняем результат
                try
                {
                    await _wordService.SaveExerciseResult(_user.Id, CurrentWord.Id, isCorrect, UserAnswer, Feedback);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Save error: {ex.Message}");
                }

                ShowFeedback = true; // Показываем фидбек и кнопку "Далее"
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

        private void NextWord()
        {
            _currentWordIndex++;

            if (_currentWordIndex < (_words?.Count ?? 0))
            {
                ShowNextWord();
            }
            else
            {
                // Урок закончен
                EndLesson();
            }
        }

        private async void EndLesson()
        {
            try
            {
                await _wordService.EndCurrentSession(_user.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EndSession error: {ex.Message}");
            }

            _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
        }
    }
}