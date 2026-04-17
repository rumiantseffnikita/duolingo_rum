using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace duolingo_rum.Views
{
    public partial class LanguageSelectionView : UserControl
    {
        public LanguageSelectionView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}