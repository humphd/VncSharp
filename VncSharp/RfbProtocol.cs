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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VncSharp.zlib.NET;
// ReSharper disable ArrangeAccessorOwnerBody

namespace VncSharp
{
	/// <summary>
	/// Contains methods and properties to handle all aspects of the RFB Protocol versions 3.3 - 3.8.
	/// </summary>
	public class RfbProtocol
	{
		#region Constants
		// ReSharper disable InconsistentNaming
		// Version Constants
		private const string RFB_VERSION_ZERO			= "RFB 000.000\n";

		// Timeout Constants
		public const int RECEIVE_TIMEOUT				= 15000;
		public const int SEND_TIMEOUT					= 15000;
		
		// Encoding Constants
		public const int RAW_ENCODING 					= 0;
		public const int COPYRECT_ENCODING 				= 1;
		public const int RRE_ENCODING 					= 2;
		public const int CORRE_ENCODING					= 4;
		public const int HEXTILE_ENCODING 				= 5;
		public const int ZRLE_ENCODING 					= 16;

		// Server to Client Message-Type constants
		public const int FRAMEBUFFER_UPDATE 			= 0;
		public const int SET_COLOUR_MAP_ENTRIES			= 1;
		public const int BELL 							= 2;
		public const int SERVER_CUT_TEXT 				= 3;

		// Client to Server Message-Type constants
		private const byte SET_PIXEL_FORMAT 			= 0;
		private const byte SET_ENCODINGS 				= 2;
		private const byte FRAMEBUFFER_UPDATE_REQUEST 	= 3;
		private const byte KEY_EVENT 					= 4;
		private const byte POINTER_EVENT 				= 5;
		private const byte CLIENT_CUT_TEXT 				= 6;

        // Keyboard constants
		public const int XK_BackSpace 	= 0xFF08;
		public const int XK_Tab 		= 0xFF09;
		public const int XK_Clear 		= 0xFF0B;
		public const int XK_Return 		= 0xFF0D;
		public const int XK_Pause 		= 0xFF13;
		public const int XK_Sys_Req 	= 0xFF15;
		public const int XK_Escape 		= 0xFF1B;
		public const int XK_Home 		= 0xFF50;
		public const int XK_Left 		= 0xFF51;
		public const int XK_Up 			= 0xFF52;
		public const int XK_Right 		= 0xFF53;
		public const int XK_Down 		= 0xFF54;
		public const int XK_Prior 		= 0xFF55;
		public const int XK_Next 		= 0xFF56;
		public const int XK_End 		= 0xFF57;
		public const int XK_Select 		= 0xFF60;
		public const int XK_Print 		= 0xFF61;
		public const int XK_Execute 	= 0xFF62;
		public const int XK_Insert 		= 0xFF63;
		public const int XK_Menu 		= 0xFF67;
		public const int XK_Cancel 		= 0xFF69;
		public const int XK_Help 		= 0xFF6A;
		public const int XK_Break 		= 0xFF6B;
		public const int XK_F1 			= 0xFFBE;
		public const int XK_F2 			= 0xFFBF;
		public const int XK_F3 			= 0xFFC0;
		public const int XK_F4 			= 0xFFC1;
		public const int XK_F5 			= 0xFFC2;
		public const int XK_F6 			= 0xFFC3;
		public const int XK_F7 			= 0xFFC4;
		public const int XK_F8 			= 0xFFC5;
		public const int XK_F9 			= 0xFFC6;
		public const int XK_F10 		= 0xFFC7;
		public const int XK_F11 		= 0xFFC8;
		public const int XK_F12 		= 0xFFC9;
		public const int XK_Shift_L 	= 0xFFE1;
		public const int XK_Shift_R 	= 0xFFE2;
		public const int XK_Control_L 	= 0xFFE3;
		public const int XK_Control_R 	= 0xFFE4;
		public const int XK_Meta_L 		= 0xFFE7;
		public const int XK_Meta_R 		= 0xFFE8;
		public const int XK_Alt_L 		= 0xFFE9;
		public const int XK_Alt_R 		= 0xFFEA;
		public const int XK_Super_L 	= 0xFFEB;
		public const int XK_Super_R 	= 0xFFEC;
		public const int XK_Hyper_L 	= 0xFFED;
		public const int XK_Hyper_R 	= 0xFFEE;
		public const int XK_Delete 		= 0xFFFF;

