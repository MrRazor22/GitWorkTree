using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows;
using System.Windows.Forms;
using GitWorkTree.Commands;

namespace GitWorkTree.ToolWindows.View.Converters
{
    public class CommandTypeToForceCheckBoxTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Add) ? "Force Create" : "Force Remove";
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
                return (commandType == CommandType.Add) ? "Create" : "Remove";
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
                return (commandType == CommandType.Add) ? "Branch name:" : "Worktree path:";
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
                return (commandType == CommandType.Add) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible; // Default to Visible if the value is not a CommandTypeEnum
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class RemoveCommandTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CommandType commandType)
            {
                return (commandType == CommandType.Remove) ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible; // Default to Visible if the value is not a CommandTypeEnum
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
