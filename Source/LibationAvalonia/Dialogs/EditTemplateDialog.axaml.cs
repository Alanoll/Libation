﻿using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Dinah.Core;
using LibationFileManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using Avalonia.Controls.Documents;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Templates;

namespace LibationAvalonia.Dialogs
{
	public partial class EditTemplateDialog : DialogWindow
	{
		private EditTemplateViewModel _viewModel;

		public EditTemplateDialog()
		{
			AvaloniaXamlLoader.Load(this);
			userEditTbox = this.FindControl<TextBox>(nameof(userEditTbox));
			if (Design.IsDesignMode)
			{
				_ = Configuration.Instance.LibationFiles;
				var editor = TemplateEditor<Templates.FileTemplate>.CreateFilenameEditor(Configuration.Instance.Books, Configuration.Instance.FileTemplate);
				_viewModel = new(Configuration.Instance, editor);
				_viewModel.resetTextBox(editor.EditingTemplate.TemplateText);
				Title = $"Edit {editor.EditingTemplate.Name}";
				DataContext = _viewModel;
			}
		}

		public EditTemplateDialog(ITemplateEditor templateEditor) : this()
		{
			ArgumentValidator.EnsureNotNull(templateEditor, nameof(templateEditor));

			_viewModel = new EditTemplateViewModel(Configuration.Instance, templateEditor);
			_viewModel.resetTextBox(templateEditor.EditingTemplate.TemplateText);
			Title = $"Edit {templateEditor.EditingTemplate.Name}";
			DataContext = _viewModel;
		}


		public void EditTemplateViewModel_DoubleTapped(object sender, Avalonia.Input.TappedEventArgs e)
		{
			var dataGrid = sender as DataGrid;

			var item = (dataGrid.SelectedItem as Tuple<string, string, string>).Item3;
			if (string.IsNullOrWhiteSpace(item)) return;

			var text = userEditTbox.Text;

			userEditTbox.Text = text.Insert(Math.Min(Math.Max(0, userEditTbox.CaretIndex), text.Length), item);
			userEditTbox.CaretIndex += item.Length;
		}

		protected override async Task SaveAndCloseAsync()
		{
			if (!await _viewModel.Validate())
				return;

			await base.SaveAndCloseAsync();
		}

		public async void SaveButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
			=> await SaveAndCloseAsync();

		public void ResetButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
			=> _viewModel.resetTextBox(_viewModel.TemplateEditor.DefaultTemplate);

		private class EditTemplateViewModel : ViewModels.ViewModelBase
		{
			private readonly Configuration config;
			public FontFamily FontFamily { get; } = FontManager.Current.DefaultFontFamilyName;
			public InlineCollection Inlines { get; } = new();
			public ITemplateEditor TemplateEditor { get; }
			public EditTemplateViewModel(Configuration configuration, ITemplateEditor templates)
			{
				config = configuration;
				TemplateEditor = templates;
				Description = templates.EditingTemplate.Description;
				ListItems
				= new AvaloniaList<Tuple<string, string, string>>(
					TemplateEditor
					.EditingTemplate
					.TagsRegistered
					.Cast<TemplateTags>()
					.Select(
						t => new Tuple<string, string, string>(
							$"<{t.TagName.Replace("->", "-\x200C>").Replace("<-", "<\x200C-")}>",
							t.Description,
							t.DefaultValue)
						)
					);

			}

			// hold the work-in-progress value. not guaranteed to be valid
			private string _userTemplateText;
			public string UserTemplateText
			{
				get => _userTemplateText;
				set
				{
					this.RaiseAndSetIfChanged(ref _userTemplateText, value);
					templateTb_TextChanged();
				}
			}

			private string _warningText;
			public string WarningText { get => _warningText; set => this.RaiseAndSetIfChanged(ref _warningText, value); }

			public string Description { get; }

			public AvaloniaList<Tuple<string, string, string>> ListItems { get; set; }

			public void resetTextBox(string value) => UserTemplateText = value;

			public async Task<bool> Validate()
			{
				if (TemplateEditor.EditingTemplate.IsValid)
					return true;

				var errors
					= TemplateEditor
						.EditingTemplate
						.Errors
						.Select(err => $"- {err}")
						.Aggregate((a, b) => $"{a}\r\n{b}");
				await MessageBox.Show($"This template text is not valid. Errors:\r\n{errors}", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}

			private void templateTb_TextChanged()
			{
				TemplateEditor.SetTemplateText(UserTemplateText);

				const char ZERO_WIDTH_SPACE = '\u200B';
				var sing = $"{Path.DirectorySeparatorChar}";

				// result: can wrap long paths. eg:
				// |-- LINE WRAP BOUNDARIES --|
				// \books\author with a very     <= normal line break on space between words
				// long name\narrator narrator   
				// \title                        <= line break on the zero-with space we added before slashes
				string slashWrap(string val) => val.Replace(sing, $"{ZERO_WIDTH_SPACE}{sing}");

				WarningText
					= !TemplateEditor.EditingTemplate.HasWarnings
					? ""
					: "Warning:\r\n" +
						TemplateEditor
						.EditingTemplate
						.Warnings
						.Select(err => $"- {err}")
						.Aggregate((a, b) => $"{a}\r\n{b}");

				var bold = FontWeight.Bold;
				var reg = FontWeight.Normal;

				Inlines.Clear();

				if (!TemplateEditor.IsFilePath)
				{
					Inlines.Add(new Run(TemplateEditor.GetName()) { FontWeight = bold });
					return;
				}

				var folder = TemplateEditor.GetFolderName();
				var file = TemplateEditor.GetFileName();
				var ext = config.DecryptToLossy ? "mp3" : "m4b";

				Inlines.Add(new Run(slashWrap(TemplateEditor.BaseDirectory.PathWithoutPrefix)) { FontWeight = reg });
				Inlines.Add(new Run(sing) { FontWeight = reg });

				Inlines.Add(new Run(slashWrap(folder)) { FontWeight = TemplateEditor.IsFolder ? bold : reg });

				Inlines.Add(new Run(sing));

				Inlines.Add(new Run(slashWrap(file)) { FontWeight = TemplateEditor.IsFolder ? reg : bold });

				Inlines.Add(new Run($".{ext}"));
			}
		}
	}
}