		// ReSharper restore InconsistentNaming
		#endregion

		private int verMajor;	// Major version of Protocol--probably 3
		private int verMinor;	// Minor version of Protocol--probably 3, 7, or 8

		private TcpClient tcp;			// Network object used to communicate with host
		private NetworkStream stream;	// Stream object used to send/receive data
		private BinaryWriter writer;	// sent and received, so these handle this.

		/// <summary>
		/// Gets the Protocol Version of the remote VNC Host--probably 3.3, 3.7, or 3.8.
		/// </summary>
		public float ServerVersion
		{
			get { return verMajor + verMinor * 0.1f; }
		}

		/// <summary>
		/// Gets or sets the proxy identifier to be send when using UltraVNC's repeater functionality
		/// </summary>
		/// <value>
		/// The proxy identifier.
		/// </value>
		// ReSharper disable once UnusedAutoPropertyAccessor.Local
		private int ProxyID { get; set; }

		public BinaryReader Reader { get; private set; }

		public ZRLECompressedReader ZrleReader { get; private set; }

	    /// <summary>
		/// Attempt to connect to a remote VNC Host.
		/// </summary>
		/// <param name="host">The IP Address or Host Name of the VNC Host.</param>
		/// <param name="port">The Port number on which to connect.  Usually this will be 5900, except in the case that the VNC Host is running on a different Display, in which case the Display number should be added to 5900 to determine the correct port.</param>
		public void Connect(string host, int port)
		{
			if (host == null) throw new ArgumentNullException(nameof(host));

			// Try to connect, passing any exceptions up to the caller, and if successful, 
			// wrap a big endian Binary Reader and Binary Writer around the resulting stream.
			tcp = new TcpClient {NoDelay = true}; // turn-off Nagle's Algorithm for better interactive performance with host.

			tcp.ReceiveTimeout = RECEIVE_TIMEOUT; // set receive timeout (15s default)
			tcp.SendTimeout = SEND_TIMEOUT;    // set send timeout to (15s default)
			tcp.Connect(host, port);
			stream = tcp.GetStream();

			stream.ReadTimeout = RECEIVE_TIMEOUT; // set read timeout to (15s default)
			stream.WriteTimeout = SEND_TIMEOUT;// set write timeout to (15s default)

			// Most of the RFB protocol uses Big-Endian byte order, while
			// .NET uses Little-Endian. These wrappers convert between the
			// two.  See BigEndianReader and BigEndianWriter below for more details.
			Reader = new BigEndianBinaryReader(stream);
			writer = new BigEndianBinaryWriter(stream);
			ZrleReader = new ZRLECompressedReader(stream);
		}

		/// <summary>
		/// Closes the connection to the remote host.
		/// </summary>
		public void Close()
		{
			try {
				writer.Close();
				Reader.Close();
				stream.Close();
				tcp.Close();
			} catch (Exception ex) {
				Debug.Fail(ex.Message);
			}
		}
		
