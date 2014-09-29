using System;
using System.Globalization;

namespace Lyriser
{
	public abstract class ErrorSink
	{
		public static readonly ErrorSink Null = new NullErrorSink();
		public static readonly ErrorSink Throw = new ThrowErrorSink();

		public abstract void ReportError(string description, int index);

		public abstract void Clear();

		class NullErrorSink : ErrorSink
		{
			public override void ReportError(string description, int index) { }

			public override void Clear() { }
		}

		class ThrowErrorSink : ErrorSink
		{
			public override void ReportError(string description, int index) { throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, "{0} ({1})", description, index)); }

			public override void Clear() { }
		}
	}
}
