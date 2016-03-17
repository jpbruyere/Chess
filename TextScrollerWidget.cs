//
//  ChessBoardWidget.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2016 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using Chess;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Collections;
using Cairo;


namespace Crow
{
	public class TextScrollerWidget : GraphicObject
	{
		const int scrBarWidth = 6;
		IList lines;
		bool scrollOnOutput;
		int scroll;
		int visibleLines = 1;
		FontExtents fe;

		[XmlAttributeAttribute()][DefaultValue(0)]
		public virtual int Scroll {
			get { return scroll; }
			set {
				if (scroll == value)
					return;

				scroll = value;

				if (scroll + visibleLines > Lines.Count)
					scroll = Lines.Count - visibleLines;
				if (scroll < 0)
					scroll = 0;

				NotifyValueChanged ("Scroll", scroll);
				RegisterForGraphicUpdate ();
			}
		}
		[XmlAttributeAttribute()][DefaultValue(true)]
		public virtual bool ScrollOnOutput {
			get { return scrollOnOutput; }
			set {
				if (scrollOnOutput == value)
					return;
				scrollOnOutput = value;
				NotifyValueChanged ("ScrollOnOutput", scrollOnOutput);

			}
		}
		[XmlAttributeAttribute()][DefaultValue(null)]
		public virtual IList Lines {
			get { return lines; }
			set {
				if (lines == value) {
					if (ScrollOnOutput && lines!=null)
						Scroll = lines.Count - visibleLines;
					RegisterForGraphicUpdate ();
					return;
				}
				lines = value;

				NotifyValueChanged ("Lines", lines);
				RegisterForGraphicUpdate ();
			}
		}
		public TextScrollerWidget ():base()
		{
		}
		public override void OnLayoutChanges (LayoutingType layoutType)
		{
			base.OnLayoutChanges (layoutType);

			if (layoutType == LayoutingType.Height) {
				using (ImageSurface img = new ImageSurface (Format.Argb32, 10, 10)) {
					using (Context gr = new Context (img)) {
						//Cairo.FontFace cf = gr.GetContextFontFace ();

						gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
						gr.SetFontSize (Font.Size);

						fe = gr.FontExtents;
					}
				}
				visibleLines = (int)Math.Floor ((double)ClientRectangle.Height / fe.Height);
			}
		}
		protected override void onDraw (Cairo.Context gr)
		{
			base.onDraw (gr);

			if (lines == null)
				return;

			gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
			gr.SetFontSize (Font.Size);

			Rectangle r = ClientRectangle;

			Foreground.SetAsSource (gr);

			double y = ClientRectangle.Y;
			double x = ClientRectangle.X;
			Fill errorFill = new SolidColor (Color.Carmine);
			for (int i = 0; i < visibleLines; i++) {
				if (i + Scroll >= Lines.Count)
					break;
				if ((lines [i + Scroll] as string).StartsWith ("error", StringComparison.OrdinalIgnoreCase)) {
					errorFill.SetAsSource (gr);
					gr.Rectangle (x, y, (double)r.Width, fe.Height);
					gr.Fill ();
					Foreground.SetAsSource (gr);
				}
				gr.MoveTo (x, y + fe.Ascent);
				gr.ShowText (lines[i+Scroll] as string);
				y += fe.Height;
				gr.Fill ();
			}

			if (Lines.Count <= visibleLines)
				return;
			Rectangle scrBar = ClientRectangle;
			scrBar.X += ClientRectangle.Width - scrBarWidth;
			scrBar.Width = scrBarWidth;

			new SolidColor (Color.LightGray.AdjustAlpha(0.5)).SetAsSource (gr);
			CairoHelpers.CairoRectangle(gr, scrBar, 2.0);
			gr.Fill ();
			new SolidColor (Color.BlueCrayola.AdjustAlpha(0.7)).SetAsSource (gr);
			scrBar.Y += (int)((double)scrBar.Height * (double)Scroll / (double)Lines.Count);
			scrBar.Height = (int)((double)scrBar.Height * (double)visibleLines / (double)Lines.Count);
			CairoHelpers.CairoRectangle(gr, scrBar, 2.0);
			gr.Fill ();
		}
		public override void onMouseWheel (object sender, MouseWheelEventArgs e)
		{
			base.onMouseWheel (sender, e);
			if (e.Delta > 0)
				Scroll--;
			else
				Scroll++;
		}
	}
}

