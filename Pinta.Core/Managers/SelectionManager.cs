// 
// SelectionManager.cs
//  
// Author:
//       Greg Lowe <greg@vis.net.nz>
// 
// Copyright (c) 2010 Greg Lowe
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

namespace Pinta.Core
{
	public enum SelectCombineMode : int
	{
		Replace = 0,
		Add = 1,
		Subtract = 2,
		Intersect = 3
	}

	public enum SelectionDisplayStyle
	{
		Outline,
		RedMask
	}

	public delegate void CairoDrawingOperation (); //(Cairo.Context cr);

	public class Mask : IDisposable
	{
		public Mask (ImageSurface maskSurface)
		{
			MaskSurface = (maskSurface == null) ? null : maskSurface.Clone ();
		}

		// This method should only be used by the selection manager.
		internal ImageSurface MaskSurface { get; private set; }

		public void Dispose ()
		{
			if (MaskSurface != null)
				MaskSurface.Dispose ();
		}
	}

	public class SelectionManager
	{
		bool is_selection_active;
		ImageSurface mask_surface;
		Outline[] mask_outlines;

		public SelectionManager ()
		{
			mask_surface = null;
			mask_outlines = null;
			IsSelectionActive = false;
			CombineMode = SelectCombineMode.Replace;
			DisplayStyle = SelectionDisplayStyle.RedMask;
			OffsetX = 0;
			OffsetY = 0;
		}

		// Keeps track of whether there is currently a selection.
		public bool IsSelectionActive {
			get { return is_selection_active; }
			set {
				is_selection_active = value;

				PintaCore.Actions.Edit.Deselect.Sensitive = is_selection_active;
				PintaCore.Actions.Edit.EraseSelection.Sensitive = is_selection_active;
				PintaCore.Actions.Edit.FillSelection.Sensitive = is_selection_active;
				PintaCore.Actions.Image.CropToSelection.Sensitive = is_selection_active;
			}
		}

		//Perhaps set action states in an event handler instead.
		//public event EventHandler<EventArgs> IsSelectionActiveChanged;

		public SelectCombineMode CombineMode { get; set; }

		public SelectionDisplayStyle DisplayStyle { get; set; }

		public Gdk.Rectangle Bounds { get; private set; }

		public int OffsetX { get; set; }
		public int OffsetY { get; set; }

		public Mask CopySelectionMask ()
		{
			return new Mask (mask_surface);
		}

		public void SetSelectionMask (Mask mask)
		{
			if (mask == null)
				throw new ArgumentNullException ("mask");

			mask_surface = mask.MaskSurface;
		}

		public void SelectAll ()
		{
			int imageWidth = PintaCore.Workspace.ImageSize.Width;
			int imageHeight = PintaCore.Workspace.ImageSize.Height;
			SetSelection (new Gdk.Rectangle (0, 0, imageWidth, imageHeight));
		}

		public void Deselect ()
		{
			IsSelectionActive = false;
			if (mask_surface != null)
				mask_surface.Dispose ();
			mask_surface = null;
		}

		public void SetSelection (Gdk.Rectangle rect)
		{
			var mode = CombineMode;
			CombineMode = SelectCombineMode.Replace;
			Select (rect);
			CombineMode = mode;
		}

		public void SetSelection (Cairo.Path path)
		{
			var mode = CombineMode;
			CombineMode = SelectCombineMode.Replace;
			Select (path);
			CombineMode = mode;
		}

		public void Select (Gdk.Rectangle rect)
		{
			// Create a cairo rectangle path.
			using (var s = new ImageSurface (Format.A1, 1, 1))
			using (var cr = new Cairo.Context (s)) {
				cr.Rectangle (rect.X, rect.Y, rect.Width, rect.Height);
				using (var path = cr.CopyPath ()) {
					UpdateSelectionMask (rect, path);
				}
			}
		}

		public void Select (Cairo.Path path)
		{
			// Get path bounds.
//			var bounds = new Gdk.Rectangle ();
//			using (var s = new ImageSurface (Format.A1, 1, 1))
//			using (var cr = new Cairo.Context (s)) {
//				cr.AppendPath (path);
//				var r = cr.FillExtents ();
//				bounds = new Gdk.Rectangle ((int) Math.Floor (r.X),
//				                            (int) Math.Floor (r.Y),
//				                            (int) (Math.Ceiling (r.Width + r.X) - Math.Floor (r.X)),
//				                            (int) (Math.Ceiling (r.Height + r.Y) - Math.Floor (r.Y)));
//			}

			var bounds = path.GetBounds ();

			UpdateSelectionMask (bounds, path);
		}

		void UpdateSelectionMask (Gdk.Rectangle bounds, Cairo.Path path)
		{
			if (path == null)
				return;

			IsSelectionActive = true;

			int imageWidth = PintaCore.Workspace.ImageSize.Width;
			int imageHeight = PintaCore.Workspace.ImageSize.Height;

			// If image size has changed, dispose old mask.
			if (mask_surface != null
			    && (mask_surface.Width != imageWidth
			    || mask_surface.Height != imageHeight)) {
				mask_surface.Dispose ();
				mask_surface = null;
			}

			// Create new mask surface if required.
			if (mask_surface == null)
				mask_surface = new ImageSurface (Format.A8, imageWidth, imageHeight);

			// Update mask.
			using (var cr = new Context (mask_surface)) {
				if (CombineMode == SelectCombineMode.Replace) {
					Bounds = bounds;
					cr.Operator = Operator.Clear;
					cr.Paint ();
				} else {
					Bounds.Intersect(bounds);
				}

				cr.Operator = GetOperator (CombineMode);
				cr.SetSourceRGBA (0, 0, 0, 1);
				cr.AppendPath (path);
				cr.Fill ();
			}

			UpdateSelectionOutline ();
		}

