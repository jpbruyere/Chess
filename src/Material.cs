//
//  Material.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2017 jp
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
using Crow;

namespace Chess
{
	public class Material : IValueChange
	{
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public void NotifyValueChanged(string MemberName, object _value)
		{
			if (ValueChanged != null)
				ValueChanged.Invoke(this, new ValueChangeEventArgs(MemberName, _value));
		}
		#endregion


		Color diffuse, ambient, specular;
		double shininess;

		public Color Diffuse {
			get { return diffuse; }
			set {
				if (Diffuse == value)
					return;
				diffuse = value;
				NotifyValueChanged ("Diffuse", value);
			}
		}
		public Color Ambient {
			get { return ambient; }
			set {
				if (Ambient == value)
					return;
				ambient = value;
				NotifyValueChanged ("Ambient", value);
			}
		}
		public Color Specular {
			get { return specular; }
			set {
				if (Specular == value)
					return;
				specular = value;
				NotifyValueChanged ("Specular", value);
			}
		}
		public double Shininess {
			get { return Math.Round (shininess, 2); }
			set {
				if (shininess == value)
					return;
				shininess = Math.Round (value, 2);
				NotifyValueChanged ("Shininess", Shininess);
			}
		}

		#region Object overrides and operators
		public static bool operator ==(Material m1, Material m2){
			return m1 is Material ? m2 is Material ?
				m1.Diffuse == m2.Diffuse &&
				m1.Ambient == m2.Ambient &&
				m1.Specular == m2.Specular &&
				m1.Shininess == m2.Shininess : false : true;
		}
		public static bool operator !=(Material m1, Material m2){
			return !(m1.Diffuse == m2.Diffuse &&
				m1.Ambient == m2.Ambient &&
				m1.Specular == m2.Specular &&
				m1.Shininess == m2.Shininess);
		}

		public override int GetHashCode ()
		{
			return Diffuse.GetHashCode () ^
				Ambient.GetHashCode () ^
				Specular.GetHashCode () ^
				Shininess.GetHashCode ();
		}
		public override bool Equals (object obj)
		{
			return (obj == null || obj.GetType() != typeof(Material)) ?
				false :
				this == (Material)obj;
		}
		public override string ToString ()
		{
			return string.Format ("{0};{1};{2};{3}", Diffuse, Ambient, Specular, Shininess);
		}
		#endregion

		public static Material Parse(string str){
			if (string.IsNullOrEmpty (str))
				return new Material();
			string[] tmp = str.Trim ().Split (';');
			return new Material () {
				Diffuse = (Color)Color.Parse (tmp [0]),
				Ambient = (Color)Color.Parse (tmp [1]),
				Specular = (Color)Color.Parse (tmp [2]),
				Shininess = float.Parse (tmp [3])
			};
		}
	}
}

