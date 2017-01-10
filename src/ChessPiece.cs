//
//  ChessPiece.cs
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
using OpenTK;
using Crow;
using Tetra;
using GGL;
using Tetra.DynamicShading;

namespace Chess
{
	public class ChessPiece{
		float xAngle, zAngle;
		Vector3 position;
		InstancedModel<VAOChessData> newMesh;//replacment mesh when promote or unpromote
		PieceType originalType;
		PieceType promotion;

		public InstancedModel<VAOChessData> Mesh;
		public int InstanceIndex;
		public ChessPlayer Player;
		public bool IsPromoted;
		public bool OpenGLSync = false;

		public PieceType Type {
			get { return IsPromoted ? promotion : originalType; }
			set {
				originalType = value;
			}
		}

		public bool HasMoved;
		public bool Captured;

		public int InitX, InitY;

		public Point BoardCell{
			get { return new Point ((int)Math.Truncate (X), (int)Math.Truncate (Y)); }
		}

		public Vector3 Position {
			get { return position; }
			set {
				if (value == position)
					return;
				position = value;
				updatePos ();
			}
		}

		public float X {
			get {return position.X;}
			set {
				if (position.X == value)
					return;
				position.X = value;
				updatePos ();
			}
		}
		public float Y {
			get {return position.Y;}
			set {
				if (position.Y == value)
					return;
				position.Y = value;
				updatePos ();
			}
		}
		public float Z {
			get {return position.Z;}
			set {
				if (position.Z == value)
					return;
				position.Z = value;
				updatePos ();
			}
		}
		public float XAngle {
			get {return xAngle;}
			set {
				if (xAngle == value)
					return;
				xAngle = value;
				updatePos ();
			}
		}
		public float ZAngle {
			get {return zAngle;}
			set {
				if (zAngle == value)
					return;
				zAngle = value;
				updatePos ();
			}
		}
		public ChessPiece(InstancedModel<VAOChessData> vaoi, int idx, ChessPlayer _player , PieceType _type, int xPos, int yPos){
			Mesh = vaoi;
			InstanceIndex = idx;
			Player = _player;
			Type = _type;
			InitX= xPos;
			InitY= yPos;
			position = new Vector3(xPos + 0.5f, yPos + 0.5f, 0f);
			xAngle = 0f;
			HasMoved = false;
			Captured = false;

			UpdateColor ();
			updatePos ();

			Player.Pieces.Add (this);

			if (Type == PieceType.King)
				Player.King = this;
		}
		public void Reset(bool animate = true){
			xAngle = 0f;
			Z = 0f;
			if (HasMoved) {
				if (animate)
					Animation.StartAnimation (new PathAnimation (this, "Position",
						new BezierPath (
							Position,
							new Vector3 (InitX + 0.5f, InitY + 0.5f, 0f), Vector3.UnitZ)));
				else
					Position = new Vector3 (InitX + 0.5f, InitY + 0.5f, 0f);
			}
			Unpromote ();
			IsPromoted = false;
			HasMoved = false;
			Captured = false;
			UpdateColor ();
		}
		public void Promote(char prom, bool preview = false){
			if (IsPromoted)
				throw new Exception ("trying to promote already promoted " + Type.ToString());
			if (Type != PieceType.Pawn)
				throw new Exception ("trying to promote " + Type.ToString());
			IsPromoted = true;
			switch (prom) {
			case 'q':
				promotion = PieceType.Queen;
				newMesh = MainWin.vaoiQueen;
				break;
			case 'r':
				promotion = PieceType.Rook;
				newMesh = MainWin.vaoiRook;
				break;
			case 'b':
				promotion = PieceType.Bishop;
				newMesh = MainWin.vaoiBishop;
				break;
			case 'k':
				promotion = PieceType.Knight;
				newMesh = MainWin.vaoiKnight;
				break;
			default:
				throw new Exception ("Unrecognized promotion");
			}
			if (preview) {
				newMesh = null;
				return;
			}
			OpenGLSync = false;
		}
		public void Unpromote(){
			if (!IsPromoted)
				return;
			IsPromoted = false;
			if (Mesh == MainWin.vaoiPawn)
				return;
			newMesh = MainWin.vaoiPawn;
			OpenGLSync = false;
		}

		public void SyncGL(){
			if (OpenGLSync)
				return;
			if (newMesh != null) {
				removePieceInstance ();
				Mesh = newMesh;
				newMesh = null;
				InstanceIndex = Mesh.Instances.AddInstance ();
				updatePos ();
				UpdateColor ();
			}
			Mesh.Instances.UpdateVBO ();
			OpenGLSync = true;
		}
		void updatePos(){
//			Mesh.Datas [InstanceIndex].modelMats =
//				Matrix4.CreateRotationZ(zAngle) *
//				Matrix4.CreateTranslation(new Vector3(-0.32f, 0, 0)) *
//				Matrix4.CreateRotationY(xAngle) *
//				Matrix4.CreateTranslation(new Vector3(0.32f, 0, 0)) *
//				Matrix4.CreateRotationZ(xAngle/2f) *
//				Matrix4.CreateTranslation(new Vector3(x, y, z));
			Quaternion q = Quaternion.FromEulerAngles (zAngle, 0f, xAngle);
			Mesh.Datas [InstanceIndex].modelMats =
				Matrix4.CreateFromQuaternion (q) * Matrix4.CreateTranslation (position);
			OpenGLSync = false;
		}
		public void UpdateColor(){
			if (Player.Color == ChessColor.White) {
				Mesh.Datas [InstanceIndex].color = Crow.Configuration.Get<Color> ("WhiteColor").ToVector4();
				ZAngle = 0f;
			} else {
				Mesh.Datas [InstanceIndex].color = Crow.Configuration.Get<Color> ("BlackColor").ToVector4();
				ZAngle = MathHelper.Pi;
			}
			OpenGLSync = false;
		}
		void removePieceInstance()
		{
			Mesh.Instances.RemoveInstance (InstanceIndex);

			if (InstanceIndex == Mesh.Datas.Length)
				return;

			//reindex pce instances
			foreach (ChessPlayer pl in MainWin.Players) {
				foreach (ChessPiece pce in pl.Pieces) {
					if (pce == this || pce.Mesh != Mesh)
						continue;
					if (pce.InstanceIndex > InstanceIndex) {
						pce.InstanceIndex--;
						bool savedState = pce.OpenGLSync;
						pce.UpdateColor ();
						pce.updatePos ();
						pce.OpenGLSync = savedState; //will be update one for all pce
					}
				}
			}
			Mesh.Instances.UpdateVBO();
		}

	}

}

