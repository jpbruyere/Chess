//
//  MainWin.cs
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
using Crow;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Tetra;
using System.IO;
using Tetra.DynamicShading;
using System.Reflection;

namespace Chess
{
	public enum GameState { Init, MeshesLoading, VAOInit, ComputeTangents, BuildBuffers, Play, Checked, Pad, Checkmate };
	public enum PlayerType { Human, AI };
	public enum ChessColor { White, Black };
	public enum PieceType { Pawn, Rook, Knight, Bishop, King, Queen };

	public class InstancedChessModel : InstancedModel<VAOChessData> {
		public InstancedChessModel(MeshPointer pointer) : base(pointer) {}

		public void SetModelMat (int index, Matrix4 modelMat){
			Instances.InstancedDatas[index].modelMats = modelMat;
			SyncVBO = true;
		}
		public void SetColor (int index, Vector4 color){
			Instances.InstancedDatas[index].color = color;
			SyncVBO = true;
		}
		public void Set (int index, Matrix4 modelMat, Vector4 color){
			Instances.InstancedDatas[index].modelMats = modelMat;
			Instances.InstancedDatas[index].color = color;
			SyncVBO = true;
		}
		public void Set (Matrix4 modelMat, Vector4 color){
			if (Instances == null)
				Instances = new InstancesVBO<VAOChessData> (new VAOChessData[1]);
			Instances.InstancedDatas[0].modelMats = modelMat;
			Instances.InstancedDatas[0].color = color;
			SyncVBO = true;
		}
		public int AddInstance (){
			if (Instances == null)
				Instances = new InstancesVBO<VAOChessData> (new VAOChessData[1]);
			int idx = Instances.AddInstance ();
			SyncVBO = true;
			return idx;
		}
		public void AddInstance (Matrix4 modelMat, Vector4 color){
			if (Instances == null)
				Instances = new InstancesVBO<VAOChessData> (new VAOChessData[1]);
			Instances.AddInstance (new VAOChessData (modelMat, color));
			SyncVBO = true;
		}
		public void RemoveInstance (int index){
			Instances.RemoveInstance (index);
			SyncVBO = true;
		}

		public void UpdateBuffer(){
			if (!SyncVBO)
				return;
			Instances.UpdateVBO();
			SyncVBO = false;
		}
	}
	class MainWin : CrowWindow
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct UBOSharedData
		{
			public Vector4 Color;
			public Matrix4 modelview;
			public Matrix4 projection;
			public Matrix4 normal;
			public Vector4 LightPosition;
		}

		#region  scene matrix and vectors
		public static Matrix4 modelview;
		public static Matrix4 reflectedModelview;
		public static Matrix4 orthoMat//full screen quad rendering
		= OpenTK.Matrix4.CreateOrthographicOffCenter (-0.5f, 0.5f, -0.5f, 0.5f, 1, -1);
		public static Matrix4 projection;
		public static Matrix4 invMVP;
		public static int[] viewport = new int[4];

		public float EyeDist {
			get { return eyeDist; }
			set {
				eyeDist = value;
				UpdateViewMatrix ();
			}
		}
		public Vector3 vEyeTarget = new Vector3(4f, 2.8f, 0f);
		public Vector3 vEye;
		public Vector3 vLookInit = Vector3.Normalize(new Vector3(0.0f, -0.7f, 0.7f));
		public Vector3 vLook;  // Camera vLook Vector
		public float zFar = 30.0f;
		public float zNear = 0.1f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 12f;
		float eyeDistTarget = 12f;
		float MoveSpeed = 0.02f;
		float RotationSpeed = 0.005f;
		float ZoomSpeed = 2f;
		float viewZangle, viewXangle;

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -1f, 0f);
		//public Vector4 vLight = Vector4.Normalize(new Vector4 (0.1f, 0.1f, -0.8f, 0f));
		Vector4 arrowColor = new Vector4 (0.2f, 1.0f, 0.2f, 0.5f);

		Vector4 validPosColor = new Vector4 (0.0f, 0.5f, 0.7f, 0.5f);
		Vector4 activeColor = new Vector4 (0.2f, 0.2f, 1.0f, 0.5f);
		Vector4 kingCheckedColor = new Vector4 (1.0f, 0.1f, 0.1f, 0.8f);

//		uniform vec3 diffuse = vec3(1.0, 1.0, 1.0);
//		uniform vec3 ambient = vec3(0.5, 0.5, 0.5);
//		uniform vec3 specular = vec3(0.7,0.7,0.7);
		volatile bool shaderMatsAreDirty = true;
		#endregion

		#region Options
		public int ReflexionIntensity {
			get {
				return Crow.Configuration.Get<int> ("ReflexionIntensity");
			}
			set {
				if (LightX == value)
					return;
				Crow.Configuration.Set ("ReflexionIntensity", value);
				NotifyValueChanged ("ReflexionIntensity", value);
				vaoiQuad.SetColor (0, new Vector4 (1.0f, 1.0f, 1.0f, (float)ReflexionIntensity / 100f));
			}
		}
		public int LightX {
			get {
				return Crow.Configuration.Get<int> ("LightX");
			}
			set {
				if (LightX == value)
					return;
				Crow.Configuration.Set ("LightX", value);
				NotifyValueChanged ("LightX", value);
				shaderMatsAreDirty = true;
			}
		}
		public int LightY {
			get {
				return Crow.Configuration.Get<int> ("LightY");
			}
			set {
				if (LightY == value)
					return;
				Crow.Configuration.Set ("LightY", value);
				NotifyValueChanged ("LightY", value);
				shaderMatsAreDirty = true;
			}
		}
		public int LightZ {
			get {
				return Crow.Configuration.Get<int> ("LightZ");
			}
			set {
				if (LightZ == value)
					return;
				Crow.Configuration.Set ("LightZ", value);
				NotifyValueChanged ("LightZ", value);
				shaderMatsAreDirty = true;
			}
		}
		float[] mainColor;
		float[] clearColor;
		public Color BackgroundColor {
			get {
				return Crow.Configuration.Get<Color> ("BackgroundColor");
			}
			set {
				if (BackgroundColor == value)
					return;
				Crow.Configuration.Set ("BackgroundColor", value);
				clearColor = value.floatArray;
				NotifyValueChanged ("BackgroundColor", value);
			}
		}
		public Color MainColor {
			get {
				return Crow.Configuration.Get<Color> ("MainColor");
			}
			set {
				if (MainColor == value)
					return;
				Crow.Configuration.Set ("MainColor", value);
				mainColor = value.floatArray;
				NotifyValueChanged ("MainColor", value);
			}
		}
		public Color WhiteColor {
			get {
				return Crow.Configuration.Get<Color> ("WhiteColor");
			}
			set {
				if (WhiteColor == value)
					return;
				Crow.Configuration.Set ("WhiteColor", value);
				NotifyValueChanged ("WhiteColor", value);
				foreach (ChessPiece pce in Players[0].Pieces)
					pce.UpdateColor ();
			}
		}
		public Color BlackColor {
			get {
				return Crow.Configuration.Get<Color> ("BlackColor");
			}
			set {
				if (BlackColor == value)
					return;
				Crow.Configuration.Set ("BlackColor", value);
				NotifyValueChanged ("BlackColor", value);
				foreach (ChessPiece pce in Players[1].Pieces)
					pce.UpdateColor ();
			}
		}
		public float Shininess {
			get { return Crow.Configuration.Get<float> ("Shininess"); }
			set {
				if (Shininess == value)
					return;
				Crow.Configuration.Set ("Shininess", value);
				shaderUniformsAreDirty = true;
				NotifyValueChanged ("Shininess", value);
			}
		}
		public int Samples {
			get { return Crow.Configuration.Get<int> ("Samples"); }
			set {
				if (Samples == value)
					return;
				Crow.Configuration.Set ("Samples", value);
				NotifyValueChanged ("Samples", value);
			}
		}
		public float ScreenGamma {
			get { return Crow.Configuration.Get<float> ("ScreenGamma"); }
			set {
				if (ScreenGamma == value)
					return;
				Crow.Configuration.Set ("ScreenGamma", value);
				shaderUniformsAreDirty = true;
				NotifyValueChanged ("ScreenGamma", value);
			}
		}
		//		public Vector3 Diffuse {
		//			get { return Crow.Configuration.Get<Vector3> ("Diffuse"); }
		//			set {
		//				if (Diffuse == value)
		//					return;
		//				piecesShader.Enable ();
		//				GL.Uniform3(GL.GetUniformLocation(piecesShader.pgmId, "diffuse"), value);
		//				Crow.Configuration.Set ("Diffuse", value);
		//				NotifyValueChanged ("Diffuse", value);
		//			}
		//		}
		#endregion

		#region GL

		UBOSharedData shaderSharedData;
		int uboShaderSharedData;

		MeshesGroup<MeshData> meshes;

		public static Mat4InstancedShader piecesShader;

		public static InstancedVAO<MeshData, VAOChessData> mainVAO;
		public static InstancedChessModel boardVAOItem;
		public static InstancedChessModel boardPlateVAOItem;
		public static InstancedChessModel cellVAOItem;
		public static InstancedChessModel vaoiPawn;
		public static InstancedChessModel vaoiBishop;
		public static InstancedChessModel vaoiKnight;
		public static InstancedChessModel vaoiRook;
		public static InstancedChessModel vaoiQueen;
		public static InstancedChessModel vaoiKing;
		public static InstancedChessModel vaoiQuad;//full screen quad in mainVAO to prevent unbind
													//while drawing reflexion

		public bool Reflexion {
			get { return Crow.Configuration.Get<bool> ("Reflexion"); }
			set {
				if (Reflexion == value)
					return;
				if (value)
					initReflexionFbo ();
				else
					disableReflexionFbo ();

				Crow.Configuration.Set ("Reflexion", value);
				NotifyValueChanged ("Reflexion", value);
			}
		}
		//DynamicShader dynShader;
		const int GBP_UBO0 = 0;
		void initOpenGL()
		{
			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
			mainColor = Crow.Configuration.Get<Color> ("MainColor").floatArray;
			clearColor = Crow.Configuration.Get<Color> ("BackgroundColor").floatArray;

			Debug.WriteLine("MaxVertexAttribs: " + GL.GetInteger(GetPName.MaxVertexAttribs));
			GL.Enable (EnableCap.CullFace);
			GL.CullFace (CullFaceMode.Back);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);
			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			piecesShader = new Mat4InstancedShader();
			piecesShader.Enable ();

			updateShaderUniforms ();

			#region test DynamicShading
