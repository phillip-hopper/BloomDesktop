﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Gecko;
using Palaso.UI.WindowsForms.Extensions;

namespace Bloom
{
	/// <summary>
	/// Any links (web or file path) are cause the browser or file explorer to open.
	/// </summary>
	public partial class HtmlLabel : UserControl
	{
		private GeckoWebBrowser _browser;
		private string _html;

		public HtmlLabel()
		{
			InitializeComponent();

			_browser = new GeckoWebBrowser();

			_browser.Parent = this;
			_browser.Dock = DockStyle.Fill;
			Controls.Add(_browser);
			_browser.NoDefaultContextMenu = true;
			_browser.Margin = new Padding(0);
		}


		/// <summary>
		/// Just a simple html string, no html, head, body tags.
		/// </summary>
		[Browsable(true),CategoryAttribute("Text")]
		public string HTML
		{
			get { return _html; }
			set
			{
				_html = value;
				if (this.DesignModeAtAll())
					return;

				if (_browser!=null)
				{
					_browser.Visible = !string.IsNullOrEmpty(_html);
					var htmlColor = ColorTranslator.ToHtml(ForeColor);
					if(_browser.Visible)
						_browser.LoadHtml("<!DOCTYPE html><html><head><meta charset=\"UTF-8\"></head><body><span style=\"color:" + htmlColor + "; font-family:Segoe UI, Arial; font-size:" + Font.Size.ToString() + "pt\">" + _html + "</span></body></html>");
				}
			}
		}

		//public string ColorName;

		private void HtmlLabel_Load(object sender, EventArgs e)
		{
			if (this.DesignModeAtAll())
				return;

			HTML = _html;//in the likely case that there's html waiting to be shown
			_browser.DomClick += new EventHandler<DomMouseEventArgs>(OnBrowser_DomClick);

		}

		private void OnBrowser_DomClick(object sender, DomEventArgs ge)
		{
			if (this.DesignModeAtAll())
				return;

			if (ge.Target == null)
				return;
			if (ge.Target.CastToGeckoElement().TagName=="A")
			{
				var url = ge.Target.CastToGeckoElement().GetAttribute("href");
				System.Diagnostics.Process.Start(url);
				ge.Handled = true; //don't let the browser navigate itself
			}
		}
	}
}
