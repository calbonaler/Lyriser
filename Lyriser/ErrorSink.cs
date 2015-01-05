using System;
using System.Globalization;

namespace Lyriser
{
	public abstract class ErrorSink
	{
		public static readonly ErrorSink Null = new NullErrorSink();

		public abstract void ReportError(string description, int index);

		public abstract void Clear();

		class NullErrorSink : ErrorSink
		{
			public override void ReportError(string description, int index) { }

			public override void Clear() { }
		}
	}
}