		/// <summary>
		/// Reads VNC Host Protocol Version message (see RFB Doc v. 3.8 section 6.1.1)
		/// </summary>
		/// <exception cref="NotSupportedException">Thrown if the version of the host is not known or supported.</exception>
		public void ReadProtocolVersion()
		{
			var b = Reader.ReadBytes(12);
			string a = "";
			string s = "";
			for (int x = 0; x < 12; x++)
				a += $"{b[x]} ";
			for (int x = 0; x < 12; x++)
				s += $"{Convert.ToChar(b[x])} ";

			// As of the time of writing, the only supported versions are 3.3, 3.7, and 3.8.
			if (Encoding.ASCII.GetString(b) == RFB_VERSION_ZERO) // Repeater functionality
			{
				verMajor = 0;
				verMinor = 0;
			}
			// Added support for RealVNC 4.001 (Linux VNC Server) - Supports 3.8 Protocol
			else if (b[0] == 82 &&		// R
					 b[1] == 70 &&		// F
					 b[2] == 66 &&		// B
					 b[3] == 32 &&		// (space)
					 b[4] == 48 &&		// 0
					 b[5] == 48 &&		// 0
					 b[6] == 52 &&		// 4
					 b[7] == 46 &&		// .
					 b[8] == 48 &&		// 0
					 b[9] == 48 &&		// 0
					 b[10] == 49 &&		// 1
					 b[11] == 10)		// \n
			{
				verMajor = 3;
				verMinor = 8;
			}
			else if ( 
				b[0]  == 0x52 &&					// R
				b[1]  == 0x46 &&					// F
				b[2]  == 0x42 &&					// B
				b[3]  == 0x20 &&					// (space)
				b[4]  == 0x30 &&					// 0
				b[5]  == 0x30 &&					// 0
				b[6]  == 0x33 &&					// 3
				b[7]  == 0x2e &&					// .
				(b[8]  == 0x30 ||					// 0
				 b[8]  == 0x38) &&					// BUG FIX: Apple reports 8 
				(b[9] == 0x30 ||					// 0
				 b[9] == 0x38) &&					// BUG FIX: Apple reports 8 
				(b[10] == 0x33 ||					// 3, 7, OR 8 are all valid and possible
				 b[10] == 0x36 ||				 	// BUG FIX: UltraVNC reports protocol version 3.6!
				 b[10] == 0x37 ||					 
				 b[10] == 0x38 ||					 
				 b[10] == 0x39) &&					// BUG FIX: Apple reports 9					
				b[11] == 0x0a)						// \n
			{
				// Since we only currently support the 3.x protocols, this can be assumed here.
				// If and when 4.x comes out, this will need to be fixed--however, the entire 
				// protocol will need to be updated then anyway :)
				verMajor = 3;

				// Figure out which version of the protocol this is:
				// ReSharper disable once SwitchStatementMissingSomeCases
				switch (b[10]) {
					case 0x33: 
					case 0x36:	// BUG FIX: pass 3.3 for 3.6 to allow UltraVNC to work, thanks to Steve Bostedor.
						verMinor = 3;
						break;
					case 0x37:
						verMinor = 7;
						break;
					case 0x38:
						verMinor = 8;
						break;
					case 0x39:  // BUG FIX: Apple reports 3.889
						// According to the RealVNC mailing list, Apple is really using 3.3 
						// (see http://www.mail-archive.com/vnc-list@realvnc.com/msg23615.html).  I've tested with
						// both 3.3 and 3.8, and they both seem to work (I obviously haven't hit the issues others have).
						// Because 3.8 seems to work, I'm leaving that, but it might be necessary to use 3.3 in future.
						verMinor = 8;
						break;
				}
			} else {
				throw new NotSupportedException($"Only versions 3.3, 3.7, and 3.8 of the RFB Protocol are supported.\r\n{a}\r\n{s}");
			}
		}

		/// <summary>
		/// Send the Protocol Version supported by the client.  Will be highest supported by server (see RFB Doc v. 3.8 section 6.1.1).
		/// </summary>
		public void WriteProtocolVersion()
		{
			// We will use which ever version the server understands, be it 3.3, 3.7, or 3.8.
			Debug.Assert(verMinor == 3 || verMinor == 7 || verMinor == 8, "Wrong Protocol Version!",
				$"Protocol Version should be 3.3, 3.7, or 3.8 but is {verMajor}.{verMinor}");

			writer.Write(GetBytes($"RFB 003.00{verMinor}\n"));
			writer.Flush();
		}

		/// <summary>
		/// Send the Target Proxy address, needs to be 250 bytes
		/// </summary>
		public void WriteProxyAddress()
		{
			var proxyMessage = new byte[250];
			GetBytes("ID:" + ProxyID + "\n").CopyTo(proxyMessage, 0);
			writer.Write(proxyMessage);
			writer.Flush();
		}

