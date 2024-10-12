using System;
using ICSharpCode.AvalonEdit.Document;
using Lyriser.Core.Ime;
using Lyriser.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lyriser.Tests;

[TestClass]
public class ModelTest
{
	class MonoRubyPorviderMock(Func<string, MonoRuby> implementation) : IMonoRubyProvider
	{
		readonly Func<string, MonoRuby> m_Implementation = implementation;

		public MonoRuby GetMonoRuby(string text) => m_Implementation(text);
	}

	[TestMethod]
	public void TestAutoSetRuby_GetJMorphResultがモノルビ分割する()
	{
		var model = new Model(new MonoRubyPorviderMock(_ => new MonoRuby("はなやか", [0, 2, 3, 4])));
		model.SourceDocument.Text = "華やか";
		model.AutoSetRuby(new TextSegment() { StartOffset = 0, Length = model.SourceDocument.TextLength });
		Assert.AreEqual("華(はな)やか", model.SourceDocument.Text);
	}

	[TestMethod]
	public void TestAutoSetRuby_GetJMorphResultがモノルビ分割しない_後ろに別単語なし()
	{
		var model = new Model(new MonoRubyPorviderMock(_ => new MonoRuby("うららか", [0, MonoRuby.UnmatchedPosition, MonoRuby.UnmatchedPosition, 4])));
		model.SourceDocument.Text = "麗らか";
		model.AutoSetRuby(new TextSegment() { StartOffset = 0, Length = model.SourceDocument.TextLength });
		Assert.AreEqual("麗(うら)らか", model.SourceDocument.Text);
	}

	[TestMethod]
	public void TestAutoSetRuby_GetJMorphResultがモノルビ分割しない_後ろに別単語あり()
	{
		var model = new Model(new MonoRubyPorviderMock(_ => new MonoRuby("うららかにさいてる", [0, MonoRuby.UnmatchedPosition, MonoRuby.UnmatchedPosition, 4, 5, 6, 7, 8, 9])));
		model.SourceDocument.Text = "麗らかに咲いてる";
		model.AutoSetRuby(new TextSegment() { StartOffset = 0, Length = model.SourceDocument.TextLength });
		Assert.AreEqual("麗(うら)らかに咲(さ)いてる", model.SourceDocument.Text);
	}

	[TestMethod]
	public void TestAutoSetRuby_UnicodeカテゴリLo以外のベース文字列()
	{
		var model = new Model(new MonoRubyPorviderMock(_ => new MonoRuby("おだやかなひびをすごす", [0, 2, 3, 4, 5, MonoRuby.UnmatchedPosition, 7, 8, 9, 10, 11])));
		model.SourceDocument.Text = "穏やかな日々を過ごす";
		model.AutoSetRuby(new TextSegment() { StartOffset = 0, Length = model.SourceDocument.TextLength });
		Assert.AreEqual("穏(おだ)やかな|日々(ひび)を過(す)ごす", model.SourceDocument.Text);
	}
}
