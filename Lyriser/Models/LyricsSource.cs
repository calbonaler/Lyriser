using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lyriser.Models;

public class LyricsSource
{
	public static readonly LyricsSource Empty = new();

	public LyricsSource(IEnumerable<LyricsNode[]> lyrics)
	{
		if (lyrics == null)
			throw new ArgumentNullException(nameof(lyrics));
		var attachedSpecs = new List<AttachedSpecifier>();
		var baseTextBuilder = new StringBuilder();
		var physicalLines = new List<PhysicalLine>();
		var logicalToPhysicalMap = new List<int>();
		var syllableLines = new List<SubSyllable[][]>();
		var firstLine = true;
		foreach (var nodes in lyrics)
		{
			if (!firstLine)
				baseTextBuilder.Append('\n');
			var textStart = baseTextBuilder.Length;
			var attachedStart = attachedSpecs.Count;
			var syllableStore = new SyllableStore();
			foreach (var node in nodes)
				node.Transform(baseTextBuilder, attachedSpecs, syllableStore);
			physicalLines.Add(new PhysicalLine(textStart, baseTextBuilder.Length - textStart, attachedStart, attachedSpecs.Count - attachedStart));
			if (syllableStore.HasAnySyllable)
			{
				logicalToPhysicalMap.Add(physicalLines.Count - 1);
				syllableLines.Add(syllableStore.ToArray());
			}
			firstLine = false;
		}
		Text = baseTextBuilder.ToString();
		AttachedSpecifiers = attachedSpecs.ToArray();
		LineMap = new LineMap(physicalLines, logicalToPhysicalMap);
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
	protected AttachedSpecifier(Core.DirectWrite.TextRange range) => Range = range;

	public Core.DirectWrite.TextRange Range { get; }

	public abstract AttachedSpecifier Move(int distance);
}

public class RubySpecifier : AttachedSpecifier
{
	public RubySpecifier(Core.DirectWrite.TextRange range, string text) : base(range) => Text = text;

	public string Text { get; }

	public override AttachedSpecifier Move(int distance) => new RubySpecifier(Core.DirectWrite.TextRange.FromStartLength(Range.StartPosition + distance, Range.Length), Text);
}

public class SyllableDivisionSpecifier : AttachedSpecifier
{
	public SyllableDivisionSpecifier(Core.DirectWrite.TextRange range, int divisionCount) : base(range) => DivisionCount = divisionCount;

	public int DivisionCount { get; }

	public override AttachedSpecifier Move(int distance) => new SyllableDivisionSpecifier(Core.DirectWrite.TextRange.FromStartLength(Range.StartPosition + distance, Range.Length), DivisionCount);
}

public readonly record struct SubSyllable(int AttachedIndex, int CharacterIndex)
{
	public bool IsSimple => AttachedIndex < 0;
}

public readonly record struct SyllableLocation(int Line, int Column);

public readonly record struct PhysicalLine(int TextStart, int TextLength, int AttachedStart, int AttachedLength);

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
