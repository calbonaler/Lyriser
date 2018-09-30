using System;
using System.Drawing;
using System.Windows.Forms;

namespace Lyriser
{
	public partial class MainForm : Form
	{
		public MainForm() => InitializeComponent();

		Lyrics lyrics = new Lyrics();
		string savedFilePath;
		
		void OptimizeScrollBarMaximum()
		{
			scrLineScroll.Enabled = lyrics.VerticalScrollMaximum > 0;
			scrLineScroll.Maximum = lyrics.VerticalScrollMaximum + scrLineScroll.LargeChange - 1;
			if (scrLineScroll.Value > scrLineScroll.Maximum + 1 - scrLineScroll.LargeChange)
				scrLineScroll.Value = scrLineScroll.Maximum + 1 - scrLineScroll.LargeChange;
		}

		void MainForm_Load(object sender, EventArgs e)
		{
			picViewer.MouseWheel += picViewer_MouseWheel;
			txtLyrics.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
			txtLyrics.HighlightTokenizer = new LyricsParser(ErrorSink.Null);
			lyrics.Bounds = picViewer.Bounds;
			lyrics.MainFont = new Font(Font.FontFamily, 14);
			lyrics.PhoneticOffset = -5;
			miNew_Click(sender, e);
		}

		void picViewer_MouseWheel(object sender, MouseEventArgs e)
		{
			var newValue = scrLineScroll.Value - e.Delta / SystemInformation.MouseWheelScrollDelta;
			if (newValue < 0)
				newValue = 0;
			if (newValue > scrLineScroll.Maximum + 1 - scrLineScroll.LargeChange)
				newValue = scrLineScroll.Maximum + 1 - scrLineScroll.LargeChange;
			scrLineScroll.Value = newValue;
		}

		void miRenew_Click(object sender, EventArgs e)
		{
			lyrics.Lines.Clear();
			foreach (var line in new LyricsParser(new ListBoxBoundErrorSink(lstErrors)).Transform(txtLyrics.Text))
				lyrics.Lines.Add(line);
			lyrics.ResetHighlightPosition();
			OptimizeScrollBarMaximum();
			scrLineScroll.Value = 0;
			picViewer.Invalidate();
		}

		void picViewer_MouseDown(object sender, MouseEventArgs e)
		{
			var (lineIndex, syllableIndex) = lyrics.HitTestSyllable(e.Location);
			lyrics.Highlight(lineIndex, _ => syllableIndex);
			picViewer.Invalidate();
		}

		void picViewer_Paint(object sender, PaintEventArgs e) => lyrics.Draw(e.Graphics);

		void picViewer_Resize(object sender, EventArgs e)
		{
			lyrics.Bounds = picViewer.Bounds;
			OptimizeScrollBarMaximum();
		}

		void picViewer_KeyDown(object sender, KeyEventArgs e)
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

		void scrLineScroll_ValueChanged(object sender, EventArgs e)
		{
			lyrics.ViewStartLineIndex = scrLineScroll.Value;
			picViewer.Invalidate();
		}

		void miNew_Click(object sender, EventArgs e)
		{
			savedFilePath = null;
			txtLyrics.Clear();
			txtLyrics.ClearUndo();
			miRenew_Click(sender, e);
			Text = string.Format(System.Globalization.CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, Properties.Resources.Untitled);
		}

		void miOpen_Click(object sender, EventArgs e)
		{
			using (var dialog = new OpenFileDialog())
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

		void miSave_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(savedFilePath))
				miSaveAs_Click(sender, e);
			else
				txtLyrics.SaveFile(savedFilePath, RichTextBoxStreamType.PlainText);
		}

		void miSaveAs_Click(object sender, EventArgs e)
		{
			using (var dialog = new SaveFileDialog())
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

		void miExit_Click(object sender, EventArgs e) => Close();

		void miUndo_Click(object sender, EventArgs e) => txtLyrics.Undo();

		void miRedo_Click(object sender, EventArgs e) => txtLyrics.Redo();

		void miCut_Click(object sender, EventArgs e) => txtLyrics.Cut();

		void miCopy_Click(object sender, EventArgs e) => txtLyrics.Copy();

		void miPaste_Click(object sender, EventArgs e) => txtLyrics.Paste();

		void miSelectAll_Click(object sender, EventArgs e) => txtLyrics.SelectAll();

		void miHighlightNext_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNext(true);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		void miHighlightPrevious_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNext(false);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		void miHighlightNextLine_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNextLine(true);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		void miHighlightPreviousLine_Click(object sender, EventArgs e)
		{
			lyrics.HighlightNextLine(false);
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		void miHighlightFirst_Click(object sender, EventArgs e)
		{
			lyrics.ResetHighlightPosition();
			scrLineScroll.Value = 0;
			picViewer.Invalidate();
		}

		void lstErrors_DoubleClick(object sender, EventArgs e)
		{
			if (!(lstErrors.SelectedItem is ErrorInfo info))
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
			public ListBoxBoundErrorSink(ListBox listBox) => _listBox = listBox;

			ListBox _listBox;

			public override void ReportError(string description, int index) => _listBox.Items.Add(new ErrorInfo(description, index));

			public override void Clear() => _listBox.Items.Clear();
		}
	}
}
