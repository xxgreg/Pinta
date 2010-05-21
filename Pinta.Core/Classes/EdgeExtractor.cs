using System;
using System.Collections.Generic;

namespace Pinta.Core
{
	enum PixelType : byte {
		Transparent = 0,
		Edge = 127,
		Opaque = 255,
	}
	
	public enum Direction {
		Up, Right, Left, Down
	}
	
	public struct PointInt
	{
		internal int X;
		internal int Y;
	}	
	
	public struct RectInt
	{
		internal int X;
		internal int Y;
		internal int Width;
		internal int Height;
	}
	
	public struct Outline
	{
		internal Cairo.ImageSurface TempSurface;
		internal PointInt InitialPoint;
		internal List<Direction> Path;
		internal RectInt Bounds;
	}	
	
	public static unsafe class EdgeExtractor
	{
		public static Outline[] Extract (Cairo.ImageSurface surface)
		{
			if (surface.Status != Cairo.Status.Success)
				throw new InvalidOperationException ("Image surface in error state.");
			
			if (surface.Format != Cairo.Format.A8)
				throw new InvalidOperationException ("Unsupported image surface format.");
			
			int width = surface.Width;
			int height = surface.Height;
			
			//TODO why does the temp surface need to be bigger again??
			// where does it prevent overflows?
			//using (var tmpSurface = new ImageSurface (Format.A8, width + 2, height + 2)) {
				
				//Make double size to properly handle 1px thick lines.
				var tmpSurface = new Cairo.ImageSurface (Cairo.Format.A8, (width + 2) * 2, (height + 2) * 2);
			
				byte* src = (byte*) surface.DataPtr;
				byte* tmp = (byte*) tmpSurface.DataPtr;
			
				int src_stride = surface.Stride;
				int tmp_stride = tmpSurface.Stride;
				
				byte* src_row;
				byte* tmp_row;
			
				var list = new List<Outline> ();

				
				// Copy data into tmp
				var time = DateTime.Now;
				//Console.WriteLine (time);
				for (int y = 1; y < height - 1; y++) {
					src_row = src + (y * src_stride);					
					tmp_row = tmp + (((y + 1) * tmp_stride + 1) * 2);
					
					for (int x = 1; x < width - 1; x++) {
						var type = GetPixelType (src_row + x, src_stride);
						if (type == PixelType.Edge)
							type = PixelType.Opaque;					
						byte pixel_value = (byte) type;
						tmp_row [x * 2] = pixel_value;
						tmp_row [x * 2 + 1] = pixel_value;
						tmp_row [x * 2 + tmp_stride] = pixel_value;
						tmp_row [x * 2 + tmp_stride + 1] = pixel_value;
					}
				}
				//Console.WriteLine ("Upscale: " + ((DateTime.Now.Ticks - time.Ticks) / 10000));
				time = DateTime.Now;
			
				// Find edges in temp
				// Possibly more efficient to combine this with the last step?
				for (int y = 1; y < height - 1; y++) {
					tmp_row = tmp + (((y + 1) * tmp_stride + 1) * 2);					
					for (int x = 1; x < width - 1; x++) {
						tmp_row [x * 2] = (byte) GetPixelType (tmp_row + x * 2, tmp_stride);
						tmp_row [x * 2 + 1] = (byte) GetPixelType (tmp_row + x * 2 + 1, tmp_stride);
						tmp_row [tmp_stride + x * 2] = (byte) GetPixelType (tmp_row + tmp_stride + x * 2, tmp_stride);
						tmp_row [tmp_stride + x * 2 + 1] = (byte) GetPixelType (tmp_row + tmp_stride + x * 2 + 1, tmp_stride);
					}
				}			
				//Console.WriteLine ("Find edges: " + ((DateTime.Now.Ticks - time.Ticks) / 10000));
			
				int i = 0;
				//surface.WriteToPng ("src.png");
				//tmpSurface.WriteToPng ("tmp" + (++i) + ".png");
			
				// Search tmp for edges and extract them
				time = DateTime.Now;
				for (int y = 1; y < height - 1; y++) {
					//src_row = src + (y * src_stride);					
					tmp_row = tmp + (((y + 1) * tmp_stride) + 1) * 2;
					for (int x = 1; x < width - 1; x++) {
						if (tmp_row [x * 2] == (byte) PixelType.Edge) {
							bool isOutside = tmp_row [(x - tmp_stride - 1) * 2] ==
							                    (byte) PixelType.Transparent;
						
							//TODO Does isOutside determine whether this is a positive or negative shape?
							// probably doesn't matter for my application anyways.
						
							//Console.WriteLine ("Search: " + ((DateTime.Now.Ticks - time.Ticks) / 10000));
						
							//Console.WriteLine ("Extract " + i);
						
							time = DateTime.Now;
						
							var outline = ExtractEdge ((isOutside) ? x - 1 : x,
						                               (isOutside) ? y - 1 : y,
						                               src,
						                               src_stride,
						                               tmp,
						                               tmp_stride);
						
							//Console.WriteLine ("Extract: " + ((DateTime.Now.Ticks - time.Ticks) / 10000));
						
							//tmpSurface.WriteToPng ("tmp" + (++i) + ".png");
						
							outline.TempSurface = tmpSurface;
							list.Add (outline);
						
							time = DateTime.Now;
							//goto EXIT;
						}
					}
				}
				
			//}
			
		EXIT:
			
			return list.ToArray ();
		}
		
