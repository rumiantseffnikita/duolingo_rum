using Avalonia.Controls;
using Avalonia.Controls.Templates;
using duolingo_rum.ViewModels;
using duolingo_rum.Views;

namespace duolingo_rum
{
    public class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            return data switch
            {
                LoginViewModel vm => new LoginView { DataContext = vm },
                RegisterViewModel vm => new RegisterView { DataContext = vm },
                LanguageSelectionViewModel vm => new LanguageSelectionView { DataContext = vm },
                DashboardViewModel vm => new DashboardView { DataContext = vm },
                LessonViewModel vm => new LessonView { DataContext = vm },
                StatisticsViewModel vm => new StatisticsView { DataContext = vm },
                VocabularyViewModel vm => new VocabularyView { DataContext = vm },
                AchievementsViewModel vm => new AchievementsView { DataContext = vm },
                ProfileViewModel vm => new ProfileView { DataContext = vm },
                _ => new TextBlock { Text = $"View not found for: {data?.GetType().Name}" }
            };
        }

        public bool Match(object? data)
        {
            return data is LoginViewModel
                or RegisterViewModel
                or LanguageSelectionViewModel
                or DashboardViewModel
                or LessonViewModel
                or StatisticsViewModel
                or VocabularyViewModel
                or AchievementsViewModel
                or ProfileViewModel;
        }
    }
}