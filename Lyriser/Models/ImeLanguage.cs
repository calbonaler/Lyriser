using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Lyriser.Models;

public interface IMonoRubyProvider
{
	MonoRuby? GetMonoRuby(string text);
}

public class ImeLanguage : IMonoRubyProvider
{
	ImeLanguage() { }

	const int S_OK = 0;
	const int FELANG_REQ_REV = 0x00030000;
	const int FELANG_CMODE_MONORUBY = 0x00000002;

	static readonly Type? s_LanguageType = Type.GetTypeFromProgID("MSIME.Japan");
	public static readonly IMonoRubyProvider Instance = new ImeLanguage();

	public MonoRuby? GetMonoRuby(string text)
	{
		if (s_LanguageType == null)
			return null;
		var language = (IFELanguage?)Activator.CreateInstance(s_LanguageType);
		// nullability: I think created object must not be null...
		Debug.Assert(language != null, "MSIME.Japan object is null");
		try
		{
			if (language.Open() != S_OK)
				return null;
			try
			{
				var res = language.GetJMorphResult(FELANG_REQ_REV, FELANG_CMODE_MONORUBY, text.Length, text, IntPtr.Zero, out var result);
				if (res != S_OK || result.IsInvalid)
					return null;
				using (result)
				{
					ReadOnlySpan<char> output;
					ReadOnlySpan<ushort> indexes;
					unsafe
					{
						var native = (MORRSLT*)result.DangerousGetHandle();
						output = new ReadOnlySpan<char>(native->Output, native->OutputLength);
						indexes = new ReadOnlySpan<ushort>(native->MonoRubyPos, text.Length + 1);
					}
					return new MonoRuby() { Text = output.ToString(), Indexes = indexes.ToArray() };
				}
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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
unsafe struct MORRSLT
{
	public uint    Size;             //                       total size of this block.
	public char*   Output;           // [OutputLength       ] conversion result string.
	public ushort  OutputLength;
	public char*   ReadOrComp;       // [ReadOrCompLength   ] reading/composition string.
	public ushort  ReadOrCompLength;
	public ushort* InputPos;         // [InputLength + 1    ] index array of reading to input character.
	public ushort* OutputIdxWDD;     // [OutputLength       ] index array of output character to WDD
	public ushort* ReadOrCompIdxWDD; // [ReadOrCompLength   ] index array of reading/composition character to WDD;
	public ushort* MonoRubyPos;      // [InputLength + 1    ] array of position of monoruby
	public void*   WordDescriptors;  // [WordDescriptorCount] pointer to array of WDD
	public int     WordDescriptorCount;
	public void*   Private;          //                              pointer of private data area
}

class MorphResultHandle : SafeHandleZeroOrMinusOneIsInvalid
{
	protected MorphResultHandle() : base(true) { }
	protected override bool ReleaseHandle()
	{
		Marshal.FreeCoTaskMem(handle);
		return true;
	}
}

[ComImport]
[Guid("019F7152-E6DB-11D0-83C3-00C04FDDB82E")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IFELanguage
{
	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	int Open();
	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	int Close();
	[MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
	int GetJMorphResult(uint dwRequest, uint dwCMode, int cwchInput, [MarshalAs(UnmanagedType.LPWStr)] string pwchInput, IntPtr pfCInfo, out MorphResultHandle ppResult);
}
