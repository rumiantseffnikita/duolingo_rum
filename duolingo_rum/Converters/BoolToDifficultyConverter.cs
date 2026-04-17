using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace duolingo_rum.Converters
{
    public class BoolToDifficultyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentDifficulty && parameter is string buttonDifficulty)
            {
                // Сравниваем строки
                return currentDifficulty == buttonDifficulty;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string difficulty)
            {
                // Возвращаем короткое значение (beginner, intermediate, advanced)
                return difficulty;
            }

            // Если ничего не выбрано, возвращаем текущее значение или beginner
            return "beginner";
        }
    }
}