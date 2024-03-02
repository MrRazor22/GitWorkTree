using EnvDTE;
using GitWorkTree.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace GitWorkTree.ViewModel
{
    public class BaseViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        // Implement INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region IDataErrorInfo 
        public string this[string columnName] => Validate(columnName);

        public string Error => null;

        protected virtual string Validate(string propertyName)
        {
            // Implemented validation logic for specific properties in derived classes
            return null;
        }
        #endregion
    }
}