//			dynShader = new DynamicShader ();
//			dynShader.RegisterUBODataStruct (new UBOModel<UBOSharedData> (GBP_UBO0));
//			dynShader.BuildSources();
//
//			UniformBufferObject<UBOSharedData> ubo = new UniformBufferObject<UBOSharedData> ();
//			ubo.Datas.Color = new Vector4(1,1,1,1);
//			ubo.UpdateGPU();

			//ubo.Bind(GBP_UBO0);
			#endregion

			shaderSharedData.Color = new Vector4(1,1,1,1);
			uboShaderSharedData = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferRangeTarget.UniformBuffer, 0, uboShaderSharedData);

			GL.ActiveTexture (TextureUnit.Texture0);

			int b;
			GL.GetInteger(GetPName.StencilBits, out b);

			ErrorCode err = GL.GetError ();
			Debug.Assert (err == ErrorCode.NoError, "OpenGL Error");
		}
		bool shaderUniformsAreDirty = false;
		void updateShaderUniforms(){
			GL.Uniform1(GL.GetUniformLocation(piecesShader.pgmId, "shininess"), Shininess);
			GL.Uniform1(GL.GetUniformLocation(piecesShader.pgmId, "screenGamma"), ScreenGamma/100.0f);
			shaderUniformsAreDirty = false;
		}
		void updateShadersMatrices(){
			shaderSharedData.projection = projection;
			shaderSharedData.modelview = modelview;
			shaderSharedData.normal = modelview.Inverted();
			shaderSharedData.normal.Transpose ();
//			if (viewZangle == 0)//white game
				shaderSharedData.LightPosition = Vector4.Transform(
					new Vector4((float)LightX, (float)LightY, (float)LightZ, 0),	modelview);
//			else
//				shaderSharedData.LightPosition = Vector4.Transform(
//					new Vector4((float)LightX, -(float)LightY, (float)LightZ, 0),	modelview);

			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			shaderMatsAreDirty = false;
		}
		void changeShadingColor(Vector4 color){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes,
				ref color);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}
		void changeShadingColor(float[] color){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes,
				color);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}
		void changeMVP(Matrix4 newProjection, Matrix4 newModelView){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)Vector4.SizeInBytes, Vector4.SizeInBytes * 4,
				ref newModelView);
			GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)(Vector4.SizeInBytes * 5), Vector4.SizeInBytes * 4,
				ref newProjection);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}
		void changeModelView(Matrix4 newModelView){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)Vector4.SizeInBytes, Vector4.SizeInBytes * 4,
				ref newModelView);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}

		void loadMeshes()
		{
			string meshesPath = @"Datas/simple/";
			string meshesExt = ".bin";

			CurrentState = GameState.MeshesLoading;
			meshes = new MeshesGroup<MeshData>();
			vaoiPawn = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (meshesPath + "p" + meshesExt)));
			ProgressValue+=20;
			vaoiBishop = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (meshesPath + "b" + meshesExt)));
			ProgressValue+=20;
			vaoiKnight = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (meshesPath + "h" + meshesExt)));
			ProgressValue+=20;
			vaoiRook = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (meshesPath + "r" + meshesExt)));
			ProgressValue+=20;
			vaoiQueen = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (meshesPath + "q" + meshesExt)));
			ProgressValue+=20;
			vaoiKing = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (meshesPath + "k" + meshesExt)));
			ProgressValue+=20;
			boardVAOItem = new InstancedChessModel (meshes.Add (Mesh<MeshData>.Load (@"Datas/board.bin")));
			ProgressValue+=20;
			vaoiQuad = new InstancedChessModel (meshes.Add (Mesh<MeshData>.CreateQuad (0, 0, 0, 1, 1, 1, 1)));

			float
			x = 4f,
			y = 4f,
			width = 8f,
			height = 8f;

			boardPlateVAOItem = new InstancedChessModel (meshes.Add (new Mesh<MeshData> (
				new Vector3[] {
					new Vector3 (x - width / 2f, y + height / 2f, 0f),
					new Vector3 (x - width / 2f, y - height / 2f, 0f),
					new Vector3 (x + width / 2f, y + height / 2f, 0f),
					new Vector3 (x + width / 2f, y - height / 2f, 0f)
				},
				new MeshData (
					new Vector2[] {
						new Vector2 (0, 2),
						new Vector2 (0, 0),
						new Vector2 (2, 2),
						new Vector2 (2, 0)
					},
					new Vector3[] {
						Vector3.UnitZ,
						Vector3.UnitZ,
						Vector3.UnitZ,
						Vector3.UnitZ
					}
				), new ushort[] { 0, 1, 2, 2, 1, 3 })));

			x = 0f;
			y = 0f;
			width = 1.0f;
			height = 1.0f;

			cellVAOItem = new InstancedChessModel (meshes.Add (new Mesh<MeshData> (
				new Vector3[] {
					new Vector3 (x - width / 2f, y + height / 2f, 0f),
					new Vector3 (x - width / 2f, y - height / 2f, 0f),
					new Vector3 (x + width / 2f, y + height / 2f, 0f),
					new Vector3 (x + width / 2f, y - height / 2f, 0f)
				},
				new MeshData (
					new Vector2[] {
						new Vector2 (0, 1),
						new Vector2 (0, 0),
						new Vector2 (1, 1),
						new Vector2 (1, 0)
					},
					new Vector3[] {
						Vector3.UnitZ,
						Vector3.UnitZ,
						Vector3.UnitZ,
						Vector3.UnitZ
					}
				), new ushort[] { 0, 1, 2, 2, 1, 3 })));

			CurrentState = GameState.VAOInit;
		}
		void createMainVAO(){
			string texturesPath = @"Datas/simple/";
			string texturesExt = ".dds";

			mainVAO = new InstancedVAO<MeshData, VAOChessData> (meshes);

			vaoiQuad.Set (Matrix4.Identity, new Vector4 (1.0f, 1.0f, 1.0f, (float)ReflexionIntensity / 100f));

			Tetra.Texture.DefaultWrapMode = TextureWrapMode.Repeat;

			boardPlateVAOItem.Diffuse = Tetra.Texture.Load (@"Datas/board3.dds");
			boardPlateVAOItem.Set (Matrix4.Identity, new Vector4(0.7f,0.7f,0.7f,1.0f));

			boardVAOItem.Diffuse = Tetra.Texture.Load (@"Datas/marble1.dds");
			boardVAOItem.Set (Matrix4.CreateTranslation (4f, 4f, -0.15f), new Vector4(0.4f,0.4f,0.42f,1.0f));

			Tetra.Texture.DefaultWrapMode = TextureWrapMode.ClampToEdge;

			cellVAOItem.Diffuse = Tetra.Texture.Load (@"Datas/marble.dds");
			cellVAOItem.Set (Matrix4.CreateTranslation (new Vector3 (4.5f, 4.5f, 0f)), new Vector4 (0.3f, 1.0f, 0.3f, 0.5f));


			Tetra.Texture.GenerateMipMaps = true;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.LinearMipmapLinear;
			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Linear;
			Tetra.Texture.DefaultWrapMode = TextureWrapMode.ClampToEdge;

			vaoiPawn.Diffuse = Tetra.Texture.Load (texturesPath + "p" + texturesExt);
			vaoiPawn.Instances = new InstancesVBO<VAOChessData> (new VAOChessData[16]);
			ProgressValue++;

			vaoiBishop.Diffuse = Tetra.Texture.Load (texturesPath + "b" + texturesExt);
			vaoiBishop.Instances = new InstancesVBO<VAOChessData> (new VAOChessData[4]);
			ProgressValue++;

			vaoiKnight.Diffuse = Tetra.Texture.Load (texturesPath + "h" + texturesExt);
			vaoiKnight.Instances = new InstancesVBO<VAOChessData> (new VAOChessData[4]);
			ProgressValue++;

			vaoiRook.Diffuse = Tetra.Texture.Load (texturesPath + "r" + texturesExt);
			vaoiRook.Instances = new InstancesVBO<VAOChessData> (new VAOChessData[4]);
			ProgressValue++;

			vaoiQueen.Diffuse = Tetra.Texture.Load (texturesPath + "q" + texturesExt);
			vaoiQueen.Instances = new InstancesVBO<VAOChessData> (new VAOChessData[2]);
			ProgressValue++;


			vaoiKing.Diffuse = Tetra.Texture.Load (texturesPath + "k" + texturesExt);
			vaoiKing.Instances = new InstancesVBO<VAOChessData> (new VAOChessData[2]);
			ProgressValue++;

			Tetra.Texture.ResetToDefaultLoadingParams ();
		}
		void computeTangents(){
			//mainVAO.ComputeTangents();
			CurrentState = GameState.BuildBuffers;
		}

		void draw()
		{
			piecesShader.Enable ();
			if (shaderUniformsAreDirty)
				updateShaderUniforms ();
			piecesShader.SetLightingPass ();

			mainVAO.Bind ();

			changeShadingColor(mainColor);

			draw (boardVAOItem);

			if (Reflexion) {
				GL.Enable (EnableCap.StencilTest);

				//cut stencil
				GL.StencilFunc (StencilFunction.Always, 1, 0xff);
				GL.StencilOp (StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
				GL.StencilMask (0xff);
				GL.DepthMask (false);

				draw (boardPlateVAOItem);

				//draw reflected items
				GL.StencilFunc (StencilFunction.Equal, 1, 0xff);
				GL.StencilMask (0x00);

				drawReflexion ();

				GL.Disable(EnableCap.StencilTest);
				GL.DepthMask (true);
			}else
				draw (boardPlateVAOItem);

			//draw scene

			#region sel squarres
			GL.Disable (EnableCap.DepthTest);

			draw (cellVAOItem);

			GL.Enable (EnableCap.DepthTest);
			#endregion

			drawPieces ();

			mainVAO.Unbind ();

			piecesShader.SetSimpleColorPass ();
			changeShadingColor (arrowColor);
			renderArrow ();

			GL.StencilMask (0xff);
		}
		void drawPieces(){
			draw (vaoiPawn);
			draw (vaoiBishop);
			draw (vaoiKnight);
			draw (vaoiRook);
			draw (vaoiQueen);
			draw (vaoiKing);
		}
		void draw(InstancedChessModel model, BeginMode beginMode = BeginMode.Triangles){
			model.UpdateBuffer();
			GL.BindTexture (TextureTarget.Texture2D, model.Diffuse);
			mainVAO.Render (beginMode, model.VAOPointer, model.Instances);
		}
		#region Arrows
		GGL.vaoMesh arrows;
		void clearArrows(){
			if (arrows!=null)
				arrows.Dispose ();
			arrows = null;
		}
		public void createArrows(string move){
			if (string.IsNullOrEmpty (move))
				return;

			if (arrows!=null)
				arrows.Dispose ();
			arrows = null;

			Point pStart = getChessCell(move.Substring(0,2));
			Point pEnd = getChessCell(move.Substring(2,2));
			arrows = new GGL.Arrow3d (
				new Vector3 ((float)pStart.X + 0.5f, (float)pStart.Y + 0.5f, 0),
				new Vector3 ((float)pEnd.X + 0.5f, (float)pEnd.Y + 0.5f, 0),
				Vector3.UnitZ);
		}
		void renderArrow(){
			if (arrows == null)
				return;

			GL.Disable (EnableCap.CullFace);
			arrows.Render (BeginMode.TriangleStrip);
			GL.Enable (EnableCap.CullFace);

		}
		#endregion

		#region ReflexionFBO

		int reflexionTex, fboReflexion, depthRenderbuffer;

		void disableReflexionFbo()
		{
			if (GL.IsTexture (reflexionTex)) {
				GL.DeleteTexture (reflexionTex);
				GL.DeleteRenderbuffer (depthRenderbuffer);
				GL.DeleteFramebuffer (fboReflexion);
			}
		}
		void initReflexionFbo()
		{
			disableReflexionFbo ();

			System.Drawing.Size cz = ClientRectangle.Size;

			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Nearest;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.Nearest;
			Tetra.Texture.GenerateMipMaps = false;
			{
				reflexionTex = new Tetra.Texture (cz.Width, cz.Height);
			}
			Tetra.Texture.ResetToDefaultLoadingParams ();

			// Create Depth Renderbuffer
			GL.GenRenderbuffers( 1, out depthRenderbuffer );
			GL.BindRenderbuffer( RenderbufferTarget.Renderbuffer, depthRenderbuffer );
			GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, (RenderbufferStorage)All.DepthComponent32, cz.Width, cz.Height);

			GL.GenFramebuffers(1, out fboReflexion);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboReflexion);
			GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
				TextureTarget.Texture2D, reflexionTex, 0);
			GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, depthRenderbuffer );

			GL.DrawBuffers(1, new DrawBuffersEnum[]{DrawBuffersEnum.ColorAttachment0});

			if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
			{
				throw new Exception(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer).ToString());
			}

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
		}
		void updateReflexionFbo()
		{
			piecesShader.Enable ();
			piecesShader.SetLightingPass ();

			mainVAO.Bind ();

			changeShadingColor(MainColor.floatArray);

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboReflexion);

			GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
			GL.CullFace(CullFaceMode.Front);
			changeModelView (reflectedModelview);
			drawPieces ();
			changeModelView (modelview);
			GL.CullFace(CullFaceMode.Back);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			mainVAO.Unbind ();
		}
		void drawReflexion(){
			piecesShader.SetSimpleTexturedPass ();
			changeMVP (orthoMat, Matrix4.Identity);
			vaoiQuad.Diffuse = reflexionTex;
			draw (vaoiQuad, BeginMode.TriangleStrip);
			changeMVP (projection, modelview);
			piecesShader.SetLightingPass ();
		}
		#endregion
		#endregion

		#region Interface
		const string UI_Menu = "#Chess.gui.menu.crow";
		const string UI_NewGame = "#Chess.gui.newGame.crow";
		const string UI_Options = "#Chess.gui.options.crow";
		const string UI_Fps = "#Chess.gui.fps.crow";
		const string UI_Board = "#Chess.gui.board.crow";
		const string UI_Log = "#Chess.gui.log.crow";
		const string UI_Moves = "#Chess.gui.moves.crow";
		const string UI_Splash = "#Chess.gui.Splash.crow";
		const string UI_About = "#Chess.gui.about.crow";
		const string UI_Save = "#Chess.gui.saveDialog.crow";
		const string UI_Load = "#Chess.gui.loadDialog.crow";
		const string UI_Promote = "#Chess.gui.promote.crow";

		volatile int progressValue=0;
		volatile int progressMax=200;
		public int ProgressValue{
			get {
				return progressValue;
			}
			set {
				progressValue = value;
				NotifyValueChanged("ProgressValue", progressValue);
			}
		}
		public int ProgressMax{
			get {
				return progressMax;
			}
			set {
				progressMax = value;
				NotifyValueChanged("ProgressMax", progressMax);
			}
		}
		string fileName = "game.chess";

		public string FileName {
			get { return fileName; }
			set {
				if (fileName == value)
					return;
				fileName = value;
				NotifyValueChanged("FileName", fileName);
			}
		}
		public string[] SavedGames {
			get { return Directory.GetFiles (".", "*.chess"); }
		}

		void loadWindow(string path){
			try {
				GraphicObject g = FindByName (path);
				if (g != null)
					return;
				g = Load (path);
				g.Name = path;
				g.DataSource = this;
			} catch (Exception ex) {
				Debug.WriteLine (ex.ToString ());
			}
		}
		void closeWindow (string path){
			GraphicObject g = FindByName (path);
			if (g != null)
				ifaceControl [0].CrowInterface.DeleteWidget (g);
		}

		void initInterface(){
			MouseMove += Mouse_Move;
			MouseButtonDown += Mouse_ButtonDown;
			MouseWheelChanged += Mouse_WheelChanged;
			KeyboardKeyDown += MainWin_KeyboardKeyDown;

			loadWindow (UI_Menu);

			closeWindow (UI_Splash);
		}

		#region LOGS
		List<string> logBuffer = new List<string> ();
		public List<string> LogBuffer {
			get { return logBuffer; }
			set { logBuffer = value; }
		}

		void AddLog(string msg)
		{
			if (string.IsNullOrEmpty (msg))
				return;
			LogBuffer.Add (msg);
			NotifyValueChanged ("LogBuffer", logBuffer);
		}
		#endregion

		void onSaveClick (object sender, MouseButtonEventArgs e){
			if (StockfishMoves.Count == 0)
				return;
			loadWindow (UI_Save);
			FileName = "game-" + DateTime.Now.ToString () + ".chess";
		}
		void onSaveOkClick (object sender, MouseButtonEventArgs e){
			if (!FileName.EndsWith (".chess"))
				fileName += ".chess";

			using (FileStream fs = new FileStream (FileName, FileMode.Create)) {
				using (StreamWriter sw = new StreamWriter (fs)) {
					foreach (string s in StockfishMoves) {
						sw.WriteLine (s);
					}
				}
			}
			closeWindow (UI_Save);
		}
		void onSaveCancel (object sender, MouseButtonEventArgs e){
			closeWindow (UI_Save);
		}

		void onLoadClick (object sender, MouseButtonEventArgs e){
			loadWindow (UI_Load);
		}
		void onSelectedFileChanged(object sender, SelectionChangeEventArgs e){
			FileName = e.NewValue.ToString ();
		}
		void onDeleteFileClick (object sender, MouseButtonEventArgs e){
			File.Delete (FileName);
			NotifyValueChanged ("SavedGames", SavedGames);
		}
		void onLoadOkClick (object sender, MouseButtonEventArgs e){
			StockfishMoves.Clear ();
			using (FileStream fs = new FileStream (FileName, FileMode.Open)) {
				using (StreamReader sw = new StreamReader (fs)) {
					while (!sw.EndOfStream)
						StockfishMoves.Add(sw.ReadLine ());
				}
			}
			//save current pces pos
			List<Vector3> oldPositions = new List<Vector3>();
			foreach (ChessPlayer p in Players) {
				foreach (ChessPiece pce in p.Pieces) {
					oldPositions.Add (pce.Position);
				}
			}
			syncStockfish ();
			replaySilently ();
			int i = 0;
			foreach (ChessPlayer p in Players) {
				foreach (ChessPiece pce in p.Pieces) {
					if (oldPositions [i] != pce.Position) {
						GGL.Animation.StartAnimation (new GGL.PathAnimation (pce, "Position",
							new GGL.BezierPath (
								oldPositions [i],
								pce.Position, Vector3.UnitZ)));
					}
					i++;
				}
			}
			closeWindow (UI_Load);
		}
		void onLoadCancel (object sender, MouseButtonEventArgs e){
			closeWindow (UI_Load);
		}

		void onViewOptionsClick (object sender, MouseButtonEventArgs e){
			loadWindow(UI_Options);
		}
		void onViewFpsClick (object sender, MouseButtonEventArgs e){
			loadWindow(UI_Fps);
		}
		void onViewBoardClick (object sender, MouseButtonEventArgs e){
			loadWindow(UI_Board);
		}
		void onViewMovesClick (object sender, MouseButtonEventArgs e){
			loadWindow(UI_Moves);
		}
		void onViewLogsClick (object sender, MouseButtonEventArgs e){
			loadWindow(UI_Log);
		}
		void onQuitClick (object sender, MouseButtonEventArgs e){
			this.Exit();
		}
		void onHintClick (object sender, MouseButtonEventArgs e){
			if (CurrentPlayer.Type == PlayerType.Human)
				sendToStockfish("go");
		}
		void onUndoClick (object sender, MouseButtonEventArgs e){
			if (currentState != GameState.Checkmate && currentState != GameState.Pad)//undo ai move
				undoLastMove ();
			undoLastMove ();
		}
		void onResetClick (object sender, MouseButtonEventArgs e){
			loadWindow (UI_NewGame);
			resetBoard ();
			syncStockfish ();
		}
		void onNewWhiteGame (object sender, MouseButtonEventArgs e){
			closeWindow (UI_NewGame);
			viewZangle = 0;
			UpdateViewMatrix ();
			Players [0].Type = PlayerType.Human;
			Players [1].Type = PlayerType.AI;
			resetBoard ();
			syncStockfish ();
		}
		void onNewBlackGame (object sender, MouseButtonEventArgs e){
			closeWindow (UI_NewGame);
			viewZangle = MathHelper.Pi;
			UpdateViewMatrix ();
			Players [0].Type = PlayerType.AI;
			Players [1].Type = PlayerType.Human;
			resetBoard ();
			syncStockfish ();
			sendToStockfish("go");
		}
		void onPromoteToQueenClick (object sender, MouseButtonEventArgs e){
			deletePromoteDialog ();
			processMove (getChessCell (Active.X, Active.Y) + getChessCell (Selection.X, Selection.Y) + "q");
		}
		void onPromoteToBishopClick (object sender, MouseButtonEventArgs e){
			deletePromoteDialog ();
			processMove (getChessCell (Active.X, Active.Y) + getChessCell (Selection.X, Selection.Y) + "b");
		}
		void onPromoteToRookClick (object sender, MouseButtonEventArgs e){
			deletePromoteDialog ();
			processMove (getChessCell (Active.X, Active.Y) + getChessCell (Selection.X, Selection.Y) + "r");
		}
		void onPromoteToKnightClick (object sender, MouseButtonEventArgs e){
			deletePromoteDialog ();
			processMove (getChessCell (Active.X, Active.Y) + getChessCell (Selection.X, Selection.Y) + "k");
		}
		void showPromoteDialog(){
			loadWindow (UI_Promote);
		}
		void deletePromoteDialog(){
			closeWindow (UI_Promote);
		}
		void onViewAbout(object sender, EventArgs e){
			loadWindow (UI_About);
			AssemblyName infos = System.Reflection.Assembly.GetEntryAssembly ().GetName();
			NotifyValueChanged ("Name",  infos.Name);
			NotifyValueChanged ("Version", infos.Version.ToString());
			NotifyValueChanged ("Culture", infos.CultureName.ToString());
		}
		#endregion

		#region Stockfish
		Process stockfish;
		volatile bool waitAnimationFinished = false;
		volatile bool waitStockfishIsReady = false;
		Queue<string> stockfishCmdQueue = new Queue<string>();
		List<String> stockfishMoves = new List<string> ();

