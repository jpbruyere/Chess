//
//  VAOChessData.cs
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
using System.Runtime.InteropServices;
using OpenTK;

namespace Chess
{
	[StructLayout(LayoutKind.Sequential,Pack=16)]
	public struct VAOChessData
	{
		public Matrix4 modelMats;
		public Vector4 color;
		public Vector4 ambient;
		public Vector4 specular;

		public VAOChessData(Matrix4 _model, Vector4 _color){
			modelMats = _model;
			color = _color;
			ambient = new Vector4 (0.1f, 0.1f, 0.1f, 1.0f);
			specular = new Vector4 (0.9f, 0.9f, 0.9f, 0.2f);
		}
	}
}

