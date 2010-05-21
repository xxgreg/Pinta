// 
// LineCurveTool.cs
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
	public class LineCurveTool : ShapeTool
	{
		public override string Name {
			get { return "Line"; }
		}
		public override string Icon {
			get { return "Tools.Line.png"; }
		}
		public override string StatusBarText {
			get { return "Left click to draw with primary color, right click for secondary color"; }
		}
		protected override bool ShowStrokeComboBox {
			get { return false; }
		}
		
		protected override Rectangle DrawShape (Rectangle rect, Layer l)
		{
			Rectangle dirty = new Rectangle (0, 0, 0, 0);
			
			using (Context g = new Context (l.Surface)) {
				PintaCore.Selection.DrawWithSelectionMask(g, delegate {
					g.Antialias = Antialias.Subpixel;
					dirty = g.DrawLine (shape_origin,
					                    current_point,
					                    outline_color,
					                    BrushWidth);
				});
			}
			
			return dirty;
		}
	}
}
