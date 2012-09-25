using System;
using Eto.Forms;
using Eto;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using JabbR.Eto.Model;
using System.Net;
using System.Diagnostics;
using System.Linq;

namespace JabbR.Eto
{
	public interface IJabbRApplication : IApplication
	{
		string BadgeLabel { get; set; }
		
		string EncryptString (string serverName, string accountName, string password);
		string DecryptString (string serverName, string accountName, string value);
	}
	
	
	public class JabbRApplication : Application, IXmlReadable
	{
		IJabbRApplication handler;

		public Configuration Configuration { get; private set; }

		public static new JabbRApplication Instance
		{
			get { return Application.Instance as JabbRApplication; }
		}
		
		public JabbRApplication ()
			: base(Generator.Detect, typeof(IJabbRApplication))
		{
			ServicePointManager.DefaultConnectionLimit = 100;
			
			this.Style = "application";
			this.Name = "JabbReto";
			this.Configuration = new JabbR.Eto.Model.Configuration();
			HandleEvent (TerminatingEvent);
			handler = (IJabbRApplication)Handler;
		}

		string SettingsFileName
		{
			get { 
				var path = EtoEnvironment.GetFolderPath (EtoSpecialFolder.ApplicationSettings);
				return Path.Combine (path, "settings.xml");
				//return Path.Combine (path, "JabbR.Eto.settings");
			}
		}
		
		public override void OnInitialized (EventArgs e)
		{
			base.OnInitialized (e);
			var form = new MainForm(Configuration);
			this.MainForm = form;
			
			if (File.Exists (SettingsFileName)) {
				//JsonConvert.PopulateObject (File.ReadAllText(SettingsFileName), form);
				try {
					this.LoadXml (SettingsFileName);
				}
				catch (Exception ex) {
					// don't worry about not loading
					Debug.WriteLine ("Error loading settings: {0}", ex);
				}
			}
			form.Initialize();
			this.BadgeLabel = null;
			this.MainForm.Show ();
			
			foreach (var server in Configuration.Servers) {
				if (server.ConnectOnStartup)
					server.Connect ();
			}
			
			if (!Configuration.Servers.Any ()) {
				Application.Instance.AsyncInvoke (delegate {
					var action = new Actions.AddServer {
						AutoConnect = true
					};
					action.Activate ();
				});
			}
			
		}
		
		public override void OnTerminating (System.ComponentModel.CancelEventArgs e)
		{
			base.OnTerminating (e);
			Configuration.DisconnectAll ();
			SaveConfiguration ();
		}
		
		public void SaveConfiguration()
		{
			this.SaveXml (SettingsFileName, "jabbreto");
		}
		
		public string BadgeLabel {
			get { return handler.BadgeLabel; }
			set { handler.BadgeLabel = value; }
		}
		
		public string EncryptString (string serverName, string accountName, string password)
		{
			return handler.EncryptString(serverName, accountName, password);
		}
		
		public string DecryptString (string serverName, string accountName, string value)
		{
			return handler.DecryptString(serverName, accountName, value);
		}
		

		#region IXmlReadable implementation
		public void ReadXml (System.Xml.XmlElement element)
		{
			element.ReadChildXml("interface", this.MainForm as IXmlReadable);
			element.ReadChildXml ("config", Configuration);
		}

		public void WriteXml (System.Xml.XmlElement element)
		{
			element.WriteChildXml("interface", this.MainForm as IXmlReadable);
			element.WriteChildXml ("config", Configuration);
		}
		#endregion
	}
}

