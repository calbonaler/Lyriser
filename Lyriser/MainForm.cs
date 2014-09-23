using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using Controls;

namespace Lyriser
{
	public partial class MainForm : Form
	{
		public MainForm()
		{
			InitializeComponent();
		}

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
			txtLyrics.LanguageOption = RichTextBoxLanguageOptions.UIFonts;
			var composite = new CompositeHighlightTokenizer();
			composite.HighlightDescriptors.Add(new RegexHighlightDescriptor(Color.Red, Color.Empty, @"(?<={)[^""]+?(?=(""[^""]+?"")?})|[^""](?=""[^""]+?"")"));
			composite.HighlightDescriptors.Add(new RegexHighlightDescriptor(Color.Blue, Color.Empty, @"(?<={[^""]+?)""[^""]+?""(?=})|(?<=[^""])""[^""]+?"""));
			composite.HighlightDescriptors.Add(new RegexHighlightDescriptor(Color.Silver, Color.Empty, @"\(.*?\)"));
			txtLyrics.HighlightTokenizer = composite;
			lyrics.Bounds = picViewer.Bounds;
			lyrics.MainFont = new Font(Font.FontFamily, 14);
			lyrics.PhoneticOffset = -5;
			btnRenew_Click(sender, e);
		}

		private void btnRenew_Click(object sender, EventArgs e)
		{
			lyrics.Parse(txtLyrics.Text);
			OptimizeScrollBarMaximum();
			picViewer.Invalidate();
		}

		private void picViewer_Paint(object sender, PaintEventArgs e)
		{
			lyrics.Draw(e.Graphics);
		}

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
			btnRenew_Click(sender, e);
		}

		private void miOpen_Click(object sender, EventArgs e)
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Filter = Properties.Resources.LyricsFileFilter;
				if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					miNew_Click(sender, e);
					txtLyrics.LoadFile(dialog.FileName, RichTextBoxStreamType.PlainText);
					savedFilePath = dialog.FileName;
					btnRenew_Click(sender, e);
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
				if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
				{
					txtLyrics.SaveFile(dialog.FileName, RichTextBoxStreamType.PlainText);
					savedFilePath = dialog.FileName;
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
			lyrics.HighlightNext();
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		private void miHighlightPrevious_Click(object sender, EventArgs e)
		{
			lyrics.HighlightPrevious();
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}

		private void itmHighlightFirst_Click(object sender, EventArgs e)
		{
			lyrics.ResetHighlightPosition();
			scrLineScroll.Value = lyrics.ViewStartLineIndex;
			picViewer.Invalidate();
		}
	}
}
