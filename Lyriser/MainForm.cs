using System;
using System.Drawing;
using System.Windows.Forms;

namespace Lyriser
{
	public partial class MainForm : Form
	{
		public MainForm() => InitializeComponent();

		string savedFilePath;
		
		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			txtLyrics.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
			txtLyrics.HighlightTokenizer = new LyricsParser(ErrorSink.Null);
			miNew_Click(this, e);
		}

		protected override void OnDpiChanged(DpiChangedEventArgs e)
		{
			base.OnDpiChanged(e);
			using (var oldMenuFont = msMain.Font)
				msMain.Font = new Font(oldMenuFont.FontFamily, oldMenuFont.SizeInPoints * e.DeviceDpiNew / e.DeviceDpiOld, oldMenuFont.Style, GraphicsUnit.Point);
			using (var oldToolFont = tsMain.Font)
				tsMain.Font = new Font(oldToolFont.FontFamily, oldToolFont.SizeInPoints * e.DeviceDpiNew / e.DeviceDpiOld, oldToolFont.Style, GraphicsUnit.Point);
			msMain.ImageScalingSize = new Size(msMain.ImageScalingSize.Width * e.DeviceDpiNew / e.DeviceDpiOld, msMain.ImageScalingSize.Height * e.DeviceDpiNew / e.DeviceDpiOld);
			tsMain.ImageScalingSize = new Size(tsMain.ImageScalingSize.Width * e.DeviceDpiNew / e.DeviceDpiOld, tsMain.ImageScalingSize.Height * e.DeviceDpiNew / e.DeviceDpiOld);
		}

		void miRenew_Click(object sender, EventArgs e) => lvMain.Setup(new LyricsParser(new ListBoxBoundErrorSink(lstErrors)).Transform(txtLyrics.Text));

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

		void miHighlightNext_Click(object sender, EventArgs e) => lvMain.HighlightNext();

		void miHighlightPrevious_Click(object sender, EventArgs e) => lvMain.HighlightPrevious();

		void miHighlightNextLine_Click(object sender, EventArgs e) => lvMain.HighlightNextLine(true);

		void miHighlightPreviousLine_Click(object sender, EventArgs e) => lvMain.HighlightNextLine(false);

		void miHighlightFirst_Click(object sender, EventArgs e) => lvMain.HighlightFirst();

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
