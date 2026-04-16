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
    public class VocabularyWordItem
    {
        public Word Word { get; set; } = null!;
        public WordProgress? Progress { get; set; }
        public bool IsLearned => Progress?.IsLearned == true;
        public int CorrectCount => Progress?.CorrectCount ?? 0;
        public int WrongCount => Progress?.WrongCount ?? 0;
        public string NextReviewText => Progress?.NextReview != null
            ? $"Повтор: {Progress.NextReview:dd.MM}"
            : "Новое";
        public string StatusEmoji => IsLearned ? "✅" : (Progress != null ? "📖" : "🆕");
    }

    public class VocabularyGroupItem
    {
        public string TopicName { get; set; } = "Без темы";
        public string TopicEmoji { get; set; } = "📝";
        public List<VocabularyWordItem> Words { get; set; } = new();
        public int LearnedCount => Words.Count(w => w.IsLearned);
        public int TotalCount => Words.Count;
        public string Progress => $"{LearnedCount}/{TotalCount}";
    }

    public class VocabularyViewModel : ViewModelBase
    {
        private readonly User _user;
        private readonly MainViewModel _mainVM;
        private readonly WordService _wordService;

        private bool _isLoading;
        private List<VocabularyGroupItem> _groups = new();
        private string _searchText = string.Empty;
        private List<VocabularyGroupItem> _filteredGroups = new();

        public VocabularyViewModel(User user, MainViewModel mainVM)
        {
            _user = user;
            _mainVM = mainVM;
            _wordService = new WordService();

            BackCommand = ReactiveCommand.Create(() =>
            {
                _mainVM.CurrentView = new DashboardViewModel(_user, _mainVM);
            });

            this.WhenAnyValue(x => x.SearchText)
                .Subscribe(_ => ApplyFilter());

            Task.Run(async () => await LoadVocabulary());
        }

        public bool IsLoading { get => _isLoading; set => this.RaiseAndSetIfChanged(ref _isLoading, value); }
        public List<VocabularyGroupItem> Groups { get => _groups; set => this.RaiseAndSetIfChanged(ref _groups, value); }
        public List<VocabularyGroupItem> FilteredGroups { get => _filteredGroups; set => this.RaiseAndSetIfChanged(ref _filteredGroups, value); }
        public string SearchText { get => _searchText; set => this.RaiseAndSetIfChanged(ref _searchText, value); }

        public ReactiveCommand<Unit, Unit> BackCommand { get; }

        private async Task LoadVocabulary()
        {
            IsLoading = true;
            try
            {
                var raw = await _wordService.GetVocabulary(_user.Id);

                var groups = new List<VocabularyGroupItem>();
                foreach (var (topic, words) in raw)
                {
                    var group = new VocabularyGroupItem
                    {
                        TopicName = topic?.Name ?? "Без темы",
                        TopicEmoji = topic?.IconEmoji ?? "📝",
                        Words = words.Select(w => new VocabularyWordItem
                        {
                            Word = w.Word,
                            Progress = w.Progress
                        }).ToList()
                    };
                    groups.Add(group);
                }

                Groups = groups;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadVocabulary ERROR: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredGroups = Groups;
                return;
            }

            var q = SearchText.Trim().ToLower();
            var result = new List<VocabularyGroupItem>();

            foreach (var group in Groups)
            {
                var filtered = group.Words
                    .Where(w => w.Word.Word1.ToLower().Contains(q)
                             || w.Word.Translation.ToLower().Contains(q))
                    .ToList();

                if (filtered.Count > 0)
                {
                    result.Add(new VocabularyGroupItem
                    {
                        TopicName = group.TopicName,
                        TopicEmoji = group.TopicEmoji,
                        Words = filtered
                    });
                }
            }

            FilteredGroups = result;
        }
    }
}