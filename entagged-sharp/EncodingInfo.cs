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
 * Revision 1.2  2005/08/31 07:58:58  jwillcox
 * 2005-08-31  James Willcox  <snorp@snorp.net>
 *
 *         * add an emacs modeline to all the .cs sources
 *         * src/IpodCore.cs: fix iPod syncing.
 *         * src/PlayerInterface.cs (OnSimpleSearch): fix a null reference that
 *         was causing some crashes.
 *
 * Revision 1.1  2005/08/25 21:03:43  abock
 * New entagged-sharp
 *
 * Revision 1.4  2005/02/18 13:38:12  kikidonk
 * Adds a isVbr method that checks wether the file is vbr or not, added check in OGG and MP3, other formats are always VBR
 *
 * Revision 1.3  2005/02/08 12:54:41  kikidonk
 * Added cvs log and header
 *
 */

using System.Collections;
using System.Text;

namespace Entagged.Audioformats {

public class EncodingInfo {
		
	private Hashtable content;
	
	public EncodingInfo() {
		content = new Hashtable();
		content["BITRATE"] =  -1;
		content["CHANNB"] =  -1;
		content["TYPE"] =  "";
		content["INFOS"] =  "";
		content["SAMPLING"] =  -1;
		content["LENGTH"] = -1;
		content["VBR"] = true;
	}
	
	//Sets the bitrate in KByte/s
	public int Bitrate {
		set { content["BITRATE"] = value; }
		get { return (int) content["BITRATE"]; }
	}
	//Sets the number of channels
	public int ChannelNumber {
		set { content["CHANNB"] = value; }
		get { return (int) content["CHANNB"]; }
	}
	//Sets the type of the encoding, this is a bit format specific. eg:Layer I/II/II
	public string EncodingType {
		set { content["TYPE"] = value; }
		get { return (string) content["TYPE"]; }
	}
	//A string contianing anything else that might be interesting
	public string ExtraEncodingInfos {
		set { content["INFOS"] = value; }
		get { return (string) content["INFOS"]; }
	}
	//Sets the Sampling rate in Hz
	public int SamplingRate {
		set { content["SAMPLING"] = value; }
		get { return (int) content["SAMPLING"]; }
	}
	//Sets the length of the song in seconds
	public int Length {
		set { content["LENGTH"] = value; }
		get { return (int) content["LENGTH"]; }
	}
	
	public bool Vbr {
		set { content["VBR"] = value; }
		get { return (bool) content["VBR"]; }
	}
	
	
	//Pretty prints this encoding info
	public override string ToString() {
		StringBuilder sb = new StringBuilder();
		
		sb.Append("Encoding infos content:\n");
		foreach(DictionaryEntry entry in content) {
          sb.Append("\t");
		  sb.Append(entry.Key);
		  sb.Append(" : ");
		  sb.Append(entry.Value);
		  sb.Append("\n");
		}
		return sb.ToString().Substring(0,sb.Length-1);
	}
}
}
