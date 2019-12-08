using System;
using System.Collections.Generic;
using System.Linq;
using SharpDX.DirectWrite;

namespace Lyriser.Models
{
	public class LyricsSource
	{
		public static readonly LyricsSource Empty = new LyricsSource();

		public LyricsSource(string text, IEnumerable<AttachedSpecifier> attachedSpecifiers, LineMap lineMap, IEnumerable<SubSyllable[][]> syllableLines)
		{
			Text = text ?? throw new ArgumentNullException(nameof(text));
			AttachedSpecifiers = attachedSpecifiers.ToArray();
			LineMap = lineMap ?? throw new ArgumentNullException(nameof(lineMap));
			SyllableLines = syllableLines.ToArray();
		}
		LyricsSource()
		{
			Text = string.Empty;
			AttachedSpecifiers = Array.Empty<AttachedSpecifier>();
			LineMap = new LineMap();
			SyllableLines = Array.Empty<SubSyllable[][]>();
		}

		public string Text { get; }
		public IReadOnlyList<AttachedSpecifier> AttachedSpecifiers { get; }
		public LineMap LineMap { get; }
		public IReadOnlyList<SubSyllable[][]> SyllableLines { get; }
	}

	public abstract class AttachedSpecifier
	{
		protected AttachedSpecifier(TextRange range) => Range = range;

		public TextRange Range { get; }

		public abstract AttachedSpecifier Move(int distance);
	}

	public class RubySpecifier : AttachedSpecifier
	{
		public RubySpecifier(TextRange range, string text) : base(range) => Text = text;

		public string Text { get; }

		public override AttachedSpecifier Move(int distance) => new RubySpecifier(new TextRange(Range.StartPosition + distance, Range.Length), Text);
	}

	public class SyllableDivisionSpecifier : AttachedSpecifier
	{
		public SyllableDivisionSpecifier(TextRange range, int divisionCount) : base(range) => DivisionCount = divisionCount;

		public int DivisionCount { get; }

		public override AttachedSpecifier Move(int distance) => new SyllableDivisionSpecifier(new TextRange(Range.StartPosition + distance, Range.Length), DivisionCount);
	}

	public readonly struct SubSyllable : IEquatable<SubSyllable>
	{
		public SubSyllable(int attached, int character)
		{
			AttachedIndex = attached;
			CharacterIndex = character;
		}
		public SubSyllable(int character)
		{
			AttachedIndex = -1;
			CharacterIndex = character;
		}

		public int AttachedIndex { get; }
		public int CharacterIndex { get; }
		public bool IsSimple => AttachedIndex < 0;

		public bool Equals(SubSyllable other) => AttachedIndex == other.AttachedIndex && CharacterIndex == other.CharacterIndex;
		public override bool Equals(object obj) => obj is SubSyllable other && Equals(other);
		public override int GetHashCode() => AttachedIndex ^ CharacterIndex;
		public static bool operator ==(SubSyllable left, SubSyllable right) => left.Equals(right);
		public static bool operator !=(SubSyllable left, SubSyllable right) => !(left == right);
	}

	public readonly struct SyllableLocation : IEquatable<SyllableLocation>
	{
		public SyllableLocation(int line, int column)
		{
			Line = line;
			Column = column;
		}

		public int Line { get; }
		public int Column { get; }

		public bool Equals(SyllableLocation other) => Line == other.Line && Column == other.Column;
		public override bool Equals(object obj) => obj is SyllableLocation other && Equals(other);
		public override int GetHashCode() => Line ^ Column;
		public static bool operator ==(SyllableLocation left, SyllableLocation right) => left.Equals(right);
		public static bool operator !=(SyllableLocation left, SyllableLocation right) => !(left == right);
	}

	public readonly struct PhysicalLine : IEquatable<PhysicalLine>
	{
		public PhysicalLine(int textStart, int textLength, int attachedStart, int attachedLength)
		{
			TextStart = textStart;
			TextLength = textLength;
			AttachedStart = attachedStart;
			AttachedLength = attachedLength;
		}

		public int TextStart { get; }
		public int TextLength { get; }
		public int AttachedStart { get; }
		public int AttachedLength { get; }

		public bool Equals(PhysicalLine other) => TextStart == other.TextStart && TextLength == other.TextLength && AttachedStart == other.AttachedStart && AttachedLength == other.AttachedLength;
		public override bool Equals(object obj) => obj is PhysicalLine other && Equals(other);
		public override int GetHashCode() => TextStart ^ TextLength ^ AttachedStart ^ AttachedLength;
		public static bool operator ==(PhysicalLine left, PhysicalLine right) => left.Equals(right);
		public static bool operator !=(PhysicalLine left, PhysicalLine right) => !(left == right);
	}

	public class LineMap
	{
		public LineMap()
		{
			m_PhysicalLines = Array.Empty<PhysicalLine>();
			m_LogicalToPhysicalMap = Array.Empty<int>();
		}
		public LineMap(IEnumerable<PhysicalLine> physicalLines, IEnumerable<int> logicalToPhysicalMap)
		{
			m_PhysicalLines = physicalLines.ToArray();
			m_LogicalToPhysicalMap = logicalToPhysicalMap.ToArray();
		}

		readonly PhysicalLine[] m_PhysicalLines;
		readonly int[] m_LogicalToPhysicalMap;

		public int LogicalLineCount => m_LogicalToPhysicalMap.Length;
		public int PhysicalLineCount => m_PhysicalLines.Length;

		public PhysicalLine GetPhysicalLineByLogical(int logicalLineIndex) => m_PhysicalLines[m_LogicalToPhysicalMap[logicalLineIndex]];
		public PhysicalLine GetPhysicalLineByPhysical(int physicalLineIndex) => m_PhysicalLines[physicalLineIndex];
		public int GetPhysicalLineIndexByLogical(int logicalLineIndex) => m_LogicalToPhysicalMap[logicalLineIndex];
		public int GetLogicalLineIndexByPhysical(int physicalLineIndex) => Array.BinarySearch(m_LogicalToPhysicalMap, physicalLineIndex);
	}
}
