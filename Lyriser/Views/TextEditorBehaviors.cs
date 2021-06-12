using System;
using System.Windows;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.Xaml.Behaviors;

namespace Lyriser.Views
{
	public class CaretPositionBindingBehavior : Behavior<TextEditor>
	{
		public static readonly DependencyProperty LocationProperty = DependencyProperty.Register(nameof(Location), typeof(TextLocation), typeof(CaretPositionBindingBehavior));

		public TextLocation Location
		{
			get => (TextLocation)GetValue(LocationProperty);
			set => SetValue(LocationProperty, value);
		}

		protected override void OnAttached()
		{
			base.OnAttached();
			AssociatedObject.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
		}

		protected override void OnDetaching()
		{
			AssociatedObject.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
			base.OnDetaching();
		}

		void OnCaretPositionChanged(object? sender, EventArgs e) => Location = AssociatedObject.TextArea.Caret.Location;
	}

	public class SelectionBindingBehavior : Behavior<TextEditor>
	{
		public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(nameof(Selection), typeof(Selection), typeof(SelectionBindingBehavior));

		public Selection Selection
		{
			get => (Selection)GetValue(SelectionProperty);
			set => SetValue(SelectionProperty, value);
		}

		protected override void OnAttached()
		{
			base.OnAttached();
			AssociatedObject.TextArea.SelectionChanged += OnSelectionChanged;
		}

		protected override void OnDetaching()
		{
			AssociatedObject.TextArea.SelectionChanged -= OnSelectionChanged;
			base.OnDetaching();
		}

		void OnSelectionChanged(object? sender, EventArgs e) => Selection = AssociatedObject.TextArea.Selection;
	}
}
