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
using GGL;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using Tetra;
using System.IO;
using Tetra.DynamicShading;

namespace Chess
{
	public enum GameState { Init, MeshesLoading, VAOInit, ComputeTangents, BuildBuffers, Play, Checked, Pad, Checkmate };
	public enum PlayerType { Human, AI };
	public enum ChessColor { White, Black };
	public enum PieceType { Pawn, Rook, Knight, Bishop, King, Queen };

	class MainWin : OpenTKGameWindow, IBindable
	{
		#region IBindable implementation
		List<Binding> bindings = new List<Binding> ();
		public List<Binding> Bindings {
			get { return bindings; }
		}
		public object DataSource {
			get {
				throw new NotImplementedException ();
			}
			set {
				throw new NotImplementedException ();
			}
		}
		#endregion

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
		public Vector3 vEyeTarget = new Vector3(4f, 4f, 0f);
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
		#endregion

		#region GL

		UBOSharedData shaderSharedData;
		int uboShaderSharedData;

		Mesh meshPawn;
		Mesh meshBishop;
		Mesh meshHorse;
		Mesh meshTower;
		Mesh meshQueen;
		Mesh meshKing;
		Mesh meshBoard;
		int[] piecesVAOIndexes;

		public static Mat4InstancedShader piecesShader;

		public static VertexArrayObject<MeshData, VAOChessData> mainVAO;
		public static VAOItem<VAOChessData> boardVAOItem;
		public static VAOItem<VAOChessData> boardPlateVAOItem;
		public static VAOItem<VAOChessData> cellVAOItem;
		public static VAOItem<VAOChessData> vaoiPawn;
		public static VAOItem<VAOChessData> vaoiBishop;
		public static VAOItem<VAOChessData> vaoiKnight;
		public static VAOItem<VAOChessData> vaoiRook;
		public static VAOItem<VAOChessData> vaoiQueen;
		public static VAOItem<VAOChessData> vaoiKing;
		public static VAOItem<VAOChessData> vaoiQuad;//full screen quad in mainVAO to prevent unbind
													//while drawing reflexion
		Vector4 validPosColor = new Vector4 (0.0f, 0.5f, 0.7f, 0.7f);
		Vector4 activeColor = new Vector4 (0.2f, 0.2f, 1.0f, 0.6f);
		Vector4 kingCheckedColor = new Vector4 (1.0f, 0.2f, 0.2f, 0.6f);

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
			GL.Enable (EnableCap.CullFace);
			GL.CullFace (CullFaceMode.Back);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);
			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			piecesShader = new Mat4InstancedShader();

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
		void updateShadersMatrices(){
			shaderSharedData.projection = projection;
			shaderSharedData.modelview = modelview;
			shaderSharedData.normal = modelview.Inverted();
			shaderSharedData.normal.Transpose ();
			shaderSharedData.LightPosition = Vector4.Transform(vLight, modelview);

			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}
		void changeShadingColor(Vector4 color){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes,
				ref color);
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
			CurrentState = GameState.MeshesLoading;

			meshPawn = OBJMeshLoader.Load ("#Chess.Meshes.pawn.obj");
			ProgressValue+=20;
			meshBishop = OBJMeshLoader.Load ("#Chess.Meshes.bishop.obj");
			ProgressValue+=20;
			meshHorse = OBJMeshLoader.Load ("#Chess.Meshes.horse.obj");
			ProgressValue+=20;
			meshTower = OBJMeshLoader.Load ("#Chess.Meshes.tower.obj");
			ProgressValue+=20;
			meshQueen = OBJMeshLoader.Load ("#Chess.Meshes.queen.obj");
			ProgressValue+=20;
			meshKing = OBJMeshLoader.Load ("#Chess.Meshes.king.obj");
			ProgressValue+=20;
			meshBoard = OBJMeshLoader.Load ("#Chess.Meshes.board.obj");
			ProgressValue+=20;