		/// <summary>
		/// Determine the type(s) of authentication that the server supports. See RFB Doc v. 3.8 section 6.1.2.
		/// </summary>
		/// <returns>An array of bytes containing the supported Security Type(s) of the server.</returns>
		public byte[] ReadSecurityTypes()
		{
			// Read and return the types of security supported by the server (see protocol doc 6.1.2)
			byte[] types;
			
			// Protocol Version 3.7 onward supports multiple security types, while 3.3 only 1
			if (verMinor == 3) {
				types = new[] { (byte) Reader.ReadUInt32() };
			} else {
				var num = Reader.ReadByte();
				types = new byte[num];
				
				for (var i = 0; i < num; ++i) {
					types[i] = Reader.ReadByte();
				}
			}
			return types;
		}

		/// <summary>
		/// If the server has rejected the connection during Authentication, a reason is given. See RFB Doc v. 3.8 section 6.1.2.
		/// </summary>
		/// <returns>Returns a string containing the reason for the server rejecting the connection.</returns>
		public string ReadSecurityFailureReason()
		{
			var length = (int) Reader.ReadUInt32();
			return GetString(Reader.ReadBytes(length));
		}

		/// <summary>
		/// Indicate to the server which type of authentication will be used. See RFB Doc v. 3.8 section 6.1.2.
		/// </summary>
		/// <param name="type">The type of Authentication to be used, 1 (None) or 2 (VNC Authentication).</param>
		public void WriteSecurityType(byte type)
		{
			Debug.Assert(type >= 1, "Wrong Security Type", "The Security Type must be one that requires authentication.");
			
			// Only bother writing this byte if the version of the server is 3.7
			if (verMinor < 7) return;
			writer.Write(type);
			writer.Flush();
		}

		/// <summary>
		/// When the server uses Security Type 2 (i.e., VNC Authentication), a Challenge/Response 
		/// mechanism is used to authenticate the user. See RFB Doc v. 3.8 section 6.1.2 and 6.2.2.
		/// </summary>
		/// <returns>Returns the 16 byte Challenge sent by the server.</returns>
		public byte[] ReadSecurityChallenge()
		{
			return Reader.ReadBytes(16);
		}

		/// <summary>
		/// Sends the encrypted Response back to the server. See RFB Doc v. 3.8 section 6.1.2.
		/// </summary>
		/// <param name="response">The DES password encrypted challege sent by the server.</param>
		public void WriteSecurityResponse(byte[] response)
		{
			writer.Write(response, 0, response.Length);
			writer.Flush();
		}

		/// <summary>
		/// When the server uses VNC Authentication, after the Challege/Response, the server
		/// sends a status code to indicate whether authentication worked. See RFB Doc v. 3.8 section 6.1.3.
		/// </summary>
		/// <returns>An integer indicating the status of authentication: 0 = OK; 1 = Failed; 2 = Too Many (deprecated).</returns>
		public uint ReadSecurityResult()
		{
			return Reader.ReadUInt32();
		}

		/// <summary>
		/// Sends an Initialisation message to the server. See RFB Doc v. 3.8 section 6.1.4.
		/// </summary>
		/// <param name="shared">True if the server should allow other clients to connect, otherwise False.</param>
		public void WriteClientInitialisation(bool shared)
		{
			// Non-zero if TRUE, zero if FALSE
			writer.Write((byte)(shared ? 1 : 0));
			writer.Flush();
		}
		
		/// <summary>
		/// Reads the server's Initialization message, specifically the remote Framebuffer's properties. See RFB Doc v. 3.8 section 6.1.5.
		/// </summary>
		/// <returns>Returns a Framebuffer object representing the geometry and properties of the remote host.</returns>
		public Framebuffer ReadServerInit(int bitsPerPixel, int depth)
		{
			int w = Reader.ReadUInt16();
			int h = Reader.ReadUInt16();
			var buffer = Framebuffer.FromPixelFormat(Reader.ReadBytes(16), w, h, bitsPerPixel, depth);
			var length = (int) Reader.ReadUInt32();

			buffer.DesktopName = GetString(Reader.ReadBytes(length));
			
			return buffer;
		}
		
		/// <summary>
		/// Sends the format to be used by the server when sending Framebuffer Updates. See RFB Doc v. 3.8 section 6.3.1.
		/// </summary>
		/// <param name="buffer">A Framebuffer telling the server how to encode pixel data. Typically this will be the same one sent by the server during initialization.</param>
		public void WriteSetPixelFormat(Framebuffer buffer)
		{
			byte[] buff = new byte[20];

			// Build up the packet
			buff[0] = SET_PIXEL_FORMAT;
			buff[1] = 0x00;
			buff[2] = 0x00;
			buff[3] = 0x00;
			buffer.ToPixelFormat().CopyTo(buff, 4);		// 16-byte Pixel Format
			writer.Write(buff);
			writer.Flush();
		}

