// 
// Layer.cs
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
using System.ComponentModel;
using System.Collections.Specialized;
using Cairo;

namespace Pinta.Core
{
	public class Layer : ObservableObject
	{	
		private double opacity;
		private bool hidden;
		private string name;
		private bool tiled;
		private PointD offset;
		
		public Layer () : this (null)
		{
		}
		
		public Layer (ImageSurface surface) : this (surface, false, 1f, "")
		{
		}
		
		public Layer (ImageSurface surface, bool hidden, double opacity, string name)
		{
			Surface = surface;
			Offset = new PointD (0, 0);
			
			this.hidden = hidden;
			this.opacity = opacity;
			this.name = name;			
		}
		
		public ImageSurface Surface { get; set; }
		public bool Tiled { get; set; }	
		public PointD Offset { get; set; }		
		
		public static readonly string OpacityProperty = "Opacity";
		public static readonly string HiddenProperty = "Hidden";
		public static readonly string NameProperty = "Name";		
		
		public double Opacity {
			get { return opacity; }
			set { if (opacity != value) SetValue (OpacityProperty, ref opacity, value); }
		}
		
		public bool Hidden {
			get { return hidden; }
			set { if (hidden != value) SetValue (HiddenProperty, ref hidden, value); }
		}
		
		public string Name {
			get { return name; }
			set { if (name != value) SetValue (NameProperty, ref name, value); }
		}				
			
		public void Clear ()
		{
			using (Context g = new Context (Surface)) {
				g.Operator = Operator.Clear;
				g.Paint ();
			}
		}
		
		public void FlipHorizontal ()
		{
			Layer dest = PintaCore.Layers.CreateLayer ();
			
			using (Cairo.Context g = new Cairo.Context (dest.Surface)) {
				g.Matrix = new Matrix (-1, 0, 0, 1, Surface.Width, 0);
				g.SetSource (Surface);
				
				g.Paint ();
			}
			
			Surface old = Surface;
			Surface = dest.Surface;
			(old as IDisposable).Dispose ();
		}
		
		public void FlipVertical ()
		{
			Layer dest = PintaCore.Layers.CreateLayer ();
			
			using (Cairo.Context g = new Cairo.Context (dest.Surface)) {
				g.Matrix = new Matrix (1, 0, 0, -1, 0, Surface.Height);
				g.SetSource (Surface);
				
				g.Paint ();
			}
			
			Surface old = Surface;
			Surface = dest.Surface;
			(old as IDisposable).Dispose ();
		}
		
		public void Rotate180 ()
		{
			Layer dest = PintaCore.Layers.CreateLayer ();
			
			using (Cairo.Context g = new Cairo.Context (dest.Surface)) {
				g.Matrix = new Matrix (-1, 0, 0, -1, Surface.Width, Surface.Height);
				g.SetSource (Surface);
				
				g.Paint ();
			}
			
			Surface old = Surface;
			Surface = dest.Surface;
			(old as IDisposable).Dispose ();
		}
		
		public void Rotate90CW ()
		{
			int w = PintaCore.Workspace.ImageSize.X;
			int h = PintaCore.Workspace.ImageSize.Y;
			
			Layer dest = PintaCore.Layers.CreateLayer (string.Empty, h, w);
			
			using (Cairo.Context g = new Cairo.Context (dest.Surface)) {
				g.Translate (h / 2d, w / 2d);
				g.Rotate (Math.PI / 2);
				g.Translate (-w / 2d, -h / 2d);
				g.SetSource (Surface);
				
				g.Paint ();
			}
			
			Surface old = Surface;
			Surface = dest.Surface;
			(old as IDisposable).Dispose ();
		}
		
		public void Rotate90CCW ()
		{
			int w = PintaCore.Workspace.ImageSize.X;
			int h = PintaCore.Workspace.ImageSize.Y;
			
			Layer dest = PintaCore.Layers.CreateLayer (string.Empty, h, w);
			
			using (Cairo.Context g = new Cairo.Context (dest.Surface)) {
				g.Translate (h / 2, w / 2);
				g.Rotate (Math.PI / -2);
				g.Translate (-w / 2, -h / 2);
				g.SetSource (Surface);
				
				g.Paint ();
			}
			
			Surface old = Surface;
			Surface = dest.Surface;
			(old as IDisposable).Dispose ();
		}
		
		public unsafe void HueSaturation (int hueDelta, int satDelta, int lightness)
		{
			ImageSurface dest = Surface.Clone ();
			ColorBgra* dstPtr = (ColorBgra*)dest.DataPtr;
			
			int len = Surface.Data.Length / 4;
			
			// map the range [0,100] -> [0,100] and the range [101,200] -> [103,400]
			if (satDelta > 100) 
				satDelta = ((satDelta - 100) * 3) + 100;
			
			UnaryPixelOp op;
			
			if (hueDelta == 0 && satDelta == 100 && lightness == 0)
				op = new UnaryPixelOps.Identity ();
			else
				op = new UnaryPixelOps.HueSaturationLightness (hueDelta, satDelta, lightness);
			
			op.Apply (dstPtr, len);
			
			using (Context g = new Context (Surface)) {
				g.AppendPath (PintaCore.Layers.SelectionPath);
				g.FillRule = FillRule.EvenOdd;
				g.Clip ();

				g.SetSource (dest);
				g.Paint ();
			}

			(dest as IDisposable).Dispose ();
		}

		public void Resize (int width, int height)
		{
			ImageSurface dest = new ImageSurface (Format.Argb32, width, height);

			using (Context g = new Context (dest)) {
				g.Scale ((double)width / (double)Surface.Width, (double)height / (double)Surface.Height);
				g.SetSourceSurface (Surface, 0, 0);
				g.Paint ();
			}

			(Surface as IDisposable).Dispose ();
			Surface = dest;
		}

		public void ResizeCanvas (int width, int height, Anchor anchor)
		{
			ImageSurface dest = new ImageSurface (Format.Argb32, width, height);

			int delta_x = Surface.Width - width;
			int delta_y = Surface.Height - height;
			
			using (Context g = new Context (dest)) {
				switch (anchor) {
					case Anchor.NW:
						g.SetSourceSurface (Surface, 0, 0);
						break;
					case Anchor.N:
						g.SetSourceSurface (Surface, -delta_x / 2, 0);
						break;
					case Anchor.NE:
						g.SetSourceSurface (Surface, -delta_x, 0);
						break;
					case Anchor.E:
						g.SetSourceSurface (Surface, -delta_x, -delta_y / 2);
						break;
					case Anchor.SE:
						g.SetSourceSurface (Surface, -delta_x, -delta_y);
						break;
					case Anchor.S:
						g.SetSourceSurface (Surface, -delta_x / 2, -delta_y);
						break;
					case Anchor.SW:
						g.SetSourceSurface (Surface, 0, -delta_y);
						break;
					case Anchor.W:
						g.SetSourceSurface (Surface, 0, -delta_y / 2);
						break;
					case Anchor.Center:
						g.SetSourceSurface (Surface, -delta_x / 2, -delta_y / 2);
						break;
				}
				
				g.Paint ();
			}

			(Surface as IDisposable).Dispose ();
			Surface = dest;
		}

		public void Crop (Gdk.Rectangle rect)
		{
			ImageSurface dest = new ImageSurface (Format.Argb32, rect.Width, rect.Height);

			using (Context g = new Context (dest)) {
				g.SetSourceSurface (Surface, -rect.X, -rect.Y);
				g.Paint ();
			}

			(Surface as IDisposable).Dispose ();
			Surface = dest;
		}
	}
}
