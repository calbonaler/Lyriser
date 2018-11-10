﻿namespace Lyriser
{
	partial class MainForm
	{
		/// <summary>
		/// 必要なデザイナー変数です。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// 使用中のリソースをすべてクリーンアップします。
		/// </summary>
		/// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows フォーム デザイナーで生成されたコード

		/// <summary>
		/// デザイナー サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディターで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.splMain = new System.Windows.Forms.SplitContainer();
			this.txtLyrics = new Controls.WindowsForms.SyntaxHighlightingTextBox();
			this.lstErrors = new System.Windows.Forms.ListBox();
			this.tcMain = new System.Windows.Forms.TabControl();
			this.tpView = new System.Windows.Forms.TabPage();
			this.lvMain = new Lyriser.LyricsViewer();
			this.tpEdit = new System.Windows.Forms.TabPage();
			this.msMain = new System.Windows.Forms.MenuStrip();
			this.miFile = new System.Windows.Forms.ToolStripMenuItem();
			this.miNew = new System.Windows.Forms.ToolStripMenuItem();
			this.miOpen = new System.Windows.Forms.ToolStripMenuItem();
			this.sepFile1 = new System.Windows.Forms.ToolStripSeparator();
			this.miSave = new System.Windows.Forms.ToolStripMenuItem();
			this.miSaveAs = new System.Windows.Forms.ToolStripMenuItem();
			this.sepFile2 = new System.Windows.Forms.ToolStripSeparator();
			this.miExit = new System.Windows.Forms.ToolStripMenuItem();
			this.miEdit = new System.Windows.Forms.ToolStripMenuItem();
			this.miUndo = new System.Windows.Forms.ToolStripMenuItem();
			this.miRedo = new System.Windows.Forms.ToolStripMenuItem();
			this.sepEdit1 = new System.Windows.Forms.ToolStripSeparator();
			this.miCut = new System.Windows.Forms.ToolStripMenuItem();
			this.miCopy = new System.Windows.Forms.ToolStripMenuItem();
			this.miPaste = new System.Windows.Forms.ToolStripMenuItem();
			this.sepEdit2 = new System.Windows.Forms.ToolStripSeparator();
			this.miSelectAll = new System.Windows.Forms.ToolStripMenuItem();
			this.miParse = new System.Windows.Forms.ToolStripMenuItem();
			this.miRenew = new System.Windows.Forms.ToolStripMenuItem();
			this.miOperation = new System.Windows.Forms.ToolStripMenuItem();
			this.miHighlightNext = new System.Windows.Forms.ToolStripMenuItem();
			this.miHighlightPrevious = new System.Windows.Forms.ToolStripMenuItem();
			this.miHighlightNextLine = new System.Windows.Forms.ToolStripMenuItem();
			this.miHighlightPreviousLine = new System.Windows.Forms.ToolStripMenuItem();
			this.sepOperation = new System.Windows.Forms.ToolStripSeparator();
			this.miHighlightFirst = new System.Windows.Forms.ToolStripMenuItem();
			this.tsMain = new System.Windows.Forms.ToolStrip();
			this.btnNew = new System.Windows.Forms.ToolStripButton();
			this.btnOpen = new System.Windows.Forms.ToolStripButton();
			this.btnSave = new System.Windows.Forms.ToolStripButton();
			this.sepToolStrip1 = new System.Windows.Forms.ToolStripSeparator();
			this.btnCut = new System.Windows.Forms.ToolStripButton();
			this.btnCopy = new System.Windows.Forms.ToolStripButton();
			this.btnPaste = new System.Windows.Forms.ToolStripButton();
			((System.ComponentModel.ISupportInitialize)(this.splMain)).BeginInit();
			this.splMain.Panel1.SuspendLayout();
			this.splMain.Panel2.SuspendLayout();
			this.splMain.SuspendLayout();
			this.tcMain.SuspendLayout();
			this.tpView.SuspendLayout();
			this.tpEdit.SuspendLayout();
			this.msMain.SuspendLayout();
			this.tsMain.SuspendLayout();
			this.SuspendLayout();
			// 
			// splMain
			// 
			resources.ApplyResources(this.splMain, "splMain");
			this.splMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
			this.splMain.Name = "splMain";
			// 
			// splMain.Panel1
			// 
			this.splMain.Panel1.Controls.Add(this.txtLyrics);
			// 
			// splMain.Panel2
			// 
			this.splMain.Panel2.Controls.Add(this.lstErrors);
			// 
			// txtLyrics
			// 
			this.txtLyrics.BorderStyle = System.Windows.Forms.BorderStyle.None;
			resources.ApplyResources(this.txtLyrics, "txtLyrics");
			this.txtLyrics.HighlightTokenizer = null;
			this.txtLyrics.Name = "txtLyrics";
			this.txtLyrics.TextChanged += new System.EventHandler(this.txtLyrics_TextChanged);
			// 
			// lstErrors
			// 
			resources.ApplyResources(this.lstErrors, "lstErrors");
			this.lstErrors.FormattingEnabled = true;
			this.lstErrors.Name = "lstErrors";
			this.lstErrors.DoubleClick += new System.EventHandler(this.lstErrors_DoubleClick);
			// 
			// tcMain
			// 
			this.tcMain.Controls.Add(this.tpView);
			this.tcMain.Controls.Add(this.tpEdit);
			resources.ApplyResources(this.tcMain, "tcMain");
			this.tcMain.Name = "tcMain";
			this.tcMain.SelectedIndex = 0;
			// 
			// tpView
			// 
			this.tpView.Controls.Add(this.lvMain);
			resources.ApplyResources(this.tpView, "tpView");
			this.tpView.Name = "tpView";
			this.tpView.UseVisualStyleBackColor = true;
			// 
			// lvMain
			// 
			this.lvMain.BackColor = System.Drawing.Color.White;
			resources.ApplyResources(this.lvMain, "lvMain");
			this.lvMain.Name = "lvMain";
			// 
			// tpEdit
			// 
			this.tpEdit.Controls.Add(this.splMain);
			resources.ApplyResources(this.tpEdit, "tpEdit");
			this.tpEdit.Name = "tpEdit";
			this.tpEdit.UseVisualStyleBackColor = true;
			// 
			// msMain
			// 
			this.msMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miFile,
            this.miEdit,
            this.miParse,
            this.miOperation});
			resources.ApplyResources(this.msMain, "msMain");
			this.msMain.Name = "msMain";
			// 
			// miFile
			// 
			this.miFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miNew,
            this.miOpen,
            this.sepFile1,
            this.miSave,
            this.miSaveAs,
            this.sepFile2,
            this.miExit});
			this.miFile.Name = "miFile";
			resources.ApplyResources(this.miFile, "miFile");
			// 
			// miNew
			// 
			resources.ApplyResources(this.miNew, "miNew");
			this.miNew.Name = "miNew";
			this.miNew.Click += new System.EventHandler(this.miNew_Click);
			// 
			// miOpen
			// 
			resources.ApplyResources(this.miOpen, "miOpen");
			this.miOpen.Name = "miOpen";
			this.miOpen.Click += new System.EventHandler(this.miOpen_Click);
			// 
			// sepFile1
			// 
			this.sepFile1.Name = "sepFile1";
			resources.ApplyResources(this.sepFile1, "sepFile1");
			// 
			// miSave
			// 
			resources.ApplyResources(this.miSave, "miSave");
			this.miSave.Name = "miSave";
			this.miSave.Click += new System.EventHandler(this.miSave_Click);
			// 
			// miSaveAs
			// 
			this.miSaveAs.Name = "miSaveAs";
			resources.ApplyResources(this.miSaveAs, "miSaveAs");
			this.miSaveAs.Click += new System.EventHandler(this.miSaveAs_Click);
			// 
			// sepFile2
			// 
			this.sepFile2.Name = "sepFile2";
			resources.ApplyResources(this.sepFile2, "sepFile2");
			// 
			// miExit
			// 
			this.miExit.Name = "miExit";
			resources.ApplyResources(this.miExit, "miExit");
			this.miExit.Click += new System.EventHandler(this.miExit_Click);
			// 
			// miEdit
			// 
			this.miEdit.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miUndo,
            this.miRedo,
            this.sepEdit1,
            this.miCut,
            this.miCopy,
            this.miPaste,
            this.sepEdit2,
            this.miSelectAll});
			this.miEdit.Name = "miEdit";
			resources.ApplyResources(this.miEdit, "miEdit");
			// 
			// miUndo
			// 
			this.miUndo.Name = "miUndo";
			resources.ApplyResources(this.miUndo, "miUndo");
			this.miUndo.Click += new System.EventHandler(this.miUndo_Click);
			// 
			// miRedo
			// 
			this.miRedo.Name = "miRedo";
			resources.ApplyResources(this.miRedo, "miRedo");
			this.miRedo.Click += new System.EventHandler(this.miRedo_Click);
			// 
			// sepEdit1
			// 
			this.sepEdit1.Name = "sepEdit1";
			resources.ApplyResources(this.sepEdit1, "sepEdit1");
			// 
			// miCut
			// 
			resources.ApplyResources(this.miCut, "miCut");
			this.miCut.Name = "miCut";
			this.miCut.Click += new System.EventHandler(this.miCut_Click);
			// 
			// miCopy
			// 
			resources.ApplyResources(this.miCopy, "miCopy");
			this.miCopy.Name = "miCopy";
			this.miCopy.Click += new System.EventHandler(this.miCopy_Click);
			// 
			// miPaste
			// 
			resources.ApplyResources(this.miPaste, "miPaste");
			this.miPaste.Name = "miPaste";
			this.miPaste.Click += new System.EventHandler(this.miPaste_Click);
			// 
			// sepEdit2
			// 
			this.sepEdit2.Name = "sepEdit2";
			resources.ApplyResources(this.sepEdit2, "sepEdit2");
			// 
			// miSelectAll
			// 
			this.miSelectAll.Name = "miSelectAll";
			resources.ApplyResources(this.miSelectAll, "miSelectAll");
			this.miSelectAll.Click += new System.EventHandler(this.miSelectAll_Click);
			// 
			// miParse
			// 
			this.miParse.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miRenew});
			this.miParse.Name = "miParse";
			resources.ApplyResources(this.miParse, "miParse");
			// 
			// miRenew
			// 
			this.miRenew.Name = "miRenew";
			resources.ApplyResources(this.miRenew, "miRenew");
			this.miRenew.Click += new System.EventHandler(this.miRenew_Click);
			// 
			// miOperation
			// 
			this.miOperation.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miHighlightNext,
            this.miHighlightPrevious,
            this.miHighlightNextLine,
            this.miHighlightPreviousLine,
            this.sepOperation,
            this.miHighlightFirst});
			this.miOperation.Name = "miOperation";
			resources.ApplyResources(this.miOperation, "miOperation");
			// 
			// miHighlightNext
			// 
			this.miHighlightNext.Name = "miHighlightNext";
			resources.ApplyResources(this.miHighlightNext, "miHighlightNext");
			this.miHighlightNext.Click += new System.EventHandler(this.miHighlightNext_Click);
			// 
			// miHighlightPrevious
			// 
			this.miHighlightPrevious.Name = "miHighlightPrevious";
			resources.ApplyResources(this.miHighlightPrevious, "miHighlightPrevious");
			this.miHighlightPrevious.Click += new System.EventHandler(this.miHighlightPrevious_Click);
			// 
			// miHighlightNextLine
			// 
			this.miHighlightNextLine.Name = "miHighlightNextLine";
			resources.ApplyResources(this.miHighlightNextLine, "miHighlightNextLine");
			this.miHighlightNextLine.Click += new System.EventHandler(this.miHighlightNextLine_Click);
			// 
			// miHighlightPreviousLine
			// 
			this.miHighlightPreviousLine.Name = "miHighlightPreviousLine";
			resources.ApplyResources(this.miHighlightPreviousLine, "miHighlightPreviousLine");
			this.miHighlightPreviousLine.Click += new System.EventHandler(this.miHighlightPreviousLine_Click);
			// 
			// sepOperation
			// 
			this.sepOperation.Name = "sepOperation";
			resources.ApplyResources(this.sepOperation, "sepOperation");
			// 
			// miHighlightFirst
			// 
			this.miHighlightFirst.Name = "miHighlightFirst";
			resources.ApplyResources(this.miHighlightFirst, "miHighlightFirst");
			this.miHighlightFirst.Click += new System.EventHandler(this.miHighlightFirst_Click);
			// 
			// tsMain
			// 
			resources.ApplyResources(this.tsMain, "tsMain");
			this.tsMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.btnNew,
            this.btnOpen,
            this.btnSave,
            this.sepToolStrip1,
            this.btnCut,
            this.btnCopy,
            this.btnPaste});
			this.tsMain.Name = "tsMain";
			// 
			// btnNew
			// 
			this.btnNew.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this.btnNew, "btnNew");
			this.btnNew.Name = "btnNew";
			this.btnNew.Click += new System.EventHandler(this.miNew_Click);
			// 
			// btnOpen
			// 
			this.btnOpen.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this.btnOpen, "btnOpen");
			this.btnOpen.Name = "btnOpen";
			this.btnOpen.Click += new System.EventHandler(this.miOpen_Click);
			// 
			// btnSave
			// 
			this.btnSave.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this.btnSave, "btnSave");
			this.btnSave.Name = "btnSave";
			this.btnSave.Click += new System.EventHandler(this.miSave_Click);
			// 
			// sepToolStrip1
			// 
			this.sepToolStrip1.Name = "sepToolStrip1";
			resources.ApplyResources(this.sepToolStrip1, "sepToolStrip1");
			// 
			// btnCut
			// 
			this.btnCut.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this.btnCut, "btnCut");
			this.btnCut.Name = "btnCut";
			this.btnCut.Click += new System.EventHandler(this.miCut_Click);
			// 
			// btnCopy
			// 
			this.btnCopy.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this.btnCopy, "btnCopy");
			this.btnCopy.Name = "btnCopy";
			this.btnCopy.Click += new System.EventHandler(this.miCopy_Click);
			// 
			// btnPaste
			// 
			this.btnPaste.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
			resources.ApplyResources(this.btnPaste, "btnPaste");
			this.btnPaste.Name = "btnPaste";
			this.btnPaste.Click += new System.EventHandler(this.miPaste_Click);
			// 
			// MainForm
			// 
			resources.ApplyResources(this, "$this");
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.Controls.Add(this.tcMain);
			this.Controls.Add(this.tsMain);
			this.Controls.Add(this.msMain);
			this.Name = "MainForm";
			this.splMain.Panel1.ResumeLayout(false);
			this.splMain.Panel2.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.splMain)).EndInit();
			this.splMain.ResumeLayout(false);
			this.tcMain.ResumeLayout(false);
			this.tpView.ResumeLayout(false);
			this.tpEdit.ResumeLayout(false);
			this.msMain.ResumeLayout(false);
			this.msMain.PerformLayout();
			this.tsMain.ResumeLayout(false);
			this.tsMain.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.TabControl tcMain;
		private System.Windows.Forms.TabPage tpView;
		private System.Windows.Forms.TabPage tpEdit;
		private Controls.WindowsForms.SyntaxHighlightingTextBox txtLyrics;
		private System.Windows.Forms.MenuStrip msMain;
		private System.Windows.Forms.ToolStripMenuItem miFile;
		private System.Windows.Forms.ToolStripMenuItem miNew;
		private System.Windows.Forms.ToolStripMenuItem miOpen;
		private System.Windows.Forms.ToolStripSeparator sepFile1;
		private System.Windows.Forms.ToolStripMenuItem miSave;
		private System.Windows.Forms.ToolStripMenuItem miSaveAs;
		private System.Windows.Forms.ToolStripSeparator sepFile2;
		private System.Windows.Forms.ToolStripMenuItem miExit;
		private System.Windows.Forms.ToolStripMenuItem miEdit;
		private System.Windows.Forms.ToolStripMenuItem miUndo;
		private System.Windows.Forms.ToolStripMenuItem miRedo;
		private System.Windows.Forms.ToolStripSeparator sepEdit1;
		private System.Windows.Forms.ToolStripMenuItem miCut;
		private System.Windows.Forms.ToolStripMenuItem miCopy;
		private System.Windows.Forms.ToolStripMenuItem miPaste;
		private System.Windows.Forms.ToolStripSeparator sepEdit2;
		private System.Windows.Forms.ToolStripMenuItem miSelectAll;
		private System.Windows.Forms.ToolStrip tsMain;
		private System.Windows.Forms.ToolStripButton btnNew;
		private System.Windows.Forms.ToolStripButton btnOpen;
		private System.Windows.Forms.ToolStripButton btnSave;
		private System.Windows.Forms.ToolStripSeparator sepToolStrip1;
		private System.Windows.Forms.ToolStripButton btnCut;
		private System.Windows.Forms.ToolStripButton btnCopy;
		private System.Windows.Forms.ToolStripButton btnPaste;
		private System.Windows.Forms.ToolStripMenuItem miParse;
		private System.Windows.Forms.ToolStripMenuItem miRenew;
		private System.Windows.Forms.ToolStripMenuItem miOperation;
		private System.Windows.Forms.ToolStripMenuItem miHighlightNext;
		private System.Windows.Forms.ToolStripMenuItem miHighlightPrevious;
		private System.Windows.Forms.ToolStripSeparator sepOperation;
		private System.Windows.Forms.ToolStripMenuItem miHighlightFirst;
		private System.Windows.Forms.SplitContainer splMain;
		private System.Windows.Forms.ListBox lstErrors;
		private System.Windows.Forms.ToolStripMenuItem miHighlightNextLine;
		private System.Windows.Forms.ToolStripMenuItem miHighlightPreviousLine;
		private LyricsViewer lvMain;
	}
}

