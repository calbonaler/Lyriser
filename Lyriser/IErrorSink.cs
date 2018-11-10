namespace Lyriser
{
	public interface IErrorSink
	{
		void ReportError(string description, int index);

		void Clear();
	}
}