		static PixelType GetPixelType (byte* pixel, int stride)
		{
			if (*pixel == 0)
				return PixelType.Transparent;
			
			if (pixel [-stride - 1] == 0)
				return PixelType.Edge;
			
			if (pixel [-stride] == 0)
				return PixelType.Edge;
			
			if (pixel [-stride + 1] == 0)
				return PixelType.Edge;
			
			if (pixel [-1] == 0)
				return PixelType.Edge;
			
			if (pixel [1] == 0)
				return PixelType.Edge;
			
			if (pixel [stride - 1] == 0)
				return PixelType.Edge;
			
			if (pixel [stride] == 0)
				return PixelType.Edge;
			
			if (pixel [stride + 1] == 0)
				return PixelType.Edge;
			
			return PixelType.Opaque;
		}		
		
		static byte GetPixelGroupCase (byte* src_pixel, int src_stride)
		{
			byte pixel_group_case = 0;
			
			if (src_pixel [0] != (byte) PixelType.Transparent) pixel_group_case |= 1;
			if (src_pixel [1] != (byte) PixelType.Transparent) pixel_group_case |= 2;
			if (src_pixel [src_stride] != (byte) PixelType.Transparent) pixel_group_case |= 4;
			if (src_pixel [src_stride + 1] != (byte) PixelType.Transparent) pixel_group_case |= 8;			
			
			return pixel_group_case;
		}
		
