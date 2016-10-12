using System;
using System.Drawing;
using System.Windows.Forms;

namespace Lyriser
{
	public partial class MainForm : Form
	{
		public MainForm() { InitializeComponent(); }

		Lyrics lyrics = new Lyrics();
		string savedFilePath;
		
		void OptimizeScrollBarMaximum()
		{
			scrLineScroll.Enabled = lyrics.VerticalScrollMaximum > 0;
			if (scrLineScroll.Enabled)
				scrLineScroll.Maximum = lyrics.VerticalScrollMaximum + scrLineScroll.LargeChange - 1;
		}

		private void MainForm_Load(object sender, EventArgs e)
		{
			picViewer.MouseWheel += picViewer_MouseWheel;
			txtLyrics.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
			txtLyrics.HighlightTokenizer = new LyricsParser(ErrorSink.Null);
			lyrics.Bounds = picViewer.Bounds;
			lyrics.MainFont = new Font(Font.FontFamily, 14);
			lyrics.PhoneticOffset = -5;
			miNew_Click(sender, e);
		}

		private void picViewer_MouseWheel(object sender, MouseEventArgs e)
		{
			var newValue = scrLineScroll.Value - e.Delta / SystemInformation.MouseWheelScrollDelta;
			if (newValue < 0)
				newValue = 0;
			if (newValue > scrLineScroll.Maximum + 1 - scrLineScroll.LargeChange)
				newValue = scrLineScroll.Maximum + 1 - scrLineScroll.LargeChange;
			scrLineScroll.Value = newValue;
		}

		private void miRenew_Click(object sender, EventArgs e)
		{
			lyrics.Lines.Clear();
			foreach (var line in new LyricsParser(new ListBoxBoundErrorSink(lstErrors)).Transform(txtLyrics.Text))
				lyrics.Lines.Add(line);
			lyrics.ResetHighlightPosition();
			OptimizeScrollBarMaximum();
			scrLineScroll.Value = 0;
			picViewer.Invalidate();
		}

		private void picViewer_MouseDown(object sender, MouseEventArgs e)
		{
			var res = lyrics.HitTestSyllable(e.Location);
			lyrics.Highlight(res.LineIndex, _ => res.SyllableIndex);
			picViewer.Invalidate();
		}

		private void picViewer_Paint(object sender, PaintEventArgs e) { lyrics.Draw(e.Graphics); }

		private void picViewer_Resize(object sender, EventArgs e)
		{
			lyrics.Bounds = picViewer.Bounds;
			OptimizeScrollBarMaximum();
		}

		private void picViewer_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Left)
				miHighlightPrevious_Click(sender, e);
			else if (e.KeyCode == Keys.Right)
				miHighlightNext_Click(sender, e);
			else if (e.KeyCode == Keys.Up)
				miHighlightPreviousLine_Click(sender, e);
			else if (e.KeyCode == Keys.Down)
				miHighlightNextLine_Click(sender, e);
		}

		private void scrLineScroll_ValueChanged(object sender, EventArgs e)
		{
			lyrics.ViewStartLineIndex = scrLineScroll.Value;
			picViewer.Invalidate();
		}

		private void miNew_Click(object sender, EventArgs e)
		{
			savedFilePath = null;
			txtLyrics.Clear();
			txtLyrics.ClearUndo();
			miRenew_Click(sender, e);
			Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, Properties.Resources.Untitled);
		}

		private void miOpen_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Filter = Properties.Resources.LyricsFileFilter;
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					txtLyrics.Clear();
					txtLyrics.ClearUndo();
					txtLyrics.LoadFile(dialog.FileName, RichTextBoxStreamType.PlainText);
					savedFilePath = dialog.FileName;
					miRenew_Click(sender, e);
					Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, System.IO.Path.GetFileName(savedFilePath));
				}
			}
		}

		private void miSave_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(savedFilePath))
				miSaveAs_Click(sender, e);
			else
				txtLyrics.SaveFile(savedFilePath, RichTextBoxStreamType.PlainText);
		}

		private void miSaveAs_Click(object sender, EventArgs e)
		{
			using (SaveFileDialog dialog = new SaveFileDialog())
			{
				dialog.Filter = Properties.Resources.LyricsFileFilter;
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					txtLyrics.SaveFile(dialog.FileName, RichTextBoxStreamType.PlainText);
					savedFilePath = dialog.FileName;
					Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, System.IO.Path.GetFileName(savedFilePath));
				}
			}
		}

		private void miExit_Click(object sender, EventArgs e) { Close(); }

		private void miUndo_Click(object sender, EventArgs e) { txtLyrics.Undo(); }

		private void miRedo_Click(object sender, EventArgs e) { txtLyrics.Redo(); }

		private void miCut_Click(object sender, EventArgs e) { txtLyrics.Cut(); }

		private void miCopy_Click(object sender, EventArgs e) { txtLyrics.Copy(); }

		private void miPaste_Click(object sender, EventArgs e) { txtLyrics.Paste(); }

		private void miSelectAll_Click(object sender, EventArgs e) { txtLyrics.SelectAll(); }

		private void miHighlightNext_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNext(true);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		private void miHighlightPrevious_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNext(false);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		private void miHighlightNextLine_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNextLine(true);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		private void miHighlightPreviousLine_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNextLine(false);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		private void miHighlightFirst_Click(object sender, EventArgs e)
		{
			lyrics.ResetHighlightPosition();
			scrLineScroll.Value = 0;
			picViewer.Invalidate();
		}

		private void lstErrors_DoubleClick(object sender, EventArgs e)
		{
			var info = lstErrors.SelectedItem as ErrorInfo;
			if (info == null)
				return;
			txtLyrics.Select();
			txtLyrics.Select(info.Index, 0);
		}

		class ErrorInfo
		{
			public ErrorInfo(string description, int index)
			{
				Description = description;
				Index = index;
			}

			public string Description { get; }

			public int Index { get; }

			public override string ToString() => Description;
		}

		class ListBoxBoundErrorSink : ErrorSink
		{
			public ListBoxBoundErrorSink(ListBox listBox) { _listBox = listBox; }

			ListBox _listBox;

			public override void ReportError(string description, int index) => _listBox.Items.Add(new ErrorInfo(description, index));

			public override void Clear() => _listBox.Items.Clear();
		}
	}
}
