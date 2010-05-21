// 
// SelectTool.cs
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
using Cairo;
using Gtk;

namespace Pinta.Core
{
	public abstract class SelectTool : ShapeTool
	{
		protected SelectionHistoryItem hist;
		public override Gdk.Key ShortcutKey { get { return Gdk.Key.S; } }

		protected ToolBarComboBox select_combine_mode;
		protected ToolBarComboBox select_display_style;
		
		#region ToolBar
		// We don't want the ShapeTool's toolbar
		protected override void BuildToolBar (Toolbar tb)
		{
			if (select_combine_mode == null) {
				select_combine_mode = new ToolBarComboBox (100, 0, true, "Single", "Add", "Subtract", "Intersect");
				select_combine_mode.ComboBox.Changed += HandleCombineModeChanged;
			}

			tb.AppendItem (select_combine_mode);

			if (select_display_style == null) {
				select_display_style = new ToolBarComboBox (100, 0, true, "Outline", "Red Mask");
				select_display_style.ComboBox.Changed += HandleDisplayStyleChanged;
			}

			tb.AppendItem (select_display_style);

			SyncToolbar ();
		}

		void SyncToolbar ()
		{
			if (select_combine_mode != null && select_display_style != null) {
				select_combine_mode.ComboBox.Active = (int) PintaCore.Selection.CombineMode;
				select_display_style.ComboBox.Active = (int) PintaCore.Selection.DisplayStyle;
			}
		}

		void HandleCombineModeChanged (object o, EventArgs e)
		{
			var mode = (SelectCombineMode) select_combine_mode.ComboBox.Active;
			PintaCore.Selection.CombineMode = mode;
		}

		void HandleDisplayStyleChanged (object o, EventArgs e)
		{
			var style = (SelectionDisplayStyle) select_display_style.ComboBox.Active;
			PintaCore.Selection.DisplayStyle = style;
			PintaCore.Workspace.Invalidate ();
		}
		#endregion

		protected override void OnActivated ()
		{
			base.OnActivated ();
			SyncToolbar ();
		}

		protected abstract void DoSelect (int x, int y, int width, int height);

		#region Mouse Handlers
		protected override void OnMouseDown (DrawingArea canvas, ButtonPressEventArgs args, Cairo.PointD point)
		{
			shape_origin = point;
			is_drawing = true;
			
			hist = new SelectionHistoryItem (Icon, Name);
			hist.TakeSnapshot ();
		}

		protected override void OnMouseUp (DrawingArea canvas, ButtonReleaseEventArgs args, Cairo.PointD point)
		{
			double x = point.X;
			double y = point.Y;

			// If the user didn't move the mouse, they want to deselect
			int tolerance = 2;

			if (Math.Abs (shape_origin.X - x) <= tolerance && Math.Abs (shape_origin.Y - y) <= tolerance) {
				PintaCore.Actions.Edit.Deselect.Activate ();
				hist.Dispose ();
				hist = null;
			} else {

				DoSelect ((int) Math.Min (x, shape_origin.X),
					  (int) Math.Min (y, shape_origin.Y),
					  (int) Math.Ceiling(Math.Abs (x - shape_origin.X)),
					  (int) Math.Ceiling(Math.Abs (y - shape_origin.Y)));

				if (hist != null)
					PintaCore.History.PushNewItem (hist);

				hist = null;
			}

			is_drawing = false;

			if (PintaCore.Selection.WorkingSelection != null) {
				PintaCore.Selection.WorkingSelection.Dispose ();
				PintaCore.Selection.WorkingSelection = null;
			}
		}
		
		protected override void OnMouseMove (object o, MotionNotifyEventArgs args, Cairo.PointD point)
		{
			if (!is_drawing)
				return;

			double x = Utility.Clamp (point.X, 0, PintaCore.Workspace.ImageSize.Width - 1);
			double y = Utility.Clamp (point.Y, 0, PintaCore.Workspace.ImageSize.Height - 1);

			Rectangle dirty = DrawShape (PointsToRectangle (shape_origin, new PointD (x, y), (args.Event.State & Gdk.ModifierType.ShiftMask) == Gdk.ModifierType.ShiftMask), PintaCore.Layers.SelectionLayer);

			PintaCore.Workspace.Invalidate ();
			
			last_dirty = dirty;
		}
		#endregion
	}
}
