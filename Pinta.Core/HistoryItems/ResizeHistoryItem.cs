// 
// ResizeHistoryItem.cs
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
using System.Collections.Generic;

namespace Pinta.Core
{
	//TODO properly capture new selection.
	public class ResizeHistoryItem : CompoundHistoryItem
	{
		private int old_width;
		private int old_height;

		private Mask mask;
		private bool is_selection_active;
		
		public ResizeHistoryItem (int oldWidth, int oldHeight) : base ()
		{
			old_width = oldWidth;
			old_height = oldHeight;

			Icon = "Menu.Image.Resize.png";
			Text = "Resize Image";

			mask = PintaCore.Selection.CopySelectionMask ();
			is_selection_active = PintaCore.Selection.IsSelectionActive;
		}

		public override void Undo ()
		{
			int swap_width = PintaCore.Workspace.ImageSize.Width;
			int swap_height = PintaCore.Workspace.ImageSize.Height;

			PintaCore.Workspace.ImageSize = new Gdk.Size (old_width, old_height);
			PintaCore.Workspace.CanvasSize = new Gdk.Size (old_width, old_height);
			
			old_width = swap_width;
			old_height = swap_height;
			
			base.Undo ();
			
			SwapSelection ();
			
			PintaCore.Workspace.Invalidate ();
		}

		public override void Redo ()
		{
			int swap_width = PintaCore.Workspace.ImageSize.Width;
			int swap_height = PintaCore.Workspace.ImageSize.Height;

			PintaCore.Workspace.ImageSize = new Gdk.Size (old_width, old_height);
			PintaCore.Workspace.CanvasSize = new Gdk.Size (old_width, old_height);

			old_width = swap_width;
			old_height = swap_height;

			base.Redo ();

			SwapSelection ();

			PintaCore.Workspace.Invalidate ();
		}

		public override void Dispose ()
		{
			base.Dispose ();

			if (mask != null)
				mask.Dispose ();
		}

		private void SwapSelection ()
		{
			var swap_mask = PintaCore.Selection.CopySelectionMask ();
			var swap_active = PintaCore.Selection.IsSelectionActive;

			PintaCore.Selection.SetSelectionMask (mask);
			PintaCore.Selection.IsSelectionActive = is_selection_active;

			mask = swap_mask;
			is_selection_active = swap_active;
			
			PintaCore.Workspace.Invalidate ();
		}

	}
}
