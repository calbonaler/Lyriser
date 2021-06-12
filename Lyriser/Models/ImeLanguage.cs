using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Lyriser.Models
{
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
			// nullability: MSIME.Japan ProgID must be installed in Windows Japanese platform, and other platforms are not supported.
			Debug.Assert(s_LanguageType != null, "MSIME.Japan ProgID is null");
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
						var native = Marshal.PtrToStructure<MORRSLT>(result.DangerousGetHandle());
						var output = Marshal.PtrToStringUni(native.Output, native.OutputLength);
						var length = text.Length + 1;
						var array = new short[length];
						Marshal.Copy(native.MonoRubyPos, array, 0, length);
						return new MonoRuby(output, array);
					}
				}
				finally { language.Close(); }
			}
			finally { Marshal.ReleaseComObject(language); }
		}
	}

	public struct MonoRuby : IEquatable<MonoRuby>
	{
		public MonoRuby(string text, Array indexes)
		{
			Text = text ?? throw new ArgumentNullException(nameof(text));
			if (indexes == null)
				throw new ArgumentNullException(nameof(indexes));
			if (indexes.GetType() != typeof(short[]) && indexes.GetType() != typeof(ushort[]))
				throw new ArgumentException("Must be of array of Int16 or UInt16", nameof(indexes));
			Indexes = new ushort[indexes.Length];
			Buffer.BlockCopy(indexes, 0, Indexes, 0, sizeof(ushort) * indexes.Length);
		}

		public const ushort UnmatchedPosition = 0xffff;

		public string Text;
		public ushort[] Indexes;

		public bool Equals(MonoRuby other) => Text == other.Text && Indexes == other.Indexes;
		public override bool Equals(object? obj) => obj is MonoRuby other && Equals(other);
		public override int GetHashCode() => HashCode.Combine(Text, Indexes);
		public static bool operator ==(MonoRuby left, MonoRuby right) => left.Equals(right);
		public static bool operator !=(MonoRuby left, MonoRuby right) => !(left == right);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct MORRSLT
	{
		public uint   Size;             //                              total size of this block.
		public IntPtr Output;           // [WCHAR[OutputLength       ]] conversion result string.
		public ushort OutputLength;
		public IntPtr ReadOrComp;       // [WCHAR[ReadOrCompLength   ]] reading/composition string.
		public ushort ReadOrCompLength;
		public IntPtr InputPos;         // [WORD [InputLength + 1    ]] index array of reading to input character.
		public IntPtr OutputIdxWDD;     // [WORD [OutputLength       ]] index array of output character to WDD
		public IntPtr ReadOrCompIdxWDD; // [WORD [ReadOrCompLength   ]] index array of reading/composition character to WDD;
		public IntPtr MonoRubyPos;      // [WORD [InputLength + 1    ]] array of position of monoruby
		public IntPtr WordDescriptors;  // [WDD  [WordDescriptorCount]] pointer to array of WDD
		public int    WordDescriptorCount;
		public IntPtr Private;          //                              pointer of private data area
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
}