			CurrentState = GameState.VAOInit;
		}
		void createMainVAO(){			
			mainVAO = new VertexArrayObject<MeshData, VAOChessData> ();

			float
			x = 4f,
			y = 4f,
			width = 8f,
			height = 8f;
			vaoiQuad = (VAOItem<VAOChessData>)mainVAO.Add (Mesh.CreateQuad (0, 0, 0, 1, 1, 1, 1));
			vaoiQuad.InstancedDatas = new VAOChessData[1];
			vaoiQuad.InstancedDatas[0].modelMats = Matrix4.Identity;
			vaoiQuad.InstancedDatas[0].color = new Vector4(1.0f,1.0f,1.0f,0.3f);

			vaoiQuad.UpdateInstancesData ();

			boardPlateVAOItem = (VAOItem<VAOChessData>)mainVAO.Add (new Mesh<MeshData> (
				new Vector3[] {
					new Vector3 (x - width / 2f, y + height / 2f, 0f),
					new Vector3 (x - width / 2f, y - height / 2f, 0f),
					new Vector3 (x + width / 2f, y + height / 2f, 0f),
					new Vector3 (x + width / 2f, y - height / 2f, 0f)
				},
				new MeshData(
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
				), new ushort[] { 0, 1, 2, 2, 1, 3 }));
			//boardPlateVAOItem.DiffuseTexture = new GGL.Texture ("#Chess.Textures.board1.png");
			boardPlateVAOItem.DiffuseTexture = new GGL.Texture ("#Chess.Textures.board3.png");
			//boardPlateVAOItem.DiffuseTexture = new GGL.Texture ("#Chess.Textures.marble2.jpg");
			boardPlateVAOItem.InstancedDatas = new VAOChessData[1];
			boardPlateVAOItem.InstancedDatas[0].modelMats = Matrix4.Identity;
			boardPlateVAOItem.InstancedDatas[0].color = new Vector4(0.5f,0.5f,0.5f,1f);

			boardPlateVAOItem.UpdateInstancesData ();

			x = 0f;
			y = 0f;
			width = 1.0f;
			height = 1.0f;

			cellVAOItem = (VAOItem<VAOChessData>)mainVAO.Add (new Mesh<MeshData>(
				new Vector3[] {
					new Vector3 (x - width / 2f, y + height / 2f, 0f),
					new Vector3 (x - width / 2f, y - height / 2f, 0f),
					new Vector3 (x + width / 2f, y + height / 2f, 0f),
					new Vector3 (x + width / 2f, y - height / 2f, 0f)
				},
				new MeshData(
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
				), new ushort[] { 0, 1, 2, 2, 1, 3 }));
			cellVAOItem.DiffuseTexture = new GGL.Texture ("Textures/marble.jpg");
			cellVAOItem.InstancedDatas = new VAOChessData[1];
			cellVAOItem.InstancedDatas[0].modelMats = Matrix4.CreateTranslation (new Vector3 (4.5f, 4.5f, 0f));
			cellVAOItem.InstancedDatas [0].color = new Vector4 (0.3f, 1.0f, 0.3f, 0.5f);
			cellVAOItem.UpdateInstancesData ();

			Tetra.Texture.GenerateMipMaps = true;
			Tetra.Texture.DefaultMinFilter = TextureMinFilter.LinearMipmapLinear;
			Tetra.Texture.DefaultMagFilter = TextureMagFilter.Linear;
			Tetra.Texture.DefaultWrapMode = TextureWrapMode.ClampToBorder;

			boardVAOItem = (VAOItem<VAOChessData>)mainVAO.Add (meshBoard);
			boardVAOItem.DiffuseTexture = new GGL.Texture ("#Chess.Textures.marble1.jpg");
			boardVAOItem.InstancedDatas = new VAOChessData[1];
			boardVAOItem.InstancedDatas [0].modelMats = Matrix4.CreateTranslation (4f, 4f, -0.20f);
			boardVAOItem.InstancedDatas[0].color = new Vector4(0.4f,0.4f,0.42f,1f);
			boardVAOItem.UpdateInstancesData ();

			List<int> tmp = new List<int> ();

			vaoiPawn = (VAOItem<VAOChessData>)mainVAO.Add (meshPawn);
			vaoiPawn.DiffuseTexture = new GGL.Texture ("Textures/pawn_backed.png");
			vaoiPawn.InstancedDatas = new VAOChessData[16];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiPawn));
			ProgressValue++;

			vaoiBishop = (VAOItem<VAOChessData>)mainVAO.Add (meshBishop);
			vaoiBishop.DiffuseTexture = new GGL.Texture ("Textures/bishop_backed.png");
			vaoiBishop.InstancedDatas = new VAOChessData[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiBishop));
			ProgressValue++;

			vaoiKnight = (VAOItem<VAOChessData>)mainVAO.Add (meshHorse);
			vaoiKnight.DiffuseTexture = new GGL.Texture ("Textures/horse_backed.png");
			vaoiKnight.InstancedDatas = new VAOChessData[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiKnight));
			ProgressValue++;

			vaoiRook = (VAOItem<VAOChessData>)mainVAO.Add (meshTower);
			vaoiRook.DiffuseTexture = new GGL.Texture ("Textures/tower_backed.png");
			vaoiRook.InstancedDatas = new VAOChessData[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiRook));
			ProgressValue++;

			vaoiQueen = (VAOItem<VAOChessData>)mainVAO.Add (meshQueen);
			vaoiQueen.DiffuseTexture = new GGL.Texture ("Textures/queen_backed.png");
			vaoiQueen.InstancedDatas = new VAOChessData[2];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiQueen));
			ProgressValue++;

			vaoiKing = (VAOItem<VAOChessData>)mainVAO.Add (meshKing);
			vaoiKing.DiffuseTexture = new GGL.Texture ("Textures/king_backed.png");
			vaoiKing.InstancedDatas = new VAOChessData[2];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiKing));
			ProgressValue++;

			piecesVAOIndexes = tmp.ToArray ();

			Tetra.Texture.ResetToDefaultLoadingParams ();

			meshPawn = null;
			meshBishop = null;
			meshHorse = null;
			meshTower = null;
			meshQueen = null;
			meshKing = null;
		}
		void computeTangents(){
			//mainVAO.ComputeTangents();
			CurrentState = GameState.BuildBuffers;
		}

		void draw()
		{
			piecesShader.Enable ();
			piecesShader.SetLightingPass ();

			mainVAO.Bind ();

			changeShadingColor(new Vector4(1.0f,1.0f,1.0f,1.0f));

			mainVAO.Render (PrimitiveType.Triangles, boardVAOItem);

			if (Reflexion) {
				GL.Enable (EnableCap.StencilTest);

				//cut stencil
				GL.StencilFunc (StencilFunction.Always, 1, 0xff);
				GL.StencilOp (StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
				GL.StencilMask (0xff);
				GL.DepthMask (false);

				mainVAO.Render (PrimitiveType.Triangles, boardPlateVAOItem);

				//draw reflected items
				GL.StencilFunc (StencilFunction.Equal, 1, 0xff);
				GL.StencilMask (0x00);

				drawReflexion ();

				GL.Disable(EnableCap.StencilTest);
				GL.DepthMask (true);
			}else
				mainVAO.Render (PrimitiveType.Triangles, boardPlateVAOItem);

			//draw scene

			#region sel squarres
			GL.Disable (EnableCap.DepthTest);

			mainVAO.Render (PrimitiveType.Triangles, cellVAOItem);

			GL.Enable (EnableCap.DepthTest);
			#endregion

			mainVAO.Render (PrimitiveType.Triangles, piecesVAOIndexes);

			mainVAO.Unbind ();

			piecesShader.SetSimpleColorPass ();
			changeShadingColor (new Vector4 (0.2f, 1.0f, 0.2f, 0.5f));
			renderArrow ();

			GL.StencilMask (0xff);
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
			arrows = new Arrow3d (
				new Vector3 ((float)pStart.X + 0.5f, (float)pStart.Y + 0.5f, 0),
				new Vector3 ((float)pEnd.X + 0.5f, (float)pEnd.Y + 0.5f, 0),
				Vector3.UnitZ);
		}
		void renderArrow(){
			if (arrows == null)
				return;			

			GL.Disable (EnableCap.CullFace);
			arrows.Render (PrimitiveType.TriangleStrip);
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

			changeShadingColor(new Vector4(1.0f,1.0f,1.0f,1.0f));

			GL.BindFramebuffer(FramebufferTarget.Framebuffer, fboReflexion);

			GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
			GL.CullFace(CullFaceMode.Front);
			changeModelView (reflectedModelview);
			mainVAO.Render (PrimitiveType.Triangles, piecesVAOIndexes);
			changeModelView (modelview);
			GL.CullFace(CullFaceMode.Back);
			GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
			//GL.DrawBuffer(DrawBufferMode.Back);
			mainVAO.Unbind ();
		}
		void drawReflexion(){
			piecesShader.SetSimpleTexturedPass ();
			changeMVP (orthoMat, Matrix4.Identity);
			vaoiQuad.DiffuseTexture = reflexionTex;
			mainVAO.Render (PrimitiveType.TriangleStrip, vaoiQuad);
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
		string fileName = "savedgame.chess";

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
			GraphicObject g = CrowInterface.FindByName (path);
			if (g != null)
				return;
			g = CrowInterface.LoadInterface (path);
			g.Name = path;
			g.DataSource = this;

			Crow.CompilerServices.ResolveBindings (this.Bindings);
		}
		void closeWindow (string path){
			GraphicObject g = CrowInterface.FindByName (path);
			if (g != null)
				CrowInterface.DeleteWidget (g);
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
						Animation.StartAnimation (new PathAnimation (pce, "Position",
							new BezierPath (
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
			vLight = new Vector4 (0.5f, 0.5f, -1f, 0f);
			UpdateViewMatrix ();
			Players [0].Type = PlayerType.Human;
			Players [1].Type = PlayerType.AI;
			resetBoard ();
			syncStockfish ();
		}
		void onNewBlackGame (object sender, MouseButtonEventArgs e){
			closeWindow (UI_NewGame);
			viewZangle = MathHelper.Pi;
			vLight = new Vector4 (-0.5f, -0.5f, -1f, 0f);
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
		#endregion

		#region Stockfish
		Process stockfish;
		volatile bool waitAnimationFinished = false;
		volatile bool waitStockfishIsReady = false;
		Queue<string> stockfishCmdQueue = new Queue<string>();
		List<String> stockfishMoves = new List<string> ();

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
		public Vector3 Diffuse {
			get { return Crow.Configuration.Get<Vector3> ("Diffuse"); }
			set {
				if (value == Diffuse)
					return;
				Crow.Configuration.Set ("Diffuse", value);
				NotifyValueChanged ("Diffuse", value);
			}
		}
		public List<String> StockfishMoves {
			get { return stockfishMoves; }
			set { stockfishMoves = value; }
		}

		void initStockfish()
		{
			stockfish = new Process ();
			stockfish.StartInfo.UseShellExecute = false;
			stockfish.StartInfo.RedirectStandardOutput = true;
			stockfish.StartInfo.RedirectStandardInput = true;
			stockfish.StartInfo.RedirectStandardError = true;
			stockfish.EnableRaisingEvents = true;
			stockfish.StartInfo.FileName = @"/usr/games/stockfish";
			stockfish.OutputDataReceived += dataReceived;
			stockfish.ErrorDataReceived += dataReceived;
			stockfish.Exited += P_Exited;
			stockfish.Start();

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
			GraphicObject g = CrowInterface.FindByName ("mateWin");
			if (g != null)
				CrowInterface.DeleteWidget (g);
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

		void addPiece(VAOItem<VAOChessData> vaoi, int idx, int playerIndex, PieceType _type, int col, int line){
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
				Animation.StartAnimation (new PathAnimation (p, "Position",
					new BezierPath (
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
				Animation.StartAnimation (new PathAnimation (p, "Position",
					new BezierPath (
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
							Animation.StartAnimation (new PathAnimation (p, "Position",
								new BezierPath (
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

			Animation.StartAnimation (new PathAnimation (p, "Position",
				new BezierPath (
					p.Position,
					new Vector3(pPreviousPos.X + 0.5f, pPreviousPos.Y + 0.5f, 0f), Vector3.UnitZ)));

			syncStockfish ();

			//animate undo capture
			ChessPiece pCaptured = Board [pCurPos.X, pCurPos.Y];
			if (pCaptured == null)
				return;
			Vector3 pCapLastPos = pCaptured.Position;
			pCaptured.Position = getCurrentCapturePosition (pCaptured);

			Animation.StartAnimation (new PathAnimation (pCaptured, "Position",
				new BezierPath (
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

		void move_AnimationFinished (Animation a)
		{
			waitAnimationFinished = false;

			switchPlayer ();

			bool kingIsSafe = checkKingIsSafe ();
			if (getLegalMoves ().Length == 0) {
				if (kingIsSafe)
					CurrentState = GameState.Pad;
				else {
					CurrentState = GameState.Checkmate;
					GraphicObject g = CrowInterface.LoadInterface ("#Chess.gui.checkmate.crow");
					g.DataSource = this;
					Animation.StartAnimation (new AngleAnimation (CurrentPlayer.King, "XAngle", MathHelper.Pi * 0.53f));
					Animation.StartAnimation (new AngleAnimation (CurrentPlayer.King, "ZAngle", CurrentPlayer.King.ZAngle - MathHelper.Pi, 0.2f));
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

			initStockfish ();

			Thread t = new Thread (loadMeshes);
			t.IsBackground = true;
			t.Start ();
		}
		public override void GLClear ()
		{
			GL.ClearColor(0.5f, 0.5f, 0.6f, 1.0f);
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
			cellVAOItem.AddInstance (new VAOChessData () { 
				color = cellColor, 
				modelMats = Matrix4.CreateTranslation (0.5f + (float)pos.X, 0.5f + (float)pos.Y, 0)
			});
		}
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

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
				mainVAO.BuildBuffers ();
				initBoard ();
				initInterface ();
				CurrentState = GameState.Play;
				break;
			case GameState.Play:
			case GameState.Checked:
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
			cellVAOItem.InstancedDatas[0].modelMats = Matrix4.CreateTranslation(0.5f + (float)selection.X, 0.5f + (float)selection.Y, 0);
			if (ValidPositionsForActivePce != null){
				for (int i = 1; i < cellVAOItem.InstancedDatas.Length; i++)
					cellVAOItem.RemoveInstance (i);					
				foreach (Point vm in ValidPositionsForActivePce)
					addCellLight (validPosColor, vm);				
			}else
				for (int i = 1; i < cellVAOItem.InstancedDatas.Length; i++)
					cellVAOItem.RemoveInstance (i);
			if (active >= 0)
				addCellLight (activeColor, Active);					
			
			if (CurrentState > GameState.Play)
				addCellLight(kingCheckedColor, CurrentPlayer.King.BoardCell);

			cellVAOItem.UpdateInstancesData ();
			#endregion

			Animation.ProcessAnimations ();

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
			updateShadersMatrices ();
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
			CursorVisible = true;
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
					Matrix4 m = Matrix4.CreateTranslation (-e.XDelta*MoveSpeed, e.YDelta*MoveSpeed, 0);
					vEyeTarget = vEyeTarget.Transform (m);
					UpdateViewMatrix();
				}
				Vector3 vMouse = glHelper.UnProject(ref projection, ref modelview, viewport, new Vector2 (e.X, e.Y)).Xyz;
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

			//EyeDist = eyeDistTarget;
			Animation.StartAnimation(new Animation<float> (this, "EyeDist", eyeDistTarget, (eyeDistTarget - eyeDist) * 0.1f));
		}
		#endregion

		#region CTOR and Main
		public MainWin ()
			: base(1024, 800, "Chess", 32, 24, 1, 1)
		{}

		[STAThread]
		static void Main ()
		{
			using (MainWin win = new MainWin( )) {
				win.Run (30.0);
			}
		}
		#endregion
	}
}