//		public bool StockfishRunning {
//			get { return stockfish != null; }
//		}
		public string StockfishPath{
			get { return Crow.Configuration.Get<string> ("StockfishPath"); }
			set {
				if (value == StockfishPath)
					return;
				Crow.Configuration.Set ("StockfishPath", value);
				NotifyValueChanged ("StockfishPath", value);

				initStockfish ();
			}
		}
		public int StockfishLevel{
			get { return Crow.Configuration.Get<int> ("Level"); }
			set
			{
				if (value == StockfishLevel)
					return;

				Crow.Configuration.Set ("Level", value);
				sendToStockfish ("setoption name Skill Level value " + value.ToString());
				NotifyValueChanged ("StockfishLevel", value);
			}
		}
		string stockfishPositionCommand {
			get {
				string tmp = "position startpos moves ";
				return
					StockfishMoves.Count == 0 ? tmp : tmp + StockfishMoves.Aggregate ((i, j) => i + " " + j); }
		}

		public bool AutoPlayHint {
			get { return Crow.Configuration.Get<bool> ("AutoPlayHint"); }
			set {
				if (value == AutoPlayHint)
					return;
				Crow.Configuration.Set ("AutoPlayHint", value);
				NotifyValueChanged ("AutoPlayHint", value);
			}
		}

		public List<String> StockfishMoves {
			get { return stockfishMoves; }
			set { stockfishMoves = value; }
		}

		void initStockfish()
		{
			if (!File.Exists (StockfishPath))
				return;

			if (stockfish != null) {
				resetBoard (false);

				stockfish.OutputDataReceived -= dataReceived;
				stockfish.ErrorDataReceived -= dataReceived;
				stockfish.Exited -= P_Exited;

				stockfish.Kill ();
			}

			stockfish = new Process ();
			stockfish.StartInfo.UseShellExecute = false;
			stockfish.StartInfo.RedirectStandardOutput = true;
			stockfish.StartInfo.RedirectStandardInput = true;
			stockfish.StartInfo.RedirectStandardError = true;
			stockfish.EnableRaisingEvents = true;
			stockfish.StartInfo.FileName = StockfishPath;
			stockfish.OutputDataReceived += dataReceived;
			stockfish.ErrorDataReceived += dataReceived;
			stockfish.Exited += P_Exited;
			stockfish.Start();

			//NotifyValueChanged ("StockfishRunning", true);
			//CrowInterface.FindByName ("SFStatus").Background = Color.Mantis;

			stockfish.BeginOutputReadLine ();

			sendToStockfish ("uci");
		}
		void syncStockfish(){
			NotifyValueChanged ("StockfishMoves", StockfishMoves);
			sendToStockfish (stockfishPositionCommand);
		}
		void askStockfishIsReady(){
			if (waitStockfishIsReady)
				return;
			waitStockfishIsReady = true;
			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine ("isready");
		}
		void sendToStockfish(string msg){
			stockfishCmdQueue.Enqueue(msg);
		}
		void P_Exited (object sender, EventArgs e)
		{
			AddLog ("Stockfish Terminated");
		}
		void dataReceived (object sender, DataReceivedEventArgs e)
		{
			if (string.IsNullOrEmpty (e.Data))
				return;

			string[] tmp = e.Data.Split (' ');

			if (tmp[0] != "readyok")
				AddLog (e.Data);

			switch (tmp[0]) {
			case "readyok":
				if (stockfishCmdQueue.Count == 0) {
					AddLog ("Error: no command on queue after readyok");
					return;
				}
				string cmd = stockfishCmdQueue.Dequeue ();
				AddLog ("=>" + cmd);
				stockfish.WaitForInputIdle ();
				stockfish.StandardInput.WriteLine (cmd);
				waitStockfishIsReady = false;
				return;
			case "uciok":
				sendToStockfish ("setoption name Skill Level value " + StockfishLevel.ToString());
				break;
			case "bestmove":
				if (tmp [1] == "(none)")
					return;
				if (CurrentState == GameState.Checkmate) {
					AddLog ("Error: received bestmove while game in Checkmate state");
					return;
				}
				bestMove = tmp [1];
				break;
			}
		}

		#endregion

		#region game logic

		ChessPiece[,] board;

		volatile GameState currentState = GameState.Init;
		int currentPlayerIndex = 0;
		Point selection;
		Point active = new Point(-1,-1);
		List<Point> ValidPositionsForActivePce = null;

		int cptWhiteOut = 0;
		int cptBlackOut = 0;

		volatile string bestMove;

		public static ChessPlayer[] Players;

		public ChessPiece[,] Board {
			get { return board; }
			set {
				board = value;
				NotifyValueChanged ("Board", board);
			}
		}
		public GameState CurrentState{
			get { return currentState; }
			set {
				if (currentState == value)
					return;
				currentState = value;
				NotifyValueChanged ("CurrentState", currentState);
			}
		}

		int CurrentPlayerIndex {
			get { return currentPlayerIndex; }
			set {
				currentPlayerIndex = value;
				NotifyValueChanged ("CurrentPlayer", CurrentPlayer);
			}
		}
		public ChessPlayer CurrentPlayer {
			get { return Players[CurrentPlayerIndex];}
			set {
				CurrentPlayerIndex = Array.IndexOf(Players, value);
			}
		}
		public ChessPlayer Opponent {
			get { return currentPlayerIndex == 0 ? Players [1] : Players [0]; }
		}
		public ChessPlayer Whites
		{ get { return Players [0]; } }
		public ChessPlayer Blacks
		{ get { return Players [1]; } }

		Point Active {
			get {
				return active;
			}
			set {
				active = value;
				if (active < 0)
					NotifyValueChanged ("ActCell", "" );
				else
					NotifyValueChanged ("ActCell", getChessCell(active.X,active.Y));

				if (Active < 0) {
					ValidPositionsForActivePce = null;
					return;
				}

				ValidPositionsForActivePce = new List<Point> ();

				foreach (string s in computeValidMove (Active)) {
					bool kingIsSafe = true;

					previewBoard (s);

					kingIsSafe = checkKingIsSafe ();

					restoreBoardAfterPreview ();

					if (kingIsSafe)
						addValidMove (getChessCell (s.Substring (2, 2)));
				}

				if (ValidPositionsForActivePce.Count == 0)
					ValidPositionsForActivePce = null;
			}
		}
		Point Selection {
			get {
				return selection;
			}
			set {
				selection = value;
				if (selection.X < 0)
					selection.X = 0;
				else if (selection.X > 7)
					selection.X = 7;
				if (selection.Y < 0)
					selection.Y = 0;
				else if (selection.Y > 7)
					selection.Y = 7;
				NotifyValueChanged ("SelCell", getChessCell(selection.X,selection.Y) );
			}
		}

		void initBoard(){
			CurrentPlayerIndex = 0;
			cptWhiteOut = 0;
			cptBlackOut = 0;
			StockfishMoves.Clear ();
			NotifyValueChanged ("StockfishMoves", StockfishMoves);

			Active = -1;

			Board = new ChessPiece[8, 8];

			for (int i = 0; i < 8; i++)
				addPiece (vaoiPawn, i, 0, PieceType.Pawn, i, 1);
			for (int i = 0; i < 8; i++)
				addPiece (vaoiPawn, i+8, 1, PieceType.Pawn, i, 6);

			addPiece (vaoiBishop, 0, 0, PieceType.Bishop, 2, 0);
			addPiece (vaoiBishop, 1, 0, PieceType.Bishop, 5, 0);
			addPiece (vaoiBishop, 2, 1, PieceType.Bishop, 2, 7);
			addPiece (vaoiBishop, 3, 1, PieceType.Bishop, 5, 7);

			addPiece (vaoiKnight, 0, 0, PieceType.Knight, 1, 0);
			addPiece (vaoiKnight, 1, 0, PieceType.Knight, 6, 0);
			addPiece (vaoiKnight, 2, 1, PieceType.Knight, 1, 7);
			addPiece (vaoiKnight, 3, 1, PieceType.Knight, 6, 7);

			addPiece (vaoiRook, 0, 0, PieceType.Rook, 0 ,0);
			addPiece (vaoiRook, 1, 0, PieceType.Rook, 7, 0);
			addPiece (vaoiRook, 2, 1, PieceType.Rook, 0, 7);
			addPiece (vaoiRook, 3, 1, PieceType.Rook, 7, 7);

			addPiece (vaoiQueen, 0, 0, PieceType.Queen, 3, 0);
			addPiece (vaoiQueen, 1, 1, PieceType.Queen, 3, 7);

			addPiece (vaoiKing, 0, 0, PieceType.King, 4, 0);
			addPiece (vaoiKing, 1, 1, PieceType.King, 4, 7);
		}
		void resetBoard(bool animate = true){
			CurrentState = GameState.Play;
			GraphicObject g = FindByName ("mateWin");
			if (g != null)
				ifaceControl[0].CrowInterface.DeleteWidget (g);
			CurrentPlayerIndex = 0;
			cptWhiteOut = 0;
			cptBlackOut = 0;
			StockfishMoves.Clear ();
			NotifyValueChanged ("StockfishMoves", StockfishMoves);

			Active = -1;
			Board = new ChessPiece[8, 8];
			foreach (ChessPlayer player in Players) {
				foreach (ChessPiece p in player.Pieces) {
					p.Reset (animate);
					Board [p.InitX, p.InitY] = p;
				}
			}
		}

		void addPiece(InstancedChessModel vaoi, int idx, int playerIndex, PieceType _type, int col, int line){
			ChessPiece p = new ChessPiece (vaoi, idx, Players[playerIndex], _type, col, line);
			Board [col, line] = p;
		}

		void addValidMove(Point p){
			if (ValidPositionsForActivePce.Contains (p))
				return;
			ValidPositionsForActivePce.Add (p);
		}

		bool checkKingIsSafe(){
			foreach (ChessPiece op in Opponent.Pieces) {
				if (op.Captured)
					continue;
				foreach (string opM in computeValidMove (op.BoardCell)) {
					if (opM.EndsWith ("K"))
						return false;
				}
			}
			return true;
		}
		string[] getLegalMoves(){

			List<String> legalMoves = new List<string> ();

			foreach (ChessPiece p in CurrentPlayer.Pieces) {
				if (p.Captured)
					continue;
				foreach (string s in computeValidMove (p.BoardCell)) {
					bool kingIsSafe = true;

					previewBoard (s);

					kingIsSafe = checkKingIsSafe ();

					restoreBoardAfterPreview ();

					if (kingIsSafe)
						legalMoves.Add(s);
				}
			}
			return legalMoves.ToArray ();
		}
		string[] checkSingleMove(Point pos, int xDelta, int yDelta){
			int x = pos.X + xDelta;
			int y = pos.Y + yDelta;

			if (x < 0 || x > 7 || y < 0 || y > 7)
				return null;

			if (Board [x, y] == null) {
				if (Board [pos.X, pos.Y].Type == PieceType.Pawn){
					if (xDelta != 0){
						//check En passant capturing
						int epY;
						string validEP;
						if (Board [pos.X, pos.Y].Player.Color == ChessColor.White) {
							epY = 4;
							validEP = getChessCell (x, 6) + getChessCell (x, 4);
						} else {
							epY = 3;
							validEP = getChessCell (x, 1) + getChessCell (x, 3);
						}
						if (pos.Y != epY)
							return null;
						if (Board [x, epY] == null)
							return null;
						if (Board [x, epY].Type != PieceType.Pawn)
							return null;
						if (StockfishMoves [StockfishMoves.Count-1] != validEP)
							return null;
						return new string[] { getChessCell (pos.X, pos.Y) + getChessCell (x, y) + "EP"};
					}
					//check pawn promotion
					if (y ==  Board [pos.X, pos.Y].Player.PawnPromotionY){
						string basicPawnMove = getChessCell (pos.X, pos.Y) + getChessCell (x, y);
						return new string[] {
							basicPawnMove + "q",
							basicPawnMove + "k",
							basicPawnMove + "r",
							basicPawnMove + "b"
						};
					}
				}
				return new string[] { getChessCell (pos.X, pos.Y) + getChessCell (x, y) };
			}

			if (Board [x, y].Player == Board [pos.X, pos.Y].Player)
				return null;
			if (Board [pos.X, pos.Y].Type == PieceType.Pawn && xDelta == 0)
				return null;//pawn cant take in front

			if (Board [x, y].Type == PieceType.King)
				return new string[] { getChessCell (pos.X, pos.Y) + getChessCell (x, y) + "K"};

			if (Board [pos.X, pos.Y].Type == PieceType.Pawn &&
				y ==  Board [pos.X, pos.Y].Player.PawnPromotionY){
				string basicPawnMove = getChessCell (pos.X, pos.Y) + getChessCell (x, y);
				return new string[] {
					basicPawnMove + "q",
					basicPawnMove + "k",
					basicPawnMove + "r",
					basicPawnMove + "b"
				};
			}

			return new string[] { getChessCell (pos.X, pos.Y) + getChessCell (x, y) };
		}
		string[] checkIncrementalMove(Point pos, int xDelta, int yDelta){

			List<string> legalMoves = new List<string> ();

			int x = pos.X + xDelta;
			int y = pos.Y + yDelta;

			string strStart = getChessCell(pos.X,pos.Y);

			while (x >= 0 && x < 8 && y >= 0 && y < 8) {
				if (Board [x, y] == null) {
					legalMoves.Add(strStart + getChessCell(x,y));
					x += xDelta;
					y += yDelta;
					continue;
				}

				if (Board [x, y].Player == Board [pos.X, pos.Y].Player)
					break;

				if (Board [x, y].Type == PieceType.King)
					legalMoves.Add(strStart + getChessCell(x,y) + "K");
				else
					legalMoves.Add(strStart + getChessCell(x,y));

				break;
			}
			return legalMoves.ToArray ();
		}
		string[] computeValidMove(Point pos){
			int x = pos.X;
			int y = pos.Y;

			ChessPiece p = Board [x, y];

			ChessMoves validMoves = new ChessMoves ();

			if (p != null) {
				switch (p.Type) {
				case PieceType.Pawn:
					int pawnDirection = 1;
					if (p.Player.Color == ChessColor.Black)
						pawnDirection = -1;
					validMoves.AddMove (checkSingleMove (pos, 0, 1 * pawnDirection));
					if (Board [x, y + pawnDirection] == null && !p.HasMoved)
						validMoves.AddMove (checkSingleMove (pos, 0, 2 * pawnDirection));
					validMoves.AddMove (checkSingleMove (pos, -1, 1 * pawnDirection));
					validMoves.AddMove (checkSingleMove (pos, 1, 1 * pawnDirection));
					break;
				case PieceType.Rook:
					validMoves.AddMove (checkIncrementalMove (pos, 0, 1));
					validMoves.AddMove (checkIncrementalMove (pos, 0, -1));
					validMoves.AddMove (checkIncrementalMove (pos, 1, 0));
					validMoves.AddMove (checkIncrementalMove (pos, -1, 0));
					break;
				case PieceType.Knight:
					validMoves.AddMove (checkSingleMove (pos, 2, 1));
					validMoves.AddMove (checkSingleMove (pos, 2, -1));
					validMoves.AddMove (checkSingleMove (pos, -2, 1));
					validMoves.AddMove (checkSingleMove (pos, -2, -1));
					validMoves.AddMove (checkSingleMove (pos, 1, 2));
					validMoves.AddMove (checkSingleMove (pos, -1, 2));
					validMoves.AddMove (checkSingleMove (pos, 1, -2));
					validMoves.AddMove (checkSingleMove (pos, -1, -2));
					break;
				case PieceType.Bishop:
					validMoves.AddMove (checkIncrementalMove (pos, 1, 1));
					validMoves.AddMove (checkIncrementalMove (pos, -1, -1));
					validMoves.AddMove (checkIncrementalMove (pos, 1, -1));
					validMoves.AddMove (checkIncrementalMove (pos, -1, 1));
					break;
				case PieceType.King:
					if (!p.HasMoved) {
						ChessPiece tower = Board [0, y];
						if (tower != null) {
							if (!tower.HasMoved) {
								for (int i = 1; i < x; i++) {
									if (Board [i, y] != null)
										break;
									if (i == x - 1)
										validMoves.Add (getChessCell (x, y) + getChessCell (x - 2, y));
								}
							}
						}
						tower = Board [7, y];
						if (tower != null) {
							if (!tower.HasMoved) {
								for (int i = x + 1; i < 7; i++) {
									if (Board [i, y] != null)
										break;
									if (i == 6)
										validMoves.Add (getChessCell (x, y) + getChessCell (x + 2, y));
								}
							}
						}
					}

					validMoves.AddMove (checkSingleMove (pos, -1, -1));
					validMoves.AddMove (checkSingleMove (pos, -1, 0));
					validMoves.AddMove (checkSingleMove (pos, -1, 1));
					validMoves.AddMove (checkSingleMove (pos, 0, -1));
					validMoves.AddMove (checkSingleMove (pos, 0, 1));
					validMoves.AddMove (checkSingleMove (pos, 1, -1));
					validMoves.AddMove (checkSingleMove (pos, 1, 0));
					validMoves.AddMove (checkSingleMove (pos, 1, 1));

					break;
				case PieceType.Queen:
					validMoves.AddMove (checkIncrementalMove (pos, 0, 1));
					validMoves.AddMove (checkIncrementalMove (pos, 0, -1));
					validMoves.AddMove (checkIncrementalMove (pos, 1, 0));
					validMoves.AddMove (checkIncrementalMove (pos, -1, 0));
					validMoves.AddMove (checkIncrementalMove (pos, 1, 1));
					validMoves.AddMove (checkIncrementalMove (pos, -1, -1));
					validMoves.AddMove (checkIncrementalMove (pos, 1, -1));
					validMoves.AddMove (checkIncrementalMove (pos, -1, 1));
					break;
				}
			}
			return validMoves.ToArray ();
		}

		string preview_Move;
		bool preview_MoveState;
		bool preview_wasPromoted;
		ChessPiece preview_Captured;

		void previewBoard(string move){
			if (move.EndsWith ("K")) {
				AddLog ("Previewing: " + move);
				move = move.Substring (0, 4);
			}

			preview_Move = move;

			Point pStart = getChessCell(preview_Move.Substring(0,2));
			Point pEnd = getChessCell(preview_Move.Substring(2,2));
			ChessPiece p = Board [pStart.X, pStart.Y];

			//pawn promotion
			if (preview_Move.Length == 5) {
				p.Promote (preview_Move [4],true);
				preview_wasPromoted = true;
			}else
				preview_wasPromoted = false;

			preview_MoveState = p.HasMoved;
			Board [pStart.X, pStart.Y] = null;
			p.HasMoved = true;

			//pawn en passant
			if (preview_Move.Length == 6)
				preview_Captured = Board [pEnd.X, pStart.Y];
			else
				preview_Captured = Board [pEnd.X, pEnd.Y];

			if (preview_Captured != null)
				preview_Captured.Captured = true;

			Board [pEnd.X, pEnd.Y] = p;
		}
		void restoreBoardAfterPreview(){
			Point pStart = getChessCell(preview_Move.Substring(0,2));
			Point pEnd = getChessCell(preview_Move.Substring(2,2));
			ChessPiece p = Board [pEnd.X, pEnd.Y];
			p.HasMoved = preview_MoveState;
			if (preview_wasPromoted)
				p.Unpromote ();
			if (preview_Captured != null)
				preview_Captured.Captured = false;
			Board [pStart.X, pStart.Y] = p;
			Board [pEnd.X, pEnd.Y] = null;
			if (preview_Move.Length == 6)
				Board [pEnd.X, pStart.Y] = preview_Captured;
			else
				Board [pEnd.X, pEnd.Y] = preview_Captured;
			preview_Move = null;
			preview_Captured = null;
		}

		string getChessCell(int col, int line){
			char c = (char)(col + 97);
			return c.ToString () + (line + 1).ToString ();
		}
		Point getChessCell(string s){
			return new Point ((int)s [0] - 97, int.Parse (s [1].ToString ()) - 1);
		}
		Vector3 getCurrentCapturePosition(ChessPiece p){
			float x, y;
			if (p.Player.Color == ChessColor.White) {
				x = -1.0f;
				y = 6.5f - (float)cptWhiteOut*0.7f;
				if (cptWhiteOut > 7) {
					x -= 0.7f;
					y += 8f*0.7f;
				}
			} else {
				x = 9.0f;
				y = 1.5f + (float)cptBlackOut*0.7f;
				if (cptBlackOut > 7) {
					x += 0.7f;
					y -= 8f*0.7f;
				}
			}
			return new Vector3 (x, y, -0.25f);
		}

		void capturePiece(ChessPiece p, bool animate = true){
			Point pos = p.BoardCell;
			Board [pos.X, pos.Y] = null;

			Vector3 capturePos = getCurrentCapturePosition (p);

			if (p.Player.Color == ChessColor.White)
				cptWhiteOut++;
			else
				cptBlackOut++;

			p.Captured = true;
			p.HasMoved = true;

			if (animate)
				GGL.Animation.StartAnimation (new GGL.PathAnimation (p, "Position",
					new GGL.BezierPath (
						p.Position,
						capturePos, Vector3.UnitZ)));
			else
				p.Position = capturePos;
		}

		void processMove(string move, bool animate = true){
			if (waitAnimationFinished)
				return;
			if (string.IsNullOrEmpty (move))
				return;
			if (move == "(none)")
				return;

			Point pStart = getChessCell(move.Substring(0,2));
			Point pEnd = getChessCell(move.Substring(2,2));

			ChessPiece p = Board [pStart.X, pStart.Y];
			if (p == null) {
				AddLog ("ERROR: impossible move.");
				return;
			}
			bool enPassant = false;
			if (p.Type == PieceType.Pawn && pStart.X != pEnd.X && Board[pEnd.X, pEnd.Y] == null)
				enPassant = true;

			StockfishMoves.Add (move);
			NotifyValueChanged ("StockfishMoves", StockfishMoves);

			Board [pStart.X, pStart.Y] = null;
			Point pTarget = pEnd;
			if (enPassant)
				pTarget.Y = pStart.Y;
			if (Board[pTarget.X, pTarget.Y] != null)
				capturePiece (Board[pTarget.X, pTarget.Y], animate);
			Board [pEnd.X, pEnd.Y] = p;
			p.HasMoved = true;

			Vector3 targetPosition = new Vector3 (pEnd.X + 0.5f, pEnd.Y + 0.5f, 0f);
			if (animate) {
				GGL.Animation.StartAnimation (new GGL.PathAnimation (p, "Position",
					new GGL.BezierPath (
						p.Position,
						targetPosition, Vector3.UnitZ)),
					0, move_AnimationFinished);
				waitAnimationFinished = true;
			}else
				p.Position = targetPosition;

			Active = -1;

			if (!enPassant) {
				//check if rockMove
				if (p.Type == PieceType.King) {
					int xDelta = pStart.X - pEnd.X;
					if (Math.Abs (xDelta) == 2) {
						//rocking
						if (xDelta > 0) {
							pStart.X = 0;
							pEnd.X = pEnd.X + 1;
						} else {
							pStart.X = 7;
							pEnd.X = pEnd.X - 1;
						}
						p = Board [pStart.X, pStart.Y];
						Board [pStart.X, pStart.Y] = null;
						Board [pEnd.X, pEnd.Y] = p;
						p.HasMoved = true;

						targetPosition = new Vector3 (pEnd.X + 0.5f, pEnd.Y + 0.5f, 0f);
						if (animate)
							GGL.Animation.StartAnimation (new GGL.PathAnimation (p, "Position",
								new GGL.BezierPath (
									p.Position,
									targetPosition, Vector3.UnitZ * 2f)));
						else
							p.Position = targetPosition;
					}
				}

				//check promotion
				if (move.Length == 5)
					p.Promote (move [4]);
			}
			NotifyValueChanged ("Board", board);
		}

		void undoLastMove()
		{
			if (StockfishMoves.Count == 0)
				return;

			string move = StockfishMoves [StockfishMoves.Count - 1];
			StockfishMoves.RemoveAt (StockfishMoves.Count - 1);

			Point pPreviousPos = getChessCell(move.Substring(0,2));
			Point pCurPos = getChessCell(move.Substring(2,2));

			ChessPiece p = Board [pCurPos.X, pCurPos.Y];

			replaySilently ();

			p.Position = new Vector3(pCurPos.X + 0.5f, pCurPos.Y + 0.5f, 0f);

			GGL.Animation.StartAnimation (new GGL.PathAnimation (p, "Position",
				new GGL.BezierPath (
					p.Position,
					new Vector3(pPreviousPos.X + 0.5f, pPreviousPos.Y + 0.5f, 0f), Vector3.UnitZ)));

			syncStockfish ();

			//animate undo capture
			ChessPiece pCaptured = Board [pCurPos.X, pCurPos.Y];
			if (pCaptured == null)
				return;
			Vector3 pCapLastPos = pCaptured.Position;
			pCaptured.Position = getCurrentCapturePosition (pCaptured);

			GGL.Animation.StartAnimation (new GGL.PathAnimation (pCaptured, "Position",
				new GGL.BezierPath (
					pCaptured.Position,
					pCapLastPos, Vector3.UnitZ)));

		}
		void replaySilently(){
			string[] moves = StockfishMoves.ToArray ();
			resetBoard (false);
			foreach (string m in moves) {
				processMove (m, false);
				if (currentPlayerIndex == 0)
					currentPlayerIndex = 1;
				else
					currentPlayerIndex = 0;
			}
			CurrentPlayerIndex = currentPlayerIndex;
		}

		void switchPlayer(){
			bestMove = null;
			clearArrows ();

			if (CurrentPlayerIndex == 0)
				CurrentPlayerIndex = 1;
			else
				CurrentPlayerIndex = 0;

			syncStockfish ();

			if (CurrentPlayer.Type == PlayerType.AI)
				sendToStockfish("go");
		}

		void move_AnimationFinished (GGL.Animation a)
		{
			waitAnimationFinished = false;

			switchPlayer ();

			bool kingIsSafe = checkKingIsSafe ();
			if (getLegalMoves ().Length == 0) {
				if (kingIsSafe)
					CurrentState = GameState.Pad;
				else {
					CurrentState = GameState.Checkmate;
					GraphicObject g = Load ("#Chess.gui.checkmate.crow");
					g.DataSource = this;
					GGL.Animation.StartAnimation (new GGL.FloatAnimation (CurrentPlayer.King, "Z", 0.4f, 0.04f));
					GGL.Animation.StartAnimation (new GGL.AngleAnimation (CurrentPlayer.King, "XAngle", MathHelper.Pi * 0.55f, 0.09f));
					GGL.Animation.StartAnimation (new GGL.AngleAnimation (CurrentPlayer.King, "ZAngle", CurrentPlayer.King.ZAngle + 0.3f, 0.5f));
				}
			}else if (kingIsSafe)
				CurrentState = GameState.Play;
			else
				CurrentState = GameState.Checked;
		}

		#endregion

		#region OTK window overrides
		protected override void OnLoad (EventArgs e)
		{
			Players = new ChessPlayer[2] {
				new ChessPlayer () { Color = ChessColor.White, Type = PlayerType.Human },
				new ChessPlayer () { Color = ChessColor.Black, Type = PlayerType.AI }
			};

			base.OnLoad (e);

			initOpenGL ();

			loadWindow (UI_Splash);

			Thread t = new Thread (loadMeshes);
			t.IsBackground = true;
			t.Start ();
		}
		public override void GLClear ()
		{
			GL.ClearColor (clearColor [0], clearColor [1], clearColor [2], clearColor [3]);
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
		}
		public override void OnRender (FrameEventArgs e)
		{
			if (CurrentState < GameState.Play)
				return;
			draw ();
		}
		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			UpdateViewMatrix();
			if (Reflexion)
				initReflexionFbo ();
		}

		void addCellLight(Vector4 cellColor, Point pos){
			cellVAOItem.AddInstance (Matrix4.CreateTranslation (0.5f + (float)pos.X, 0.5f + (float)pos.Y, 0), cellColor);
		}
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

			if (shaderMatsAreDirty)
				updateShadersMatrices ();
			//stockfish
			if (stockfishCmdQueue.Count > 0)
				askStockfishIsReady ();

			switch (CurrentState) {
			case GameState.Init:
			case GameState.MeshesLoading:
			case GameState.ComputeTangents:
				ProgressValue++;
				return;
			case GameState.VAOInit:

				createMainVAO ();

				CurrentState = GameState.ComputeTangents;

				Thread t = new Thread (computeTangents);
				t.IsBackground = true;
				t.Start ();
				return;
			case GameState.BuildBuffers:
				initBoard ();
				initInterface ();
				initStockfish ();
				CurrentState = GameState.Play;
				break;
			case GameState.Play:
			case GameState.Checked:
				if (stockfish == null)
					return;
				if (string.IsNullOrEmpty (bestMove))
					break;
				if (CurrentPlayer.Type == PlayerType.Human) {
					if (!AutoPlayHint) {
						createArrows (bestMove);
						break;
					}
				}
				clearArrows ();
				processMove (bestMove);
				bestMove = null;
				break;
			}

			#region cell lighting
			cellVAOItem.SetModelMat (0, Matrix4.CreateTranslation(0.5f + (float)selection.X, 0.5f + (float)selection.Y, 0));
			if (ValidPositionsForActivePce != null){
				for (int i = 1; i < cellVAOItem.Datas.Length; i++)
					cellVAOItem.RemoveInstance (i);
				foreach (Point vm in ValidPositionsForActivePce)
					addCellLight (validPosColor, vm);
			}else
				for (int i = 1; i < cellVAOItem.Datas.Length; i++)
					cellVAOItem.RemoveInstance (i);
			if (active >= 0)
				addCellLight (activeColor, Active);

			if (CurrentState > GameState.Play)
				addCellLight(kingCheckedColor, CurrentPlayer.King.BoardCell);
			#endregion

			GGL.Animation.ProcessAnimations ();

			foreach (ChessPlayer p in Players) {
				foreach (ChessPiece pce in p.Pieces) {
					pce.SyncGL ();
				}
			}
			if (Reflexion)
				updateReflexionFbo ();
		}
