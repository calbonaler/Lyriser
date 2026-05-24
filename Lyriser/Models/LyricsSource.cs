using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Lyriser.Models;

public class LyricsSource
{
	public static readonly LyricsSource Empty = new();

	public LyricsSource(IEnumerable<LyricsNode[]> lyrics)
	{
		ArgumentNullException.ThrowIfNull(lyrics);
		var physicalLines = new List<PhysicalLine>();
		var syllableLines = new List<SyllableLine>();
		foreach (var nodes in lyrics)
		{
			var baseTextBuilder = new StringBuilder();
			var attachedSpecs = new List<AttachedSpecifier>();
			var syllableStore = new SyllableStore();
			foreach (var node in nodes)
				node.Transform(baseTextBuilder, attachedSpecs, syllableStore);
			physicalLines.Add(new(baseTextBuilder.ToString(), [.. attachedSpecs]));
			if (syllableStore.HasAnySyllable)
				syllableLines.Add(new(syllableStore.ToArray(), physicalLines.Count - 1));
		}
		PhysicalLines = [.. physicalLines];
		SyllableLines = ISyllableLineCollection.FromSorted<SyllableLineCollection>([.. syllableLines]);
	}
	LyricsSource()
	{
		PhysicalLines = [];
		SyllableLines = ISyllableLineCollection.FromSorted<SyllableLineCollection>([]);
	}

	public IReadOnlyList<PhysicalLine> PhysicalLines { get; }
	public SyllableLineCollection SyllableLines { get; }
}

public abstract class AttachedSpecifier(Core.DirectWrite.TextRange range)
{
	public Core.DirectWrite.TextRange Range { get; } = range;
}

public class RubySpecifier(Core.DirectWrite.TextRange range, string text) : AttachedSpecifier(range)
{
	public string Text { get; } = text;
}

public class SyllableDivisionSpecifier(Core.DirectWrite.TextRange range, int divisionCount) : AttachedSpecifier(range)
{
	public int DivisionCount { get; } = divisionCount;
}

public readonly record struct SyllableLine(IReadOnlyList<SubSyllable[]> Syllables, int PhysicalLineIndex);

file interface ISyllableLineCollection
{
	static abstract SyllableLineCollection FromSorted(SyllableLine[] syllableLines);

	static SyllableLineCollection FromSorted<T>(SyllableLine[] syllableLines) where T : ISyllableLineCollection => T.FromSorted(syllableLines);
}

public class SyllableLineCollection : ReadOnlyCollection<SyllableLine>, ISyllableLineCollection
{
	SyllableLineCollection(SyllableLine[] syllableLines) : base(syllableLines) { }

	static SyllableLineCollection ISyllableLineCollection.FromSorted(SyllableLine[] syllableLines) => new(syllableLines);

	public int FindIndexByPhysicalLineIndex(int physicalLineIndex) => Array.BinarySearch((SyllableLine[])Items, new([], physicalLineIndex), Comparer<SyllableLine>.Create((x, y) => x.PhysicalLineIndex.CompareTo(y.PhysicalLineIndex)));
}

public readonly record struct SubSyllable(int AttachedIndex, int CharacterIndex)
{
	public bool IsSimple => AttachedIndex < 0;
}

public readonly record struct SyllableLocation(int Line, int Column);

public readonly record struct PhysicalLine(string Text, IReadOnlyList<AttachedSpecifier> AttachedSpecifiers)
{
	public static readonly PhysicalLine Empty = new(string.Empty, []);
}
