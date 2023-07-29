using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32.UI.Input.Ime;
using PInvoke = Windows.Win32.PInvoke;

namespace Lyriser.Core.Ime;

public static class Ime
{
	public static unsafe MonoRuby GetMonoRuby(ReadOnlySpan<char> text)
	{
		if (text.Length == 0)
			return new MonoRuby() { Text = string.Empty, Indexes = new ushort[] { 0 } };
		var languageType = Type.GetTypeFromProgID("MSIME.Japan");
		Debug.Assert(languageType is not null);
		var language = (IFELanguage?)Activator.CreateInstance(languageType);
		Debug.Assert(language is not null);
		try
		{
			language.Open().ThrowOnFailure();
			try
			{
				MORRSLT* result;
				fixed (char* pText = text)
					language.GetJMorphResult(PInvoke.FELANG_REQ_REV, PInvoke.FELANG_CMODE_MONORUBY, text.Length, new(pText), null, &result).ThrowOnFailure();
				try
				{
					var output = new ReadOnlySpan<char>(result->pwchOutput, result->cchOutput);
					var indexes = new ReadOnlySpan<ushort>(result->paMonoRubyPos, text.Length + 1);
					return new MonoRuby() { Text = output.ToString(), Indexes = indexes.ToArray() };
				}
				finally { Marshal.FreeCoTaskMem((nint)result); }
			}
			finally { language.Close(); }
		}
		finally { Marshal.ReleaseComObject(language); }
	}
}

public record struct MonoRuby(string Text, ushort[] Indexes)
{
	public const ushort UnmatchedPosition = 0xffff;
}
