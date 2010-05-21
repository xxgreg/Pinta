// 
// FileActions.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Gtk;
using Gdk;
using Mono.Unix;


namespace Pinta.Core
{
	public class FileActions
	{
		public Gtk.Action New { get; private set; }
		public Gtk.Action Open { get; private set; }
		public Gtk.Action OpenRecent { get; private set; }
		public Gtk.Action Close { get; private set; }
		public Gtk.Action Save { get; private set; }
		public Gtk.Action SaveAs { get; private set; }
		public Gtk.Action Print { get; private set; }
		public Gtk.Action Exit { get; private set; }
		
		private RecentData recentData;
		
		public FileActions ()
		{
			New = new Gtk.Action ("New", Mono.Unix.Catalog.GetString ("New..."), null, "gtk-new");
			Open = new Gtk.Action ("Open", Mono.Unix.Catalog.GetString ("Open..."), null, "gtk-open");
			OpenRecent = new RecentAction ("OpenRecent", Catalog.GetString ("Open Recent"), null, "gtk-open",
			                               RecentManager.Default);
			
			RecentFilter recentFilter = new RecentFilter ();
			recentFilter.AddApplication ("Pinta");
			
			(OpenRecent as RecentAction).AddFilter (recentFilter);
			
			recentData = new RecentData ();
			recentData.AppName = "Pinta";
			recentData.AppExec = GetExecutablePathname ();
			recentData.MimeType = "image/*";

			Close = new Gtk.Action ("Close", Mono.Unix.Catalog.GetString ("Close"), null, "gtk-close");
			Save = new Gtk.Action ("Save", Mono.Unix.Catalog.GetString ("Save"), null, "gtk-save");
			SaveAs = new Gtk.Action ("SaveAs", Mono.Unix.Catalog.GetString ("Save As..."), null, "gtk-save-as");
			Print = new Gtk.Action ("Print", Mono.Unix.Catalog.GetString ("Print"), null, "gtk-print");
			Exit = new Gtk.Action ("Exit", Mono.Unix.Catalog.GetString ("Quit"), null, "gtk-quit");

//			OpenRecent.Sensitive = false;
			Close.Sensitive = false;
			Print.Sensitive = false;
		}

		#region Initialization
		public void CreateMainMenu (Gtk.Menu menu)
		{
			menu.Remove (menu.Children[1]);
			
			menu.Append (New.CreateAcceleratedMenuItem (Gdk.Key.N, Gdk.ModifierType.ControlMask));
			menu.Append (Open.CreateAcceleratedMenuItem (Gdk.Key.O, Gdk.ModifierType.ControlMask));
			menu.Append (OpenRecent.CreateMenuItem ());
			//menu.Append (Close.CreateAcceleratedMenuItem (Gdk.Key.W, Gdk.ModifierType.ControlMask));
			menu.AppendSeparator ();
			menu.Append (Save.CreateAcceleratedMenuItem (Gdk.Key.S, Gdk.ModifierType.ControlMask));
			menu.Append (SaveAs.CreateAcceleratedMenuItem (Gdk.Key.S, Gdk.ModifierType.ControlMask | Gdk.ModifierType.ShiftMask));
			menu.AppendSeparator ();
			//menu.Append (Print.CreateAcceleratedMenuItem (Gdk.Key.P, Gdk.ModifierType.ControlMask));
			//menu.AppendSeparator ();
			menu.Append (Exit.CreateAcceleratedMenuItem (Gdk.Key.Q, Gdk.ModifierType.ControlMask));
		}
		
		public void RegisterHandlers ()
		{
			Open.Activated += HandlePintaCoreActionsFileOpenActivated;
			(OpenRecent as RecentAction).ItemActivated += HandleOpenRecentItemActivated;
			Save.Activated += HandlePintaCoreActionsFileSaveActivated;
			SaveAs.Activated += HandlePintaCoreActionsFileSaveAsActivated;
			Exit.Activated += HandlePintaCoreActionsFileExitActivated;
		}
		
		#endregion

