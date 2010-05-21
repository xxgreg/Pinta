// 
// LassoSelectTool.cs
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
	public class LassoSelectTool : SelectTool
	{
		private Path path;

		public LassoSelectTool ()
		{
		}

		#region Properties
		public override string Name { get { return "Lasso Select"; } }
		public override string Icon { get { return "Tools.LassoSelect.png"; } }
		public override string StatusBarText { get { return "Click and drag to draw the outline for a selection area"; } }
		#endregion

		protected override void DoSelect (int x, int y, int width, int height)
		{
			PintaCore.Selection.Select (path);
			PintaCore.Workspace.Invalidate ();
		}

		#region Mouse Handlers
		protected override void OnMouseDown (Gtk.DrawingArea canvas, Gtk.ButtonPressEventArgs args, Cairo.PointD point)
		{
			base.OnMouseDown (canvas, args, point);
			
			path = null;
		}

		protected override void OnMouseMove (object o, Gtk.MotionNotifyEventArgs args, Cairo.PointD point)
		{
			if (!is_drawing)
				return;

			double x = Utility.Clamp (point.X, 0, PintaCore.Workspace.ImageSize.Width - 1);
			double y = Utility.Clamp (point.Y, 0, PintaCore.Workspace.ImageSize.Height - 1);

			ImageSurface surf = PintaCore.Layers.ToolLayer.Surface;

			using (Context g = new Context (surf)) {
				g.Antialias = Antialias.Subpixel;

				if (path != null) {
					g.AppendPath (path);
					(path as IDisposable).Dispose ();
				}
					
				g.LineTo (x, y);

				path = g.CopyPath ();
				
				g.FillRule = FillRule.EvenOdd;
				g.ClosePath ();

				if (PintaCore.Selection.WorkingSelection != null)
					PintaCore.Selection.WorkingSelection.Dispose ();

				PintaCore.Selection.WorkingSelection = g.CopyPath ();
			}

			PintaCore.Workspace.Invalidate ();
		}

		protected override void OnMouseUp (Gtk.DrawingArea canvas, Gtk.ButtonReleaseEventArgs args, Cairo.PointD point)
		{
			base.OnMouseUp (canvas, args, point);

			ImageSurface surf = PintaCore.Layers.CurrentLayer.Surface;

			PintaCore.Workspace.Invalidate ();
		}
		#endregion
	}
}
