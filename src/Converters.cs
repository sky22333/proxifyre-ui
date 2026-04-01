using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace proxifyre_ui
{
    public class NullToBooleanConverter : IValueConverter
    {
        public static NullToBooleanConverter Instance { get; } = new NullToBooleanConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RunningToAppearanceConverter : IValueConverter
    {
        public static RunningToAppearanceConverter Instance { get; } = new RunningToAppearanceConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning && isRunning)
            {
                return ControlAppearance.Danger; // Stop button red
            }
            return ControlAppearance.Primary; // Start button blue
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RunningToIconConverter : IValueConverter
    {
        public static RunningToIconConverter Instance { get; } = new RunningToIconConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning && isRunning)
            {
                return SymbolRegular.Stop24;
            }
            return SymbolRegular.Play24;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RunningToTextConverter : IValueConverter
    {
        public static RunningToTextConverter Instance { get; } = new RunningToTextConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning && isRunning)
            {
                return "停止代理";
            }
            return "启动代理";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public static InverseBooleanConverter Instance { get; } = new InverseBooleanConverter();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