		#region Public Methods
		public bool OpenFile (string file)
		{
			bool fileOpened = false;
			
			try {
				// Open the image and add it to the layers
				Pixbuf bg = new Pixbuf (file);

				PintaCore.Layers.Clear ();
				PintaCore.History.Clear ();
				PintaCore.Layers.DestroySelectionLayer ();

				PintaCore.Workspace.ImageSize = new Size (bg.Width, bg.Height);
				PintaCore.Workspace.CanvasSize = new Gdk.Size (bg.Width, bg.Height);

				PintaCore.Selection.Deselect ();

				Layer layer = PintaCore.Layers.AddNewLayer (System.IO.Path.GetFileName (file));

				using (Cairo.Context g = new Cairo.Context (layer.Surface)) {
					CairoHelper.SetSourcePixbuf (g, bg, 0, 0);
					g.Paint ();
				}

				bg.Dispose ();

				PintaCore.Workspace.DocumentPath = System.IO.Path.GetFullPath (file);
				PintaCore.History.PushNewItem (new BaseHistoryItem ("gtk-open", "Open Image"));
				PintaCore.Workspace.IsDirty = false;
				PintaCore.Actions.View.ZoomToWindow.Activate ();
				PintaCore.Workspace.Invalidate ();
				
				fileOpened = true;
			} catch {
				MessageDialog md = new MessageDialog (PintaCore.Chrome.MainWindow, DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, "Could not open file: {0}", file);
				md.Title = "Error";
				
				md.Run ();
				md.Destroy ();
			}
			
			return fileOpened;
		}
		#endregion
		
		static string GetExecutablePathname()
		{
			string executablePathName = System.Environment.GetCommandLineArgs ()[0];
			executablePathName = System.IO.Path.GetFullPath (executablePathName);
			
			return executablePathName;
		}
		
		void AddRecentFileUri (string uri)
		{
			RecentManager.Default.AddFull (uri, recentData);
		}
		
		#region Action Handlers
		private void HandleOpenRecentItemActivated (object sender, EventArgs e)
		{
			bool canceled = false;

			if (PintaCore.Workspace.IsDirty) {
				var primary = Catalog.GetString ("Save the changes to image \"{0}\" before opening a new image?");
				var secondary = Catalog.GetString ("If you don't save, all changes will be permanently lost.");
				var markup = "<span weight=\"bold\" size=\"larger\">{0}</span>\n\n{1}\n";
				markup = string.Format (markup, primary, secondary);

				var md = new MessageDialog (PintaCore.Chrome.MainWindow, DialogFlags.Modal,
											MessageType.Question, ButtonsType.None, true,markup,
											System.IO.Path.GetFileName (PintaCore.Workspace.Filename));

				md.AddButton (Catalog.GetString ("Continue without saving"), ResponseType.No);
				md.AddButton (Stock.Cancel, ResponseType.Cancel);
				md.AddButton (Stock.Save, ResponseType.Yes);

				md.DefaultResponse = ResponseType.Cancel;

				var response = (ResponseType)md.Run ();
				md.Destroy ();

				if (response == ResponseType.Yes) {
					Save.Activate ();
				}
				else {
					canceled = response == ResponseType.Cancel;
				}
			}

			if (!canceled) {
				string fileUri = (sender as RecentAction).CurrentUri;

				OpenFile (new Uri (fileUri).LocalPath);

				PintaCore.Workspace.ActiveDocument.HasFile = true;
			}
		}


		private void HandlePintaCoreActionsFileOpenActivated (object sender, EventArgs e)
		{
			bool canceled = false;

			if (PintaCore.Workspace.IsDirty) {
				var primary = Catalog.GetString ("Save the changes to image \"{0}\" before opening a new image?");
				var secondary = Catalog.GetString ("If you don't save, all changes will be permanently lost.");
				var markup = "<span weight=\"bold\" size=\"larger\">{0}</span>\n\n{1}\n";
				markup = string.Format (markup, primary, secondary);

				var md = new MessageDialog (PintaCore.Chrome.MainWindow, DialogFlags.Modal,
				                            MessageType.Question, ButtonsType.None, true,
				                            markup,
				                            System.IO.Path.GetFileName (PintaCore.Workspace.Filename));

				md.AddButton (Catalog.GetString ("Continue without saving"), ResponseType.No);
				md.AddButton (Stock.Cancel, ResponseType.Cancel);
				md.AddButton (Stock.Save, ResponseType.Yes);

				md.DefaultResponse = ResponseType.Cancel;

				ResponseType response = (ResponseType)md.Run ();
				md.Destroy ();

				if (response == ResponseType.Yes) {
					Save.Activate ();
				}
				else {
					canceled = response == ResponseType.Cancel;
				}
			}

			if (!canceled) {
				var fcd = new Gtk.FileChooserDialog (Catalog.GetString ("Open Image File"), PintaCore.Chrome.MainWindow,
														FileChooserAction.Open, Gtk.Stock.Cancel, Gtk.ResponseType.Cancel,
														Gtk.Stock.Open, Gtk.ResponseType.Ok);

				int response = fcd.Run ();

			
				if (response == (int)Gtk.ResponseType.Ok) {
					if (OpenFile (fcd.Filename)) {
						AddRecentFileUri (fcd.Uri);

						PintaCore.Workspace.ActiveDocument.HasFile = true;
					}
				}
	
				fcd.Destroy ();
			}
		}
		