		void UpdateSelectionOutline ()
		{
			mask_outlines = EdgeExtractor.Extract (mask_surface);
		}

		public void DrawSelectionOutline (Cairo.Context cr, double scale)
		{
			if (mask_outlines == null)
				return;

			foreach (var outline in mask_outlines) {

				int x, y;

				x = outline.InitialPoint.X + 2;
				y = outline.InitialPoint.Y + 2;

				cr.MoveTo (x * scale + 0.5, y * scale + 0.5);
	
				foreach (var direction in outline.Path) {
	
					if (direction == Direction.Up)
						y--;
	
					else if (direction == Direction.Right)
						x++;
	
					else if (direction == Direction.Left)
						x--;
	
					else if (direction == Direction.Down)
						y++;
	
					cr.LineTo (x * scale + 0.5, y * scale + 0.5);
				}

				//cr.ClosePath ();

				cr.LineWidth = 1;
				cr.SetSourceRGBA (1, 0, 0, 1);
				cr.Stroke ();
			}
		}

		// Re-create the selection mask such that OffsetX and OffsetY can be reset to zero.
		public void ProcessMove ()
		{
			var surface = new ImageSurface (Format.A8, mask_surface.Width, mask_surface.Height);
			using (var cr = new Context (surface)) {
				cr.SetSourceSurface (mask_surface, OffsetX, OffsetY);
				cr.Paint ();
			}
			mask_surface.Dispose ();

			OffsetX = 0;
			OffsetY = 0;
			mask_surface = surface;
		}


		public void Invert ()
		{
			if (!IsSelectionActive) {
				SelectAll ();
			} else {
				var inverse = GetInverseMask ();

				if (mask_surface != null)
					mask_surface.Dispose ();

				mask_surface = inverse;

				// TODO calculate new bounds.
				// May need to use edge extractor to do this.

				// Currently just set them to be the entire image.
				// This is wrong but better than nothing for the moment.
				int w = PintaCore.Workspace.ImageSize.Width;
				int h = PintaCore.Workspace.ImageSize.Height;
				Bounds = new Gdk.Rectangle (0, 0, w, h);
			}
		}

		public void Feather (int radius)
		{
			throw new NotImplementedException ();
		}

		public void Expand (int radius)
		{
			throw new NotImplementedException ();
		}

		public void Contract (int radius)
		{
			throw new NotImplementedException ();
		}

		public void DrawWithSelectionMask (Cairo.Context cr, CairoDrawingOperation drawOp)
		{
			if (drawOp == null)
				throw new ArgumentNullException ("drawOp");

			if (!IsSelectionActive) {
				drawOp ();
			} else {
				cr.PushGroup ();
				drawOp ();
				cr.PopGroupToSource ();

				// Must use nearest neighbor filtering when displaying the mask with a
				// scale of 1 or greater.
				// This will almost always be used with a scale of 1, so this doesn't
				// make any difference in that case.

				var maskPattern = new SurfacePattern (mask_surface);
				maskPattern.Extend = Extend.None;
				maskPattern.Filter = Filter.Nearest;
				cr.Mask (maskPattern);
			}
		}

		public void ClearSelection (Cairo.Context cr)
		{
			cr.Save ();
			if (!IsSelectionActive) {
				cr.Operator = Operator.Clear;
				cr.Paint ();
			} else {
				cr.Operator = Operator.Clear;
				cr.MaskSurface (mask_surface, 0, 0);
			}
			cr.Restore ();
		}

		public void DrawWithInverseSelectionMask (Cairo.Context cr, CairoDrawingOperation drawOp)
		{
			if (drawOp == null)
				throw new ArgumentNullException ("drawOp");

			if (!IsSelectionActive) {
				drawOp ();
			} else {
				cr.PushGroup ();
				drawOp ();
				cr.PopGroupToSource ();
				using (var inverse_mask = GetInverseMask ()) {
					//TODO only use neirest neighbor when zoom is an integer.
					var maskPattern = new SurfacePattern (inverse_mask);
					maskPattern.Extend = Extend.None;
					maskPattern.Filter = Filter.Nearest;
					cr.Mask (maskPattern);
					//cr.MaskSurface (inverse_mask, 0, 0);
				}
			}
		}

		ImageSurface GetInverseMask ()
		{
			var surface = new ImageSurface (Format.A8,
			                                mask_surface.Width,
			                                mask_surface.Height);

			using (var cr = new Cairo.Context (surface)) {
				cr.SetSourceRGBA(0, 0, 0, 1);
				cr.Paint ();
				cr.Operator = Operator.Clear;
				cr.MaskSurface (mask_surface, 0, 0);
			}

			return surface;
		}

		static Cairo.Operator GetOperator (SelectCombineMode mode)
		{
			if (mode == SelectCombineMode.Replace
			    || mode == SelectCombineMode.Add)
				return Operator.Over;

			else if (mode == SelectCombineMode.Subtract)
				return Operator.Clear;

			else if (mode == SelectCombineMode.Intersect)
				return Operator.In;

			throw new InvalidOperationException ();
		}
	}
}
