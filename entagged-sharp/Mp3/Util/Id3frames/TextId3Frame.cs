/* -*- Mode: csharp; tab-width: 4; c-basic-offset: 4; indent-tabs-mode: t -*- */
/***************************************************************************
 *  Copyright 2005 Raphaël Slinckx <raphael@slinckx.net> 
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

/*
 * $Log$
 * Revision 1.2  2005/08/31 07:59:02  jwillcox
 * 2005-08-31  James Willcox  <snorp@snorp.net>
 *
 *         * add an emacs modeline to all the .cs sources
 *         * src/IpodCore.cs: fix iPod syncing.
 *         * src/PlayerInterface.cs (OnSimpleSearch): fix a null reference that
 *         was causing some crashes.
 *
 * Revision 1.1  2005/08/25 21:03:46  abock
 * New entagged-sharp
 *
 * Revision 1.3  2005/02/08 12:54:40  kikidonk
 * Added cvs log and header
 *
 */

using System;
using System.Text;
using Entagged.Audioformats.Util;
using Entagged.Audioformats.Mp3;

namespace Entagged.Audioformats.Mp3.Util.Id3Frames {
	public class TextId3Frame : Id3Frame {
		
		protected string content;
		protected byte encoding;
		protected string id;
		protected bool common;
		
		/*
		 * 0,1| frame flags
		 * 2| encoding
		 * 3,..,(0x00(0x00))| text content
		 */
		
		public TextId3Frame(string id, string content) {
			this.id = id;
			this.content = content;
			Encoding = Id3Tag.DEFAULT_ENCODING;
		}
		
		public TextId3Frame(string id, byte[] rawContent, byte version) : base(rawContent, version) {
			this.id = id;
		}
		
		public string Encoding {
			get {
			    if(encoding == 0)
			        return "ISO-8859-1";
			    else if(encoding == 1)
			        return "UTF-16";
			    
			    return "ISO-8859-1";
			}
			set {
				if(value == "ISO-8859-1")
		        	encoding = 0;
			    else if(value == "UTF-16")
			        encoding = 1;
			    else
			        encoding = 1;
			}
		}
		
		public string Content {
			get { return content; }
			set { this.content = value; }
		}
		
		public override bool IsBinary {
			get { return false; }
			set { /* Not allowed */ }
		}
		
		public override string Id {
			get { return this.id; }
		}
		
		public override bool IsCommon {
			get { return this.common; }
		}
				
		public override bool IsEmpty {
		    get { return content == ""; }
		}
		
		public override void CopyContent(TagField field) {
		    if(field is TextId3Frame) {
		        this.content = (field as TextId3Frame).Content;
		        Encoding = (field as TextId3Frame).Encoding;
		    }
		}
		
		protected override void Populate(byte[] raw) {
			this.encoding = raw[flags.Length];
			if(this.encoding != 0 && this.encoding != 1)
			    this.encoding = 0;

			this.content = GetString(raw, flags.Length+1, raw.Length-flags.Length-1, Encoding);
			
			this.content = this.content.Split('\0')[0];
		}
		
		protected override byte[] Build() 
		{
			byte[] data = GetBytes(this.content, Encoding);
			//the return byte[]
			byte[] b = new byte[4 + 4 + flags.Length + 1 + data.Length];
			
			int offset = 0;
			Copy(IdBytes, b, offset);        offset += 4;
			Copy(GetSize(b.Length-10), b, offset); offset += 4;
			Copy(flags, b, offset);               offset += flags.Length;
			
			b[offset] = this.encoding;	offset += 1;
			
			Copy(data, b, offset);
			
			return b;
		}
		
		public override string ToString() {
			return Content;
		}
	}
}
