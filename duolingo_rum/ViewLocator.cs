// ViewLocator.cs
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
                DashboardViewModel vm => new DashboardView { DataContext = vm },
                LessonViewModel vm => new LessonView { DataContext = vm },
                _ => new TextBlock { Text = $"View not found for: {data?.GetType().Name}" }
            };
        }

        public bool Match(object? data)
        {
            return data is LoginViewModel or DashboardViewModel or LessonViewModel;
        }
    }
}