//		protected override void OnClosing (System.ComponentModel.CancelEventArgs e)
//		{
//			closeGame ();
//			base.OnClosing (e);
//		}
		#endregion

		#region vLookCalculations
		public void UpdateViewMatrix()
		{
			Rectangle r = this.ClientRectangle;
			GL.Viewport( r.X, r.Y, r.Width, r.Height);
			projection = Matrix4.CreatePerspectiveFieldOfView (fovY, r.Width / (float)r.Height, zNear, zFar);
			vLook = vLookInit.Transform(
				Matrix4.CreateRotationX (viewXangle)*
				Matrix4.CreateRotationZ (viewZangle));
			vLook.Normalize();
			vEye = vEyeTarget + vLook * eyeDist;
			modelview = Matrix4.LookAt(vEye, vEyeTarget, Vector3.UnitZ);
			GL.GetInteger(GetPName.Viewport, viewport);
			invMVP = Matrix4.Invert(modelview) * Matrix4.Invert(projection);
			reflectedModelview =
				Matrix4.CreateScale (1.0f, 1.0f, -1.0f) * modelview;
			//Matrix4.CreateTranslation (0.0f, 0.0f, 1.0f) *
			shaderMatsAreDirty = true;
		}
		#endregion

		#region Keyboard
		void MainWin_KeyboardKeyDown (object sender, OpenTK.Input.KeyboardKeyEventArgs e)
		{
			switch (e.Key) {
			case OpenTK.Input.Key.Space:
				break;
			case OpenTK.Input.Key.Escape:
				Active = -1;
				break;

			}
		}

		#endregion

		#region Mouse
		void Mouse_ButtonDown (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{
			if (e.Mouse.LeftButton != OpenTK.Input.ButtonState.Pressed)
				return;

			clearArrows ();

			if (CurrentState == GameState.Checkmate) {
				Active = -1;
				return;
			}

			if (Active < 0) {
				ChessPiece p = Board [Selection.X, Selection.Y];
				if (p == null)
					return;
				if (p.Player != CurrentPlayer)
					return;
				Active = Selection;
			} else if (Selection == Active) {
				Active = -1;
				return;
			} else {
				ChessPiece p = Board [Selection.X, Selection.Y];
				if (p != null) {
					if (p.Player == CurrentPlayer) {
						Active = Selection;
						return;
					}
				}

				//move
				if (ValidPositionsForActivePce == null)
					return;
				if (ValidPositionsForActivePce.Contains (Selection)) {
					//check for promotion
					ChessPiece mp = Board [Active.X, Active.Y];
					if (mp.Type == PieceType.Pawn && Selection.Y == mp.Player.PawnPromotionY) {
						showPromoteDialog ();
					}else
						processMove (getChessCell (Active.X, Active.Y) + getChessCell (Selection.X, Selection.Y));
				}
			}
		}
		void Mouse_Move(object sender, OpenTK.Input.MouseMoveEventArgs e)
		{
			if (e.XDelta != 0 || e.YDelta != 0)
			{
				if (e.Mouse.MiddleButton == OpenTK.Input.ButtonState.Pressed) {
					viewZangle -= (float)e.XDelta * RotationSpeed;
					viewXangle -= (float)e.YDelta * RotationSpeed;
					if (viewXangle < - 0.75f)
						viewXangle = -0.75f;
					else if (viewXangle > MathHelper.PiOver4)
						viewXangle = MathHelper.PiOver4;
					UpdateViewMatrix ();
				}else if (e.Mouse.LeftButton == OpenTK.Input.ButtonState.Pressed) {
					return;
				}else if (e.Mouse.RightButton == OpenTK.Input.ButtonState.Pressed) {
					Vector2 v2Look = vLook.Xy.Normalized ();
					Vector2 disp = v2Look.PerpendicularLeft * e.XDelta * MoveSpeed +
					               v2Look * e.YDelta * MoveSpeed;
					vEyeTarget -= new Vector3 (disp.X, disp.Y, 0);
					UpdateViewMatrix();
				}
				Vector3 vMouse = GGL.glHelper.UnProject(ref projection, ref modelview, viewport, new Vector2 (e.X, e.Y)).Xyz;
				Vector3 vMouseRay = Vector3.Normalize(vMouse - vEye);
				float a = vEye.Z / vMouseRay.Z;
				vMouse = vEye - vMouseRay * a;
				Point newPos = new Point ((int)Math.Truncate (vMouse.X), (int)Math.Truncate (vMouse.Y));
				Selection = newPos;
			}

		}
		void Mouse_WheelChanged(object sender, OpenTK.Input.MouseWheelEventArgs e)
		{
			float speed = ZoomSpeed;
			if (Keyboard[OpenTK.Input.Key.ShiftLeft])
				speed *= 0.1f;
			else if (Keyboard[OpenTK.Input.Key.ControlLeft])
				speed *= 20.0f;

			eyeDistTarget -= e.Delta * speed;
			if (eyeDistTarget < zNear+1)
				eyeDistTarget = zNear+1;
			else if (eyeDistTarget > zFar-6)
				eyeDistTarget = zFar-6;

			GGL.Animation.StartAnimation(new GGL.Animation<float> (this, "EyeDist", eyeDistTarget, (eyeDistTarget - eyeDist) * 0.1f));
		}
		#endregion

		#region CTOR and Main
		public MainWin ()
			: base(700, 600, "Chess", 32, 24, 1, Crow.Configuration.Get<int> ("Samples"))
		{}

		[STAThread]
		static void Main ()
		{
			using (MainWin win = new MainWin( )) {
				win.VSync = VSyncMode.Off;
				win.Run (30.0);
			}
		}
		#endregion
	}
}
