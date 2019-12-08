using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace Lyriser.ViewModels
{
	public class HotKeyCommand : ICommand, INotifyPropertyChanged
	{
		public HotKeyCommand(Action execute) : this(execute, AlwaysExecute) { }

		public HotKeyCommand(Action execute, Func<bool> canExecute)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		readonly Action _execute;
		readonly Func<bool> _canExecute;
		static readonly Func<bool> AlwaysExecute = () => true;

		KeyGesture _gesture;
		public KeyGesture Gesture
		{
			get => _gesture;
			set => Utils.SetPropertyWithRelated(ref _gesture, value, PropertyChanged, this, new[] { nameof(GestureText) });
		}

		public string GestureText => Gesture?.GetDisplayStringForCulture(CultureInfo.CurrentUICulture);

		public void Execute() => _execute();
		public bool CanExecute() => _canExecute();
		public void RaiseCanExecuteChanged() => CanExecuteChanged(this, EventArgs.Empty);

		void ICommand.Execute(object parameter) => Execute();
		bool ICommand.CanExecute(object parameter) => CanExecute();

		public event EventHandler CanExecuteChanged;
		public event PropertyChangedEventHandler PropertyChanged;
	}
}