		private void HandlePintaCoreActionsFileSaveActivated (object sender, EventArgs e)
		{
			if (PintaCore.Workspace.ActiveDocument.HasFile)
				SaveFile (PintaCore.Workspace.ActiveDocument.Pathname);
			else
				HandlePintaCoreActionsFileSaveAsActivated (null, EventArgs.Empty);
		}
		
		private void HandlePintaCoreActionsFileSaveAsActivated (object sender, EventArgs e)
		{
			var fcd = new Gtk.FileChooserDialog (Mono.Unix.Catalog.GetString ("Save Image File"),
			                                                       PintaCore.Chrome.MainWindow,
			                                                       FileChooserAction.Save,
			                                                       Gtk.Stock.Cancel,
			                                                       Gtk.ResponseType.Cancel,
			                                                       Gtk.Stock.Save, Gtk.ResponseType.Ok);
			
			fcd.DoOverwriteConfirmation = true;
			
			int response = fcd.Run ();
			
			if (response == (int)Gtk.ResponseType.Ok) {
				SaveFile (fcd.Filename);
				AddRecentFileUri (fcd.Uri);

				PintaCore.Workspace.ActiveDocument.HasFile = true;
			}


			fcd.Destroy ();
		}

		private void HandlePintaCoreActionsFileExitActivated (object sender, EventArgs e)
		{
			bool canceled = false;

			if (PintaCore.Workspace.IsDirty) {
				var primary = Catalog.GetString ("Save the changes to image \"{0}\" before closing?");
				var secondary = Catalog.GetString ("If you don't save, all changes will be permanently lost.");
				var markup = "<span weight=\"bold\" size=\"larger\">{0}</span>\n\n{1}\n";
				markup = string.Format (markup, primary, secondary);

				var md = new MessageDialog (PintaCore.Chrome.MainWindow, DialogFlags.Modal,
				                            MessageType.Question, ButtonsType.None, true,
				                            markup,
				                            System.IO.Path.GetFileName (PintaCore.Workspace.Filename));

				md.AddButton (Catalog.GetString ("Close without saving"), ResponseType.No);
				md.AddButton (Stock.Cancel, ResponseType.Cancel);
				md.AddButton (Stock.Save, ResponseType.Yes);

				// so that user won't accidentally overwrite
				md.DefaultResponse = ResponseType.Cancel;

				ResponseType response = (ResponseType)md.Run ();
				md.Destroy ();
				
				if (response == ResponseType.Yes) {
					Save.Activate ();
				}
				else {
					canceled = response == ResponseType.Cancel;
				}
			}

			if (!canceled) {
				PintaCore.History.Clear ();
				Application.Quit ();
			}
		}
		#endregion

		#region Private Methods
		private void SaveFile (string file)
		{
			Cairo.ImageSurface surf = PintaCore.Layers.GetFlattenedImage ();

			Pixbuf pb = surf.ToPixbuf ();

			if (System.IO.Path.GetExtension (file) == ".jpeg" || System.IO.Path.GetExtension (file) == ".jpg")
				pb.Save (file, "jpeg");
			else
				pb.Save (file, "png");

			(pb as IDisposable).Dispose ();
			(surf as IDisposable).Dispose ();
			
			PintaCore.Workspace.Filename = System.IO.Path.GetFileName (file);
			PintaCore.Workspace.IsDirty = false;
		}
		#endregion
	}
}
