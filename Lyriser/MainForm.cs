using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Lyriser
{
	public partial class MainForm : Form
	{
		public MainForm()
		{
			InitializeComponent();
			autoSetupTimer = new System.Timers.Timer();
			autoSetupTimer.SynchronizingObject = this;
			autoSetupTimer.AutoReset = true;
			autoSetupTimer.Interval = 1000.0;
			autoSetupTimer.Elapsed += AutoSetupTimer_Elapsed;
		}

		string savedFilePath;
		bool isDirty;
		System.Timers.Timer autoSetupTimer;
		(AttachedLine Line, CharacterIndex[][] Keys)[] lastSetupLines;
		(AttachedLine Line, CharacterIndex[][] Keys)[] lastParsedLines;
		int lastParsedLineIndex;

		void Save()
		{
			txtLyrics.SaveFile(savedFilePath, RichTextBoxStreamType.PlainText);
			isDirty = false;
		}

		bool ConfirmSaveChanges()
		{
			if (!isDirty)
				return true;
			using (var dialog = new TaskDialog())
			{
				dialog.Caption = Application.ProductName;
				dialog.InstructionText = string.Format(CultureInfo.CurrentCulture, Properties.Resources.ConfirmSaveChangesMessageFormat, savedFilePath != null ? Path.GetFileName(savedFilePath) : Properties.Resources.Untitled);
				dialog.OwnerWindowHandle = Handle;
				TaskDialogButton SaveButton = new TaskDialogButton(nameof(SaveButton), Properties.Resources.SaveButtonText);
				SaveButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Yes);
				TaskDialogButton DoNotSaveButton = new TaskDialogButton(nameof(DoNotSaveButton), Properties.Resources.DoNotSaveButtonText);
				DoNotSaveButton.Click += (s, ev) => dialog.Close(TaskDialogResult.No);
				TaskDialogButton CancelButton = new TaskDialogButton(nameof(CancelButton), Properties.Resources.CancelButtonText);
				CancelButton.Click += (s, ev) => dialog.Close(TaskDialogResult.Cancel);
				dialog.Controls.Add(SaveButton);
				dialog.Controls.Add(DoNotSaveButton);
				dialog.Controls.Add(CancelButton);
				dialog.Cancelable = true;
				dialog.StartupLocation = TaskDialogStartupLocation.CenterOwner;
				var result = dialog.Show();
				if (result == TaskDialogResult.Yes)
					miSave_Click(miSave, EventArgs.Empty);
				return result == TaskDialogResult.No;
			}
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			txtLyrics.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
			txtLyrics.DetectUrls = false;
			txtLyrics.HighlightTokenizer = new LyricsParser(new ListBoxBoundErrorSink(lstErrors), LyricsParser_Parsed);
			miNew_Click(this, e);
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			base.OnFormClosing(e);
			e.Cancel = !ConfirmSaveChanges();
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

		void LyricsParser_Parsed((AttachedLine Line, CharacterIndex[][] Keys)[] lines)
		{
			lastParsedLines = lines;
			lastParsedLineIndex = txtLyrics.GetLineFromCharIndex(txtLyrics.SelectionStart);
			if (lines.Length <= 0)
			{
				lvMain.Setup(lines);
				lastSetupLines = lines;
				autoSetupTimer.Stop();
			}
			else if (!autoSetupTimer.Enabled)
			{
				lvMain.Setup(lines);
				lvMain.ScrollIntoPhysicalLineHead(Math.Min(lastParsedLineIndex, lastParsedLines.Length - 1));
				lastSetupLines = lines;
				autoSetupTimer.Start();
			}
		}

		void AutoSetupTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (lastParsedLines != lastSetupLines)
			{
				lvMain.Setup(lastParsedLines);
				lvMain.ScrollIntoPhysicalLineHead(Math.Min(lastParsedLineIndex, lastParsedLines.Length - 1));
				lastSetupLines = lastParsedLines;
			}
		}

		void miNew_Click(object sender, EventArgs e)
		{
			if (!ConfirmSaveChanges()) return;
			savedFilePath = null;
			txtLyrics.Clear();
			isDirty = false;
			Text = string.Format(CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, Properties.Resources.Untitled);
		}

		void miOpen_Click(object sender, EventArgs e)
		{
			if (!ConfirmSaveChanges()) return;
			using (var dialog = new CommonOpenFileDialog())
			{
				dialog.Filters.Add(new CommonFileDialogFilter(Properties.Resources.LyricsFileFilterName, Properties.Resources.LyricsFileFilterExtensionList));
				dialog.DefaultExtension = Properties.Resources.LyricsFileDefaultExtension;
				if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				{
					txtLyrics.LoadFile(dialog.FileName, RichTextBoxStreamType.PlainText);
					isDirty = false;
					savedFilePath = dialog.FileName;
					Text = string.Format(CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, Path.GetFileName(savedFilePath));
				}
			}
		}

		void miSave_Click(object sender, EventArgs e)
		{
			if (string.IsNullOrEmpty(savedFilePath))
				miSaveAs_Click(sender, e);
			else
				Save();
		}

		void miSaveAs_Click(object sender, EventArgs e)
		{
			using (var dialog = new CommonSaveFileDialog())
			{
				dialog.Filters.Add(new CommonFileDialogFilter(Properties.Resources.LyricsFileFilterName, Properties.Resources.LyricsFileFilterExtensionList));
				dialog.DefaultExtension = Properties.Resources.LyricsFileDefaultExtension;
				dialog.AlwaysAppendDefaultExtension = true;
				if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				{
					savedFilePath = dialog.FileName;
					Save();
					Text = string.Format(CultureInfo.CurrentCulture, Properties.Resources.TitleFormat, Path.GetFileName(savedFilePath));
				}
			}
		}

		void miExit_Click(object sender, EventArgs e) => Close();

		void miCut_Click(object sender, EventArgs e) => txtLyrics.Cut();

		void miCopy_Click(object sender, EventArgs e) => txtLyrics.Copy();

		void miPaste_Click(object sender, EventArgs e) => txtLyrics.Paste();

		void miSelectAll_Click(object sender, EventArgs e) => txtLyrics.SelectAll();

		void miHighlightNext_Click(object sender, EventArgs e) => lvMain.HighlightNext();

		void miHighlightPrevious_Click(object sender, EventArgs e) => lvMain.HighlightPrevious();

		void miHighlightNextLine_Click(object sender, EventArgs e) => lvMain.HighlightNextLine(true);

		void miHighlightPreviousLine_Click(object sender, EventArgs e) => lvMain.HighlightNextLine(false);

		void miHighlightFirst_Click(object sender, EventArgs e) => lvMain.HighlightFirst();

		void txtLyrics_TextChanged(object sender, EventArgs e) => isDirty = true;

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

		class ListBoxBoundErrorSink : IErrorSink
		{
			public ListBoxBoundErrorSink(ListBox listBox) => _listBox = listBox;

			ListBox _listBox;

			public void ReportError(string description, int index) => _listBox.Items.Add(new ErrorInfo(description, index));

			public void Clear() => _listBox.Items.Clear();
		}
	}
}
