using System;
using Eto.Forms;
using System.Threading.Tasks;
using System.Linq;
using jab = JabbR.Client;
using System.Collections.Generic;
using System.Diagnostics;
using Eto.Drawing;

namespace JabbR.Eto.Model.JabbR
{
	public class JabbRRoom : JabbRChannel
	{
		List<User> users = new List<User> ();
		List<string> owners = new List<string> ();
		TaskCompletionSource<IEnumerable<ChannelMessage>> recentMessages;
		ChannelMessage firstMessage;
		bool historyLoaded;
		TaskCompletionSource<Channel> getChannelInfo = new TaskCompletionSource<Channel> ();
		
		static Image image = Bitmap.FromResource (typeof(JabbRRoom).Assembly, "JabbR.Eto.Resources.room.png");
		
		public override Image Image { get { return image; } }
		
		public override IEnumerable<User> Users { get { return users; } }
		
		public override IEnumerable<string> Owners { get { return owners; } }
		
		public new JabbRServer Server {
			get { return base.Server as JabbRServer; }
		}
		
		public JabbRRoom (JabbRServer server, jab.Models.Room room)
			: base(server)
		{
			Set (room);
		}

		void Set (jab.Models.Room room)
		{
			this.Name = room.Name;
			this.Id = room.Name;
			this.Topic = room.Topic;
			this.Private = room.Private;
			
			recentMessages = new TaskCompletionSource<IEnumerable<ChannelMessage>>();
			getChannelInfo = new TaskCompletionSource<Channel>();
			Server.Client.GetRoomInfo (this.Id).ContinueWith (task => {
				if (task.Exception != null) {
					getChannelInfo.SetException (task.Exception);
					recentMessages.SetException (task.Exception);
				}
				else {
					this.Topic = task.Result.Topic;
					this.Private = task.Result.Private;
					
					lock (users) {
						this.users.Clear ();
						this.users.AddRange (from r in task.Result.Users select new JabbRUser (r));
					}
					lock (owners) {
						this.owners.Clear ();
						this.owners.AddRange (task.Result.Owners);
					}
					var messages = (from m in task.Result.RecentMessages select CreateMessage(m));
					historyLoaded = true;
					if (firstMessage != null) {
						// filter up to the first already received message
						messages = messages.TakeWhile (r => r.When < firstMessage.When || (r.When == firstMessage.When && r.Content != firstMessage.Content));
						firstMessage = null;
					}
					recentMessages.SetResult (messages);
					getChannelInfo.SetResult (this);
				}
			});
		}
		
		public override Task<IEnumerable<ChannelMessage>> GetHistory (string fromId)
		{
			var task = new TaskCompletionSource<IEnumerable<ChannelMessage>> ();
			if (recentMessages != null) {
				recentMessages.Task.ContinueWith (messages => {
					if (!messages.IsFaulted)
						task.SetResult (messages.Result);
					else 
						task.SetException (messages.Exception);
					recentMessages = null;
				});
			}
			else {
				var previous = Server.Client.GetPreviousMessages (fromId);
				previous.ContinueWith (t => {
					if (t.IsCompleted)
						task.TrySetResult (from m in t.Result select CreateMessage(m));
					else
						task.TrySetException (t.Exception);
				});
			}
			
			return task.Task;
		}
		
		public override void SendMessage (string command)
		{
			var message = new global::JabbR.Client.Models.ClientMessage{
				Id = Guid.NewGuid ().ToString (),
				Room = this.Name,
				Content = command
			};
			if (!command.TrimStart ().StartsWith ("/")) {
				OnMessageReceived (new MessageEventArgs (new ChannelMessage (message.Id, DateTimeOffset.Now, Server.CurrentUser.Name, command)));
			}
			Server.Client.Send (message).ContinueWith (task => {
				Application.Instance.Invoke (() => {
					MessageBox.Show (
						Application.Instance.MainForm,
						string.Format ("Error sending message: {0}", task.Exception)
					);
				});
			}, TaskContinuationOptions.OnlyOnFaulted);
		}
		
		public override void TriggerMessage (ChannelMessage message)
		{
			if (firstMessage == null && !historyLoaded)
				firstMessage = message;
			base.TriggerMessage (message);
		}
		
		public void TriggerMeMessage (string user, string content)
		{
			var message = new MeMessageEventArgs (new MeMessage (DateTimeOffset.Now, user, content));
			OnMeMessageReceived (message);
		}
		
		
		internal void TriggerUserLeft (UserEventArgs e)
		{
			OnUserLeft (e);
			lock (users)
				users.RemoveAll (r => r.Name == e.User.Name);
		}
		
		internal void TriggerUserJoined (UserEventArgs e)
		{
			OnUserJoined (e);
			lock (users) {
				if (!users.Any (r => r.Name == e.User.Name)) {
					users.Add (e.User);
				}
			}
		}
		
		public static ChannelMessage CreateMessage(jab.Models.Message m)
		{
			return new ChannelMessage (m.Id, m.When, m.User.Name, m.Content);
		}
		
		public override Task<Channel> GetChannelInfo ()
		{
			return getChannelInfo.Task;
		}

		internal void TriggerOwnerAdded (jab.Models.User user)
		{
			if (!this.owners.Contains (user.Name)) {
				bool added = false;
				lock (owners) {
					if (!this.owners.Contains (user.Name)) {
						owners.Add (user.Name);
						added = true;
					};
				}
				if (added)
					OnOwnerAdded (new UserEventArgs (new JabbRUser (user), DateTimeOffset.Now));
			}
		}

		internal void TriggerOwnerRemoved (jab.Models.User user)
		{
			if (this.owners.Contains (user.Name)) {
				bool removed = false;
				lock (owners) {
					if (this.owners.Contains (user.Name)) {
						owners.Remove (user.Name);
						removed = true;
					}
				}
				if (removed)
					OnOwnerRemoved (new UserEventArgs (new JabbRUser (user), DateTimeOffset.Now));
			}
		}
		
		static object typinglock = new object ();
		static string lastTypingRoom;
		static DateTime? lastTypingTime;
		
		public override void UserTyping ()
		{
			lock (typinglock) {
				if (lastTypingRoom != this.Name || (lastTypingTime != null && lastTypingTime.Value < DateTime.Now)) {
					lastTypingTime = DateTime.Now.AddSeconds (5);
					lastTypingRoom = this.Name;
					Server.Client.SetTyping (this.Name);
				}
			}
		}
		
		public override int CompareTo (object obj)
		{
			if (obj is JabbRChat) return -1;
			else return base.CompareTo (obj);
		}
	}
}

