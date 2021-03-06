using System;
using Eto.Forms;
using JabbR.Desktop.Model;
using Eto.Drawing;
using Eto;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JabbR.Desktop.Interface.Dialogs
{
    public class ServerDialog : Dialog
    {
        Button cancelButton;
        Button connectButton;
        Button disconnectButton;
        bool allowConnect;
        
        public Server Server { get; private set; }
        
        public bool AllowCancel
        {
            get { return cancelButton.Enabled; }
            set { cancelButton.Enabled = cancelButton.Visible = value; }
        }
        
        public ServerDialog(Server server, bool isNew, bool allowConnect)
        {
            this.allowConnect = allowConnect && !isNew;
            this.Server = server;
            this.Title = "Add Server";
            this.MinimumSize = new Size(300, 0);
            this.DataContext = server;
            
            var layout = new DynamicLayout();
            
            layout.BeginVertical();
            
            layout.AddRow(new Label { Text = "Server Name"}, ServerName());
            
            // generate server-specific edit controls
            server.GenerateEditControls(layout, isNew);
            
            layout.AddRow(null, AutoConnectButton());
            
            layout.EndBeginVertical();

            layout.AddRow(Connect(), Disconnect(), null, cancelButton = this.CancelButton(), this.OkButton("Save", () => SaveData()));
            
            layout.EndVertical();

            Content = layout;
            
            SetVisibility();
        }

        bool SaveData(bool allowCancel = true)
        {
            UpdateBindings();
            if (Server.CheckAuthentication(this, allowCancel, true) && Server.PreSaveSettings(this))
            {
                return true;
            }
            return false;
        }
        
        void SetVisibility()
        {
            connectButton.Visible = allowConnect && !Server.IsConnected;
            disconnectButton.Visible = allowConnect && Server.IsConnected;
        }
        
        Control AutoConnectButton()
        {
            var control = new CheckBox { Text = "Connect on Startup" };
            control.CheckedBinding.Bind <Server>(c => c.ConnectOnStartup, (c,v) => c.ConnectOnStartup = v ?? false, mode: DualBindingMode.OneWay);
            return control;
        }
        
        Control ServerName()
        {
            var control = new TextBox();
            control.TextBinding.Bind <Server>(c => c.Name, (c,v) => c.Name = v, mode: DualBindingMode.OneWay);
            return control;
        }

        Control Connect()
        {
            var control = connectButton = new Button {
                Text = "Connect"
            };
            control.Click += (sender, e) => {
                if (SaveData(false))
                {
                    Server.Connect();
                    Debug.WriteLine("Closing Dialog!");
                    Close(DialogResult.Ok);
                }
            };
            return control;
        }
        
        Control Disconnect()
        {
            var control = disconnectButton = new Button {
                Text = "Disconnect"
            };
            control.Click += (sender, e) => {
                try
                {
                    Server.Disconnect();
                    Close(DialogResult.Ok);
                }
                catch (Exception ex)
                {
                    var msg = string.Format("Error disconnecting from server {0}", ex.GetBaseException().Message);
                    MessageBox.Show(this, msg, MessageBoxType.Error);
                }
                Close(DialogResult.Ok);
            };
            return control;
        }
        
        public override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (DialogResult == DialogResult.Ok)
                UpdateBindings();
        }
    }
}

