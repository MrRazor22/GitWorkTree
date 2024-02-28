using System.Globalization;
using System.Windows.Data;
using System.Windows;
using GitWorkTree.Commands;

namespace GitWorkTree.View.Converters
{
    public class CommandTypeToForceCheckBoxTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Create) ? "Force create" : "Force remove";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CommandTypeToCreateRemoveButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Create) ? "Create" : "Remove";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CommandTypeToBranchWorktreeLableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Create) ? "Branch name:" : "Worktrees:";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CreateCommandTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Create) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible; // Default to Visible if the value is not a CommandTypeEnum
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ManageCommandTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Manage) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible; // Default to Visible if the value is not a CommandTypeEnum
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
