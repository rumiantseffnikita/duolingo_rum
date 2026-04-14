using ReactiveUI;

namespace duolingo_rum.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        private object _currentView;

        public object CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }
    }
}