		/// <summary>
		/// Tell the server which encodings are supported by the client. See RFB Doc v. 3.8 section 6.3.3.
		/// </summary>
		/// <param name="encodings">An array of integers indicating the encoding types supported.  The order indicates preference, where the first item is the first preferred.</param>
		public void WriteSetEncodings(uint[] encodings)
		{
			byte[] buff = new byte[(encodings.Length*4) + 4];
			int x = 0;
			buff[0] = SET_ENCODINGS;
			buff[1] = 0x00;
			BitConverter.GetBytes((ushort)encodings.Length).Reverse().ToArray().CopyTo(buff, 2);
			
			foreach (var t in encodings)
			{	
				BitConverter.GetBytes(t).Reverse().ToArray().CopyTo(buff, 4+x);
				x+=4;
			}
			writer.Write(buff);
			writer.Flush();
		}

		/// <summary>
		/// Sends a request for an update of the area specified by (x, y, w, h). See RFB Doc v. 3.8 section 6.3.4.
		/// </summary>
		/// <param name="x">The x-position of the area to be updated.</param>
		/// <param name="y">The y-position of the area to be updated.</param>
		/// <param name="width">The width of the area to be updated.</param>
		/// <param name="height">The height of the area to be updated.</param>
		/// <param name="incremental">Indicates whether only changes to the client's data should be sent or the entire desktop.</param>
		public void WriteFramebufferUpdateRequest(ushort x, ushort y, ushort width, ushort height, bool incremental)
		{
			byte[] buff = new byte[10];
			buff[0] = FRAMEBUFFER_UPDATE_REQUEST;
			buff[1] = (byte)(incremental ? 1 : 0);
			BitConverter.GetBytes((ushort)x).Reverse().ToArray().CopyTo(buff, 2);
			BitConverter.GetBytes((ushort)y).Reverse().ToArray().CopyTo(buff, 4);
			BitConverter.GetBytes((ushort)width).Reverse().ToArray().CopyTo(buff, 6);
			BitConverter.GetBytes((ushort)height).Reverse().ToArray().CopyTo(buff, 8);
			writer.Write(buff);
			writer.Flush();
		}

		/// <summary>
		/// Sends a key press or release to the server. See RFB Doc v. 3.8 section 6.3.5.
		/// </summary>
		/// <param name="keysym">The value of the key pressed, expressed using X Window "keysym" values.</param>
		/// <param name="pressed"></param>
		public void WriteKeyEvent(uint keysym, bool pressed)
		{
			byte[] buff = new byte[8];
			buff[0] = KEY_EVENT;
			buff[1] = (byte)(pressed ? 1 : 0);
			buff[2] = 0x00;
			buff[3] = 0x00;
			BitConverter.GetBytes(keysym).Reverse().ToArray().CopyTo(buff, 4);
			writer.Write(buff);
			writer.Flush();
		}

		/// <summary>
		/// Sends a mouse movement or button press/release to the server. See RFB Doc v. 3.8 section 6.3.6.
		/// </summary>
		/// <param name="buttonMask">A bitmask indicating which button(s) are pressed.</param>
		/// <param name="point">The location of the mouse cursor.</param>
		public void WritePointerEvent(byte buttonMask, Point point)
		{
			byte[] buff = new byte[6];
			buff[0] = POINTER_EVENT;
			buff[1] = buttonMask;
			BitConverter.GetBytes((ushort)point.X).Reverse().ToArray().CopyTo(buff, 2);
			BitConverter.GetBytes((ushort)point.Y).Reverse().ToArray().CopyTo(buff, 4);
			writer.Write(buff);
			writer.Flush();
		}

