using System.Windows;
using Livet;

namespace Lyriser.Views
{
	/// <summary>
	/// App.xaml の相互作用ロジック
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			DispatcherHelper.UIDispatcher = Dispatcher;
		}
	}
}
