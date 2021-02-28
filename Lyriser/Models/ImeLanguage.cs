﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Lyriser.Models
{
	public static class ImeLanguage
	{
		const int S_OK = 0;
		const int FELANG_REQ_REV = 0x00030000;
		const int FELANG_CMODE_MONORUBY = 0x00000002;

		public const ushort UnmatchedPosition = 0xffff;

		static readonly Type s_LanguageType = Type.GetTypeFromProgID("MSIME.Japan");

		static ushort[] CopyToUInt16(IntPtr ptr, int length)
		{
			if (ptr == IntPtr.Zero)
				return null;
			var array = new short[length];
			Marshal.Copy(ptr, array, 0, length);
			var finalArray = new ushort[length];
			Buffer.BlockCopy(array, 0, finalArray, 0, sizeof(ushort) * length);
			return finalArray;
		}

		public static (string Output, ushort[] MonoRubyIndexes) GetMonoRuby(string text)
		{
			var language = (IFELanguage)Activator.CreateInstance(s_LanguageType);
			try
			{
				if (language.Open() != S_OK)
					return (null, null);
				try
				{
					var res = language.GetJMorphResult(FELANG_REQ_REV, FELANG_CMODE_MONORUBY, text.Length, text, IntPtr.Zero, out var result);
					if (res != S_OK || result.IsInvalid)
						return (null, null);
					using (result)
					{
						var native = Marshal.PtrToStructure<MORRSLT>(result.DangerousGetHandle());
						var output = Marshal.PtrToStringUni(native.Output, native.OutputLength);
						var monoRubyIndexes = CopyToUInt16(native.MonoRubyPos, text.Length + 1);
						return (output, monoRubyIndexes);
					}
				}
				finally { language.Close(); }
			}
			finally { Marshal.ReleaseComObject(language); }
		}
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
