// 
// EllipseSelectTool.cs
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

namespace Pinta.Core
{
	public class EllipseSelectTool : SelectTool
	{
		public override string Name {
			get { return "Ellipse Select"; }
		}
		public override string Icon {
			get { return "Tools.EllipseSelect.png"; }
		}
		public override string StatusBarText {
			get { return "Click and drag to draw an elliptical selection. Hold shift to constrain to a circle."; }
		}

		protected override void DoSelect (int x, int y, int width, int height)
		{
			using (var s = new ImageSurface (Format.A1, 1, 1))
			using (var cr = new Cairo.Context (s)) {
				cr.Save ();
				cr.Translate (x + width / 2.0, y + height / 2.0);
				cr.Scale (width / 2.0, height / 2.0);
				cr.Arc (0, 0, 1, 0, 2 * Math.PI);
				cr.Restore ();
				using (var path = cr.CopyPath ()) {
					PintaCore.Selection.Select (path);
				}
			}

			PintaCore.Workspace.Invalidate ();
		}

		protected override Rectangle DrawShape (Rectangle r, Layer l)
		{
			Path path = PintaCore.Layers.SelectionPath;

			using (Context g = new Context (l.Surface))
				PintaCore.Layers.SelectionPath = g.CreateEllipsePath (r);

			(path as IDisposable).Dispose ();
			
			return r;
		}
	}
}