		/// <summary>
		/// Sends text in the client's Cut Buffer to the server. See RFB Doc v. 3.8 section 6.3.7.
		/// </summary>
		/// <param name="text">The text to be sent to the server.</param>
		public void WriteClientCutText(string text)
		{
			byte[] buff = new byte[text.Length + 8];
			buff[0] = CLIENT_CUT_TEXT;
			buff[1] = 0x00;
			buff[2] = 0x00;
			buff[3] = 0x00;
			BitConverter.GetBytes((uint)text.Length).Reverse().ToArray().CopyTo(buff, 4);
			GetBytes(text).CopyTo(buff, 8);
			writer.Write(buff);
			writer.Flush();
		}

		/// <summary>
		/// Reads the type of message being sent by the server--all messages are prefixed with a message type.
		/// </summary>
		/// <returns>Returns the message type as an integer.</returns>
		public int ReadServerMessageType()
		{
			return Reader.ReadByte();
		}

		/// <summary>
		/// Reads the number of update rectangles being sent by the server. See RFB Doc v. 3.8 section 6.4.1.
		/// </summary>
		/// <returns>Returns the number of rectangles that follow.</returns>
		public int ReadFramebufferUpdate()
		{
			ReadPadding(1);
			return Reader.ReadUInt16();
		}

		/// <summary>
		/// Reads a rectangle's header information, including its encoding. See RFB Doc v. 3.8 section 6.4.1.
		/// </summary>
		/// <param name="rectangle">The geometry of the rectangle that is about to be sent.</param>
		/// <param name="encoding">The encoding used for this rectangle.</param>
		public void ReadFramebufferUpdateRectHeader(out Rectangle rectangle, out int encoding)
		{
			rectangle = new Rectangle
			{
				X = Reader.ReadUInt16(),
				Y = Reader.ReadUInt16(),
				Width = Reader.ReadUInt16(),
				Height = Reader.ReadUInt16()
			};
			encoding = (int) Reader.ReadUInt32();
		}
		
		// TODO: this colour map code should probably go in Framebuffer.cs
		public ushort[,] MapEntries { get; } = new ushort[256, 3];

		/// <summary>
		/// Reads 8-bit RGB colour values (or updated values) into the colour map.  See RFB Doc v. 3.8 section 6.5.2.
		/// </summary>
		public void ReadColourMapEntry()
		{
			ReadPadding(1);
			var firstColor = ReadUInt16();
			var nbColors = ReadUInt16();

			for (var i = 0; i < nbColors; i++, firstColor++)
			{
				MapEntries[firstColor, 0] = (byte)(ReadUInt16() * byte.MaxValue / ushort.MaxValue);	// R
				MapEntries[firstColor, 1] = (byte)(ReadUInt16() * byte.MaxValue / ushort.MaxValue);	// G
				MapEntries[firstColor, 2] = (byte)(ReadUInt16() * byte.MaxValue / ushort.MaxValue);	// B
			}
		} 

		/// <summary>
		/// Reads the text from the Cut Buffer on the server. See RFB Doc v. 3.8 section 6.4.4.
		/// </summary>
		/// <returns>Returns the text in the server's Cut Buffer.</returns>
		public string ReadServerCutText()
		{
			ReadPadding(3);
			var length = (int) Reader.ReadUInt32();
			return GetString(Reader.ReadBytes(length));
		}

		// ---------------------------------------------------------------------------------------
		// Here's all the "low-level" protocol stuff so user objects can access the data directly

		/// <summary>
		/// Reads a single UInt32 value from the server, taking care of Big- to Little-Endian conversion.
		/// </summary>
		/// <returns>Returns a UInt32 value.</returns>
		public uint ReadUint32()
		{
			return Reader.ReadUInt32(); 
		}
		
		/// <summary>
		/// Reads a single UInt16 value from the server, taking care of Big- to Little-Endian conversion.
		/// </summary>
		/// <returns>Returns a UInt16 value.</returns>
		public ushort ReadUInt16()
		{
			return Reader.ReadUInt16(); 
		}
		
		/// <summary>
		/// Reads a single Byte value from the server.
		/// </summary>
		/// <returns>Returns a Byte value.</returns>
		public byte ReadByte()
		{
			return Reader.ReadByte();
		}
		
