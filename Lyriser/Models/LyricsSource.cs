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
		ArgumentNullException.ThrowIfNull(lyrics);
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
		AttachedSpecifiers = [.. attachedSpecs];
		LineMap = new LineMap(physicalLines, logicalToPhysicalMap);
		SyllableLines = [.. syllableLines];
	}
	LyricsSource()
	{
		Text = string.Empty;
		AttachedSpecifiers = [];
		LineMap = new LineMap();
		SyllableLines = [];
	}

	public string Text { get; }
	public IReadOnlyList<AttachedSpecifier> AttachedSpecifiers { get; }
	public LineMap LineMap { get; }
	public IReadOnlyList<SubSyllable[][]> SyllableLines { get; }
}

public abstract class AttachedSpecifier(Core.DirectWrite.TextRange range)
{
	public Core.DirectWrite.TextRange Range { get; } = range;

	public abstract AttachedSpecifier Move(int distance);
}

public class RubySpecifier(Core.DirectWrite.TextRange range, string text) : AttachedSpecifier(range)
{
	public string Text { get; } = text;

	public override AttachedSpecifier Move(int distance) => new RubySpecifier(Core.DirectWrite.TextRange.FromStartLength(Range.StartPosition + distance, Range.Length), Text);
}

public class SyllableDivisionSpecifier(Core.DirectWrite.TextRange range, int divisionCount) : AttachedSpecifier(range)
{
	public int DivisionCount { get; } = divisionCount;

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
		_physicalLines = [];
		_logicalToPhysicalMap = [];
	}
	public LineMap(IEnumerable<PhysicalLine> physicalLines, IEnumerable<int> logicalToPhysicalMap)
	{
		_physicalLines = physicalLines.ToArray();
		_logicalToPhysicalMap = logicalToPhysicalMap.ToArray();
	}

	readonly PhysicalLine[] _physicalLines;
	readonly int[] _logicalToPhysicalMap;

	public int LogicalLineCount => _logicalToPhysicalMap.Length;
	public int PhysicalLineCount => _physicalLines.Length;

	public PhysicalLine GetPhysicalLineByLogical(int logicalLineIndex) => _physicalLines[_logicalToPhysicalMap[logicalLineIndex]];
	public PhysicalLine GetPhysicalLineByPhysical(int physicalLineIndex) => _physicalLines[physicalLineIndex];
	public int GetPhysicalLineIndexByLogical(int logicalLineIndex) => _logicalToPhysicalMap[logicalLineIndex];
	public int GetLogicalLineIndexByPhysical(int physicalLineIndex) => Array.BinarySearch(_logicalToPhysicalMap, physicalLineIndex);
}