		static Outline ExtractEdge (int initialX,
		                            int initialY,
		                            byte* src,
		                            int src_stride,
		                            byte* tmp,
		                            int tmp_stride)
		{
			//Console.WriteLine ("Extract Edge from start point: ({0}, {1})", initialX, initialY);
			
			var path = new List<Direction> ();	
			int x = initialX;
			int y = initialY;
			Direction previous = Direction.Down; // Initial value doesn't matter as long as not Up, or Right.
			
			int minX = x;
			int maxX = x;
			int minY = y;
			int maxY = y;
			
			byte pixel_group_case; // marching squares case.
			byte erase_value;
			byte* src_pixel;
			byte* tmp_pixel;
			Direction direction;
			
			erase_value = (byte) PixelType.Opaque;			
			
			src_pixel = src + (y * src_stride) + x;
			
			pixel_group_case = GetPixelGroupCase (src_pixel, src_stride);			
						
			//Console.WriteLine ("Initial pixel group case: " + pixel_group_case);
			
			//Highlight initial point.
			//tmp_pixel = tmp + (((y + 1) * tmp_stride) + x + 1) * 2;
			//*tmp_pixel = 200;
			
			if (pixel_group_case == 0 || pixel_group_case == 15) {
				//TODO debugging
				//Console.WriteLine ("Boom: {0}, {1}", x, y);
				//tmp_pixel = tmp + (((y + 1) * tmp_stride) + x + 1) * 2;
				//*tmp_pixel = 200;
				return new Outline () { Path = new List<Direction> () };
				//throw new InvalidOperationException ("Initial point not on outline.");
			}
			
			int i = 0;
			do {
				i++;
				if (i > 1000)
					break;
				
				src_pixel = src + (y * src_stride) + x;				
				tmp_pixel = tmp + (((y + 1) * tmp_stride) + x + 1) * 2;
				
				pixel_group_case = GetPixelGroupCase (src_pixel, src_stride);
				
				//TODO prevent this running of the edge of the image, and causing a pointer error.
				switch (pixel_group_case) {
					case  1:
						direction = Direction.Up;
						tmp_pixel [1] = erase_value;
						tmp_pixel [tmp_stride] = erase_value;
						tmp_pixel [tmp_stride + 1] = erase_value;
						break;
					
					case  2:
						direction = Direction.Right;
						tmp_pixel [2] = erase_value;
						tmp_pixel [tmp_stride + 2] = erase_value;
						tmp_pixel [tmp_stride + 3] = erase_value;
						break;
					
					case  3:
						direction = Direction.Right;
						tmp_pixel [tmp_stride] = erase_value;
						tmp_pixel [tmp_stride + 1] = erase_value;
						tmp_pixel [tmp_stride + 2] = erase_value;
						tmp_pixel [tmp_stride + 3] = erase_value;
						break;
					
					case  4:
						direction = Direction.Left;
						tmp_pixel [tmp_stride * 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 1] = erase_value;
						tmp_pixel [tmp_stride * 3 + 1] = erase_value;
						break;
					
					case  5:
						direction = Direction.Up;
						tmp_pixel [1] = erase_value;
						tmp_pixel [tmp_stride + 1] = erase_value;
						tmp_pixel [tmp_stride * 2 + 1] = erase_value;
						tmp_pixel [tmp_stride * 3 + 1] = erase_value;
						break;
					
					case  6:
						// Ambiguous case - the direction depends on the value
						// of the previous pixel group. This prevents cycles
						// from occuring. Always treat this as closed.
						direction = (previous == Direction.Up) ? Direction.Right
						                                       : Direction.Left;
					
						//TODO pixel erasing is kinda complex...
						//TODO On 1 px thick lines only erase corner pixels, after second extract.
						if (previous == Direction.Down || previous == Direction.Left) {
							tmp_pixel [2] = erase_value;							
							tmp_pixel [tmp_stride * 2 + 1] = erase_value;
							
							//if (tmp_pixel [tmp_stride * 3 + 2] == (byte) PixelType.Opaque
						    //	&& tmp_pixel [tmp_stride] == (byte) PixelType.Opaque) {
									//fixme
									//tmp_pixel [tmp_stride + 2] = erase_value;
									//tmp_pixel [tmp_stride * 2] = erase_value;
							//}
						} else {
							
							tmp_pixel [tmp_stride + 3] = erase_value;							
							tmp_pixel [tmp_stride * 3 + 1] = erase_value;
						
							//if (tmp_pixel [1] == (byte) PixelType.Opaque
						    //	&& tmp_pixel [tmp_stride * 2 + 3] == (byte) PixelType.Opaque) {
									//fixme
									//tmp_pixel [tmp_stride + 2] = erase_value;
									//tmp_pixel [tmp_stride * 2 + 1] = erase_value;
							//}
						}
					
						//Console.WriteLine ("Case 6. previous direction: " + previous + " next: " + direction);
					
						break;
					
					case  7:
						direction = Direction.Right;
						tmp_pixel [tmp_stride + 1] = erase_value;
						tmp_pixel [tmp_stride + 2] = erase_value;
						tmp_pixel [tmp_stride + 3] = erase_value;
						tmp_pixel [tmp_stride * 2 + 1] = erase_value;
						tmp_pixel [tmp_stride * 3 + 1] = erase_value;
						break;
					
					case  8:
						direction = Direction.Down;
						tmp_pixel [tmp_stride * 2 + 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 3] = erase_value;
						tmp_pixel [tmp_stride * 3 + 3] = erase_value;
						break;
					
					case  9:
						// Ambiguous case - the direction depends on the value
						// of the previous pixel group. This prevents cycles
						// from occuring. Always treat this as closed.		
						direction = (previous == Direction.Right) ? Direction.Down
						                                          : Direction.Up;
					
						//TODO pixel erasing is kinda complex...
						//TODO On 1 px thick lines only erase corner pixels, after second extract.
						if (previous == Direction.Up || previous == Direction.Left) {
							tmp_pixel [tmp_stride] = erase_value;
							tmp_pixel [tmp_stride + 1] = erase_value;
							tmp_pixel [tmp_stride * 2 + 2] = erase_value;
							tmp_pixel [tmp_stride * 3 + 2] = erase_value;
						} else {
							tmp_pixel [1] = erase_value;
							tmp_pixel [tmp_stride + 1] = erase_value;
							tmp_pixel [tmp_stride * 2 + 2] = erase_value;
							tmp_pixel [tmp_stride * 2 + 3] = erase_value;
						}
					
						//Console.WriteLine ("Case 9. previous direction: " + previous + " next: " + direction);
					
						break;
					
					case 10:
						direction = Direction.Down;
						tmp_pixel [2] = erase_value;
						tmp_pixel [tmp_stride + 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 2] = erase_value;
						tmp_pixel [tmp_stride * 3 + 2] = erase_value;
						break;
					
					case 11:
						direction = Direction.Down;
						tmp_pixel [tmp_stride] = erase_value;
						tmp_pixel [tmp_stride + 1] = erase_value;
						tmp_pixel [tmp_stride + 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 2] = erase_value;
						tmp_pixel [tmp_stride * 3 + 2] = erase_value;
						break;
					
					case 12:
						direction = Direction.Left;
						tmp_pixel [tmp_stride * 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 1] = erase_value;
						tmp_pixel [tmp_stride * 2 + 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 3] = erase_value;
						break;
					
					case 13:
						direction = Direction.Up;
						tmp_pixel [1] = erase_value;
						tmp_pixel [tmp_stride + 1] = erase_value;
						tmp_pixel [tmp_stride * 2 + 1] = erase_value;
						tmp_pixel [tmp_stride * 2 + 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 3] = erase_value;
						break;
					
					case 14:
						direction = Direction.Left;
						tmp_pixel [2] = erase_value;
						tmp_pixel [tmp_stride + 2] = erase_value;
						tmp_pixel [tmp_stride * 2] = erase_value;
						tmp_pixel [tmp_stride * 2 + 1] = erase_value;
						tmp_pixel [tmp_stride * 2 + 2] = erase_value;
						break;
					
					default:
						throw new InvalidOperationException ("Unexpected pixel group case.");
				}
				
				//Console.WriteLine (pixel_group_case + ": " + direction);
				
				path.Add (direction);
				previous = direction;
				
				if (direction == Direction.Up)
					y--;
				
				else if (direction == Direction.Right)
					x++;
				
				else if (direction == Direction.Left)
					x--;
				
				else if (direction == Direction.Down)
					y++;		
				
				// Keep track of overall bounds.
				minX = Math.Min (x, minX);
				maxX = Math.Max (x, maxX);
				minY = Math.Min (y, minY);
				maxY = Math.Max (y, maxY);				
				
			} while (!(x == initialX && y == initialY)); // Stop once initial point is reached again.
			
			return new Outline () {
				InitialPoint = new PointInt () {
					X = initialX - 1,
					Y = initialY - 1 }, // Intial point in original image not tmp image.
				Path = path,
				Bounds = new RectInt () {
					X = minX,
					Y = minY,
					Width = maxX - minX,
					Height = maxY - minY }
			};
		}
				
	}
}