		/// <summary>
		/// Reads the specified number of bytes from the server, taking care of Big- to Little-Endian conversion.
		/// </summary>
		/// <param name="count">The number of bytes to be read.</param>
		/// <returns>Returns a Byte Array containing the values read.</returns>
		public byte[] ReadBytes(int count)
		{
			return Reader.ReadBytes(count);
		}

		/// <summary>
		/// Writes a single UInt32 value to the server, taking care of Little- to Big-Endian conversion.
		/// </summary>
		/// <param name="value">The UInt32 value to be written.</param>
		public void WriteUint32(uint value)
		{
			writer.Write(value);
		}

		/// <summary>
		/// Writes a single UInt16 value to the server, taking care of Little- to Big-Endian conversion.
		/// </summary>
		/// <param name="value">The UInt16 value to be written.</param>
		public void WriteUInt16(ushort value)
		{
			writer.Write(value);
		}
		
		/// <summary>
		/// Writes a single Byte value to the server.
		/// </summary>
		/// <param name="value">The UInt32 value to be written.</param>
		public void WriteByte(byte value)
		{
			writer.Write(value);
		}

		/// <summary>
		/// Reads the specified number of bytes of padding (i.e., garbage bytes) from the server.
		/// </summary>
		/// <param name="length">The number of bytes of padding to read.</param>
		protected void ReadPadding(int length)
		{
			ReadBytes(length);
		}
		
		/// <summary>
		/// Writes the specified number of bytes of padding (i.e., garbage bytes) to the server.
		/// </summary>
		/// <param name="length">The number of bytes of padding to write.</param>
		protected void WritePadding(int length)
		{
			var padding = new byte[length];
			writer.Write(padding, 0, padding.Length);
		}

		/// <summary>
		/// Converts a string to bytes for transfer to the server.
		/// </summary>
		/// <param name="text">The text to be converted to bytes.</param>
		/// <returns>Returns a Byte Array containing the text as bytes.</returns>
		protected static byte[] GetBytes(string text)
		{
			return Encoding.ASCII.GetBytes(text);
		}
		
		/// <summary>
		/// Converts a series of bytes to a string.
		/// </summary>
		/// <param name="bytes">The Array of Bytes to be converted to a string.</param>
		/// <returns>Returns a String representation of bytes.</returns>
		protected static string GetString(byte[] bytes)
		{
			return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
		}

		/// <summary>
		/// BigEndianBinaryReader is a wrapper class used to read .NET integral types from a Big-Endian stream.  It inherits from BinaryReader and adds Big- to Little-Endian conversion.
		/// </summary>
		protected sealed class BigEndianBinaryReader : BinaryReader
		{
			private byte[] buff = new byte[4];

			public BigEndianBinaryReader(Stream input) : base(input)
			{
			}
			
			public BigEndianBinaryReader(Stream input, Encoding encoding) : base(input, encoding)
			{
			}

			// Since this is being used to communicate with an RFB host, only some of the overrides are provided below.
	
			public override ushort ReadUInt16()
			{
				FillBuff(2);
				return (ushort)(buff[1] | (uint)buff[0] << 8);
				
			}
			
			public override short ReadInt16()
			{
				FillBuff(2);
				return (short)(buff[1] & 0xFF | buff[0] << 8);
			}

			public override uint ReadUInt32()
			{
				FillBuff(4);
				return (uint)buff[3] & 0xFF | (uint)buff[2] << 8 | (uint)buff[1] << 16 | (uint)buff[0] << 24;
			}
			
			public override int ReadInt32()
			{
				FillBuff(4);
				return buff[3] | buff[2] << 8 | buff[1] << 16 | buff[0] << 24;
			}

			private void FillBuff(int totalBytes)
			{
				var bytesRead = 0;

				do {
					var n = BaseStream.Read(buff, bytesRead, totalBytes - bytesRead);
					
					if (n == 0)
						throw new IOException("Unable to read next byte(s).");

					bytesRead += n;
				} while (bytesRead < totalBytes);
			}
		}
		

		/// <summary>
		/// BigEndianBinaryWriter is a wrapper class used to write .NET integral types in Big-Endian order to a stream.  It inherits from BinaryWriter and adds Little- to Big-Endian conversion.
		/// </summary>
		protected sealed class BigEndianBinaryWriter : BinaryWriter
		{
			public BigEndianBinaryWriter(Stream input) : base(input)
			{
			}

