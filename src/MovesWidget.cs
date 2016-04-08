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
	public class MovesWidget : GraphicObject
	{		
		IList moves;
		Scroller scr;

		[XmlAttributeAttribute()][DefaultValue(null)]
		public virtual IList Moves {
			get { return moves; }
			set {
				if (moves != value) {
					moves = value; 
					NotifyValueChanged ("Moves", moves);
				}
				RegisterForLayouting (LayoutingType.Height);
				RegisterForGraphicUpdate ();
			}
		} 
		public MovesWidget ():base()
		{			
		}
		protected override int measureRawSize (LayoutingType lt)
		{
			using (ImageSurface img = new ImageSurface (Format.Argb32, 10, 10)) {
				using (Context gr = new Context (img)) {
					//Cairo.FontFace cf = gr.GetContextFontFace ();

					gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
					gr.SetFontSize (Font.Size);

					if (moves == null)
						return 10;
					if (moves.Count == 0)
						return 10;
					if (lt == LayoutingType.Width)
						return (int)(gr.FontExtents.MaxXAdvance * 5);
					else
						return (int)gr.FontExtents.Height * moves.Count;					
				}
			}
		}
		protected override void onDraw (Cairo.Context gr)
		{
			base.onDraw (gr);

			if (moves == null)
				return;
			
			gr.SelectFontFace (Font.Name, Font.Slant, Font.Wheight);
			gr.SetFontSize (Font.Size);

			Cairo.FontExtents fe = gr.FontExtents;

			Rectangle r = ClientRectangle;

			Foreground.SetAsSource (gr);

			double y = ClientRectangle.Y;
			double x = ClientRectangle.X;

			for (int i = moves.Count - 1; i >= 0; i--) {
				gr.MoveTo (x, y + fe.Ascent);
				gr.ShowText (moves[i] as string);
				y += fe.Height;
			}
			gr.Fill ();

			const int scrBarWidth = 1;
			if (scr == null)
				return;
			if (scr.ClientRectangle.Height > ClientRectangle.Height)
				return;
			Rectangle scrBar = ClientRectangle;
			scrBar.X += ClientRectangle.Width - scrBarWidth;
			scrBar.Width = scrBarWidth;
			scrBar.Height = scr.ClientRectangle.Height;
			scrBar.Y += (int)scr.ScrollY;

			new SolidColor (Color.LightGray.AdjustAlpha(0.5)).SetAsSource (gr);
			gr.Rectangle (scrBar);
			gr.Fill ();
			new SolidColor (Color.BlueCrayola.AdjustAlpha(0.7)).SetAsSource (gr);
			double ratio = (double)scr.ClientRectangle.Height / ClientRectangle.Height;
			scrBar.Height = (int)((double)scr.ClientRectangle.Height * ratio);
			scrBar.Y += (int)(scr.ScrollY * ratio);
			gr.Rectangle (scrBar);
			gr.Fill ();
		}
		public override ILayoutable Parent {
			get {
				return base.Parent;
			}
			set {
				if (scr != null)
					scr.Scrolled -= Scr_Scrolled;
				
				base.Parent = value;

				scr = Parent as Scroller;
				if (scr != null)
					scr.Scrolled += Scr_Scrolled;
			}
		}

		void Scr_Scrolled (object sender, ScrollingEventArgs e)
		{
			RegisterForGraphicUpdate ();
		}
	}
}

