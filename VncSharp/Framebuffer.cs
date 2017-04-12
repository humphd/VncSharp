// VncSharp - .NET VNC Client Library
// Copyright (C) 2008 David Humphrey
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Drawing;
// ReSharper disable ArrangeAccessorOwnerBody

namespace VncSharp
{
	/// <summary>
	/// Properties of a VNC Framebuffer, and its Pixel Format.
	/// </summary>
	public class Framebuffer
	{
	    private string name;

	    private readonly int[] pixels;   // I'm reusing the same pixel buffer for all update rectangles.
                                         // Pixel values will always be 32-bits to match GDI representation


        /// <summary>
        /// Creates a new Framebuffer with (width x height) pixels.
        /// </summary>
        /// <param name="width">The width in pixels of the remote desktop.</param>
        /// <param name="height">The height in pixels of the remote desktop.</param>
        private Framebuffer(int width, int height)
		{
		    Width = width;
			Height = height;

            // Cache the total size of the pixel array and initialize
            // The total number of pixels (w x h) assigned in SetSize()
            var pixelCount = width * height;
			pixels = new int[pixelCount];
		}

		/// <summary>
		/// An indexer to allow access to the internal pixel buffer.
		/// </summary>
		public int this[int index] {
			get {
				return pixels[index];
			}
			set {
				pixels[index] = value;				
			}
		}

		/// <summary>
		/// The Width of the Framebuffer, measured in Pixels.
		/// </summary>
		public int Width { get; }

	    /// <summary>
		/// The Height of the Framebuffer, measured in Pixels.
		/// </summary>
		public int Height { get; }

	    /// <summary>
		/// Gets a Rectangle object constructed out of the Width and Height for the Framebuffer.  Used as a convenience in other classes.
		/// </summary>
		public Rectangle Rectangle
	    {
	        get { return new Rectangle(0, 0, Width, Height); }
	    }

	    /// <summary>
		/// The number of Bits Per Pixel for the Framebuffer--one of 8, 24, or 32.
		/// </summary>
		public int BitsPerPixel { get; private set; }

	    /// <summary>
		/// The Colour Depth of the Framebuffer.
		/// </summary>
	    private int Depth { get; set; }

	    /// <summary>
		/// Indicates whether the remote host uses Big- or Little-Endian order when sending multi-byte values.
		/// </summary>
	    private bool BigEndian { get; set; }

	    /// <summary>
		/// Indicates whether the remote host supports True Colour.
		/// </summary>
	    private bool TrueColour { get; set; }

	    /// <summary>
		/// The maximum value for Red in a pixel's colour value.
		/// </summary>
		public int RedMax { get; private set; }

	    /// <summary>
		/// The maximum value for Green in a pixel's colour value.
		/// </summary>
		public int GreenMax { get; private set; }

	    /// <summary>
		/// The maximum value for Blue in a pixel's colour value.
		/// </summary>
		public int BlueMax { get; private set; }

	    /// <summary>
		/// The number of bits to shift pixel values in order to obtain Red values.
		/// </summary>
		public int RedShift { get; private set; }

	    /// <summary>
		/// The number of bits to shift pixel values in order to obtain Green values.
		/// </summary>
		public int GreenShift { get; private set; }

	    /// <summary>
		/// The number of bits to shift pixel values in order to obtain Blue values.
		/// </summary>
		public int BlueShift { get; private set; }

	    /// <summary>
		/// The name of the remote destkop, if any.  Must be non-null.
		/// </summary>
		/// <exception cref="ArgumentNullException">Thrown if a null string is used when setting DesktopName.</exception>
		public string DesktopName {
			get {
				return name;
			}
			set {
				if (value == null)
					throw new ArgumentNullException($"DesktopName");
				name = value;
			}
		}

		/// <summary>
		/// When communicating with the VNC Server, bytes are used to represent many of the values above.  However, internally it is easier to use Integers.  This method provides a translation between the two worlds.
		/// </summary>
		/// <returns>A byte array of 16 bytes containing the properties of the framebuffer in a format ready for transmission to the VNC server.</returns>
		public byte[] ToPixelFormat()
		{
			var b = new byte[16];
			
			b[0]  = (byte) BitsPerPixel;
			b[1]  = (byte) Depth;
			b[2]  = (byte) (BigEndian ? 1 : 0);
			b[3]  = (byte) (TrueColour ? 1 : 0);
			b[4]  = (byte) ((RedMax >> 8) & 0xff);
			b[5]  = (byte) (RedMax & 0xff);
			b[6]  = (byte) ((GreenMax >> 8) & 0xff);
			b[7]  = (byte) (GreenMax & 0xff);
			b[8]  = (byte) ((BlueMax >> 8) & 0xff);
			b[9]  = (byte) (BlueMax & 0xff);
			b[10] = (byte) RedShift;
			b[11] = (byte) GreenShift;
			b[12] = (byte) BlueShift;
			// plus 3 bytes padding = 16 bytes
			
			return b;
		}
		
		/// <summary>
		/// Given the dimensions and 16-byte PIXEL_FORMAT record from the VNC Host, deserialize this into a Framebuffer object.
		/// </summary>
		/// <param name="b">The 16-byte PIXEL_FORMAT record.</param>
		/// <param name="width">The width in pixels of the remote desktop.</param>
		/// <param name="height">The height in pixles of the remote desktop.</param>
		/// <returns>Returns a Framebuffer object matching the specification of b[].</returns>
		public static Framebuffer FromPixelFormat(byte[] b, int width, int height)
		{
			if (b.Length != 16)
				throw new ArgumentException("Length of b must be 16 bytes.");

		    var buffer = new Framebuffer(width, height)
		    {
		        BitsPerPixel = b[0],
		        Depth = b[1],
		        BigEndian = b[2] != 0,
		        TrueColour = b[3] != 0,
		        RedMax = b[5] | b[4] << 8,
		        GreenMax = b[7] | b[6] << 8,
		        BlueMax = b[9] | b[8] << 8,
		        RedShift = b[10],
		        GreenShift = b[11],
		        BlueShift = b[12]
		    };

		    // Last 3 bytes are padding, ignore									

			return buffer;
		}
	}
}