			public BigEndianBinaryWriter(Stream input, Encoding encoding) : base(input, encoding)
			{
			}
			
			// Flip all little-endian .NET types into big-endian order and send
			public override void Write(ushort value)
			{
				FlipAndWrite(BitConverter.GetBytes(value));
			}
			
			public override void Write(short value)
			{
				FlipAndWrite(BitConverter.GetBytes(value));
			}

			public override void Write(uint value)
			{
				FlipAndWrite(BitConverter.GetBytes(value));
			}
			
			public override void Write(int value)
			{
				FlipAndWrite(BitConverter.GetBytes(value));
			}

			public override void Write(ulong value)
			{
				FlipAndWrite(BitConverter.GetBytes(value));
			}
			
			public override void Write(long value)
			{
				FlipAndWrite(BitConverter.GetBytes(value));
			}

			private void FlipAndWrite(byte[] b)
			{
				// Given an array of bytes, flip and write to underlying stream
				Array.Reverse(b);
				base.Write(b);
			}
		}

		/// <summary>
		/// ZRLE compressed binary reader, used by ZrleRectangle.
		/// </summary>
		public sealed class ZRLECompressedReader : BinaryReader
		{
		    private MemoryStream zlibMemoryStream;
		    private ZOutputStream zlibDecompressedStream;
		    private BinaryReader uncompressedReader;

			public ZRLECompressedReader(Stream uncompressedStream) : base(uncompressedStream)
			{
				zlibMemoryStream = new MemoryStream();
				zlibDecompressedStream = new ZOutputStream(zlibMemoryStream);
				uncompressedReader = new BinaryReader(zlibMemoryStream);
			}

			public override byte ReadByte()
			{
				return uncompressedReader.ReadByte();
			}

			public override byte[] ReadBytes(int count)
			{
				return uncompressedReader.ReadBytes(count);
			}

			public void DecodeStream()
			{
				// Reset position to use same buffer
				zlibMemoryStream.Position = 0;

				// Get compressed stream length to read
				var buff = new byte[4];
				if (BaseStream.Read(buff, 0, 4) != 4)
					throw new Exception("ZRLE decoder: Invalid compressed stream size");

				// BigEndian to LittleEndian conversion
				var compressedBufferSize = buff[3] | buff[2] << 8 | buff[1] << 16 | buff[0] << 24;
				if (compressedBufferSize > 64 * 1024 * 1024)
					throw new Exception("ZRLE decoder: Invalid compressed data size");

				#region Decode stream
				// Decode stream
				// int pos = 0;
				// while (pos++ < compressedBufferSize)
				// 	zlibDecompressedStream.WriteByte(this.BaseStream.ReadByte());
				#endregion

				#region Decode stream in blocks
				// Decode stream in blocks
				var bytesNeeded = compressedBufferSize;
				const int maxBufferSize = 64 * 1024; // 64k buffer
				var receiveBuffer = new byte[maxBufferSize];
				var netStream = (NetworkStream)BaseStream;
				netStream.ReadTimeout = 15000; // Set timeout to 15s
				do
				{
					if (netStream.DataAvailable)
					{
						var bytesToRead = bytesNeeded;

						// the byteToRead should never exceed the maxBufferSize
						if (bytesToRead > maxBufferSize)
							bytesToRead = maxBufferSize;
						
						// try reading bytes (read in 1024 byte chunks) - improvement for slow connections
						int toRead = (bytesToRead > 1024) ? 1024 : bytesToRead;
						int bytesRead = 0;
						try
						{	
							bytesRead = netStream.Read(receiveBuffer, bytesRead, toRead);
						}
						catch { }
						
						// lower the bytesNeeded with the bytesRead.
						bytesNeeded -= bytesRead;

						// write the readed bytes to the decompression stream.
						zlibDecompressedStream.Write(receiveBuffer, 0, bytesRead);
					}
					else
						// there isn't any data atm. let's give the processor some time.
						Thread.Sleep(100); // increased to 100ms for slow connections

				} while (bytesNeeded > 0);
				#endregion
				
				zlibMemoryStream.Position = 0;
			}
		}
	}
}