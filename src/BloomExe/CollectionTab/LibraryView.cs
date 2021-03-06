﻿using System;
using System.Windows.Forms;
using Bloom.Properties;
using Bloom.SendReceive;
using Bloom.Workspace;
using DesktopAnalytics;
using L10NSharp;
using Palaso.Reporting;

namespace Bloom.CollectionTab
{
	public partial class LibraryView :  UserControl, IBloomTabArea
	{
		private readonly LibraryModel _model;


		private LibraryListView _collectionListView;
		private LibraryBookView _bookView;

		public LibraryView(LibraryModel model, LibraryListView.Factory libraryListViewFactory,
			LibraryBookView.Factory templateBookViewFactory,
			SelectedTabChangedEvent selectedTabChangedEvent,
			SendReceiveCommand sendReceiveCommand)
		{
			_model = model;
			InitializeComponent();

			_toolStrip.Renderer = new NoBorderToolStripRenderer();

			_collectionListView = libraryListViewFactory();
			_collectionListView.Dock = DockStyle.Fill;
			splitContainer1.Panel1.Controls.Add(_collectionListView);

			_bookView = templateBookViewFactory();
			_bookView.Dock = DockStyle.Fill;
			splitContainer1.Panel2.Controls.Add(_bookView);

			splitContainer1.SplitterDistance = _collectionListView.PreferredWidth;
			_makeBloomPackButton.Visible = model.IsShellProject;
			_sendReceiveButton.Visible = Settings.Default.ShowSendReceive;

			if (sendReceiveCommand != null)
			{
				_sendReceiveButton.Click += (x, y) => sendReceiveCommand.Raise(this);
				_sendReceiveButton.Enabled = !SendReceiver.SendReceiveDisabled;
			}
			else
				_sendReceiveButton.Enabled = false;

			selectedTabChangedEvent.Subscribe(c=>
												{
													if (c.To == this)
													{
														Logger.WriteEvent("Entered Collections Tab");
													}
												});
		}

		public string CollectionTabLabel
		{
			get { return LocalizationManager.GetString("CollectionTab.CollectionTabLabel","Collections"); }//_model.IsShellProject ? "Shell Collection" : "Collection"; }

		}


		private void OnMakeBloomPackButton_Click(object sender, EventArgs e)
		{
			_collectionListView.MakeBloomPack(false);
		}

		public string HelpTopicUrl
		{
			get
			{
				if (_model.IsShellProject)
				{
					return "/Tasks/Source_Collection_tasks/Source_Collection_tasks_overview.htm";
				}
				else
				{
					return "/Tasks/Vernacular_Collection_tasks/Vernacular_Collection_tasks_overview.htm";
				}
			}
		}

		public Control TopBarControl
		{
			get { return _topBarControl; }
		}

		private void LibraryView_Load(object sender, EventArgs e)
		{

		}
	}
}
