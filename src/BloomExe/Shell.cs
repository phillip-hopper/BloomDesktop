﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Bloom.Workspace;

namespace Bloom
{
	public partial class Shell : Form
	{
		private readonly LibrarySettings _librarySettings;
		private readonly WorkspaceView _workspaceView;

		public Shell(WorkspaceView.Factory projectViewFactory, LibrarySettings librarySettings)
		{
			_librarySettings = librarySettings;
			InitializeComponent();

			_workspaceView = projectViewFactory();
			_workspaceView.CloseCurrentProject += ((x, y) =>
													{
														UserWantsToOpenADifferentProject = true;
														Close();
													});

			_workspaceView.BackColor =
				System.Drawing.Color.FromArgb(((int)(((byte)(64)))),
											  ((int)(((byte)(64)))),
											  ((int)(((byte)(64)))));
										_workspaceView.Dock = System.Windows.Forms.DockStyle.Fill;

			this.Controls.Add(this._workspaceView);

			SetWindowText();
		}

		private void SetWindowText()
		{
			Text = string.Format("{0} - Bloom {1}", _workspaceView.Text, GetVersionInfo());
			if(_librarySettings.IsShellLibrary)
			{
				Text += " SHELL MAKING PROJECT";
			}
		}

		public static string GetVersionInfo()
		{
			var asm = Assembly.GetExecutingAssembly();
			var ver = asm.GetName().Version;
			var file = asm.CodeBase.Replace("file:", string.Empty);
			file = file.TrimStart('/');
			var fi = new FileInfo(file);

			return string.Format("Version {0}.{1}.{2} Built on {3}", ver.Major, ver.Minor,
				ver.Build, fi.CreationTime.ToString("dd-MMM-yyyy"));
		}

		public bool UserWantsToOpenADifferentProject { get; set; }

	}
}
