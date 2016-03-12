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

namespace Chess
{
	enum GameState { Init, MeshesLoading, VAOInit, ComputeTangents, BuildBuffers, Play, Check, Checkmate};
	enum PlayerType { Human, AI };
	enum ChessColor { White, Black};
	enum PieceType { Pawn, Tower, Horse, Bishop, King, Queen };

	class ChessMoves : List<String>
	{
		public void AddMove(string move)
		{
			if (!string.IsNullOrEmpty (move))
				base.Add (move);
		}
		public void AddMove(string[] moves){
			if (moves == null)
				return;
			if (moves.Length > 0)
				base.AddRange (moves);
		}
	}

	class ChessPlayer{
		public string Name;
		public ChessColor Color;
		public PlayerType Type;
		public ChessPiece King;
		public List<ChessPiece> Pieces = new List<ChessPiece>();
		public Point[] ValidMoves;
		public string [] CheckMoves;

		public override string ToString ()
		{
			return Color.ToString();
		}
	}
	class ChessPiece{
		public VAOItem<VAOChessData> Mesh;
		public int InstanceIndex;
		public ChessPlayer Player;
		PieceType originalType;

		public PieceType Type {
			get {
				return originalType;
			}
			set {
				originalType = value;
			}
		}

		public bool HasMoved;
		public bool Captured;

		float x, y, z, xAngle;
		public int InitX, InitY;

		public Point BoardCell{
			get { return new Point ((int)Math.Truncate (X), (int)Math.Truncate (Y)); }
		}

		public Vector3 Position {
			get { return new Vector3 (x, y, z); }
			set {
				x = value.X;
				y = value.Y;
				z = value.Z;
				update ();
			}
		}

		public float X {
			get {return x;}
			set {
				x = value;
				update();
			}
		}
		public float Y {
			get {return y;}
			set {
				y = value;
				update();
			}
		}
		public float Z {
			get {return z;}
			set {
				z = value;
				update();
			}
		}
		public float XAngle {
			get {return xAngle;}
			set {
				xAngle = value;
				update();
			}
		}
		void update(){
			Mesh.Datas [InstanceIndex].modelMats =
				Matrix4.CreateRotationX(xAngle) *
				Matrix4.CreateTranslation(new Vector3(x, y, z));
			Mesh.UpdateInstancesData ();
		}
		public ChessPiece(VAOItem<VAOChessData> vaoi, int idx, ChessPlayer _player , PieceType _type, int xPos, int yPos){
			Mesh = vaoi;
			InstanceIndex = idx;
			Player = _player;
			Type = _type;
			InitX= xPos;
			InitY= yPos;
			x = xPos + 0.5f;
			y = yPos + 0.5f;
			z = 0f;
			xAngle = 0f;
			HasMoved = false;
			Captured = false;

			if (Player.Color == ChessColor.White)
				Mesh.Datas [InstanceIndex].color = new Vector4 (0.80f, 0.80f, 0.70f, 1f);
			else
				Mesh.Datas [InstanceIndex].color = new Vector4 (0.2f, 0.2f, 0.2f, 1f);

			Player.Pieces.Add (this);

			if (Type == PieceType.King)
				Player.King = this;

			update ();
		}
		public void Reset(bool animate = true){
			xAngle = 0f;
			z = 0f;
			if (HasMoved) {
				if (animate)
					Animation.StartAnimation (new PathAnimation (this, "Position",
						new BezierPath (
							Position,
							new Vector3 (InitX + 0.5f, InitY + 0.5f, 0f), Vector3.UnitZ)));
				else
					Position = new Vector3 (InitX + 0.5f, InitY + 0.5f, 0f);
			}

			HasMoved = false;
			Captured = false;
			update ();
		}	
	}
	class MainWin : OpenTKGameWindow
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct UBOSharedData
		{
			public Vector4 Color;
			public Matrix4 view;
			public Matrix4 projection;
			public Matrix4 normal;
			public Vector4 LightPosition;
		}

		#region  scene matrix and vectors
		public static Matrix4 modelview;
		public static Matrix4 reflectedModelview;
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
		public Vector3 vLook = Vector3.Normalize(new Vector3(0.0f, -0.7f, 0.7f));  // Camera vLook Vector
		public float zFar = 30.0f;
		public float zNear = 0.1f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 12f;
		float eyeDistTarget = 12f;
		float MoveSpeed = 0.02f;
		float RotationSpeed = 0.005f;
		float ZoomSpeed = 2f;

		//public Vector4 vLight = new Vector4 (0.5f, 0.5f, -1f, 0f);
		public Vector4 vLight = Vector4.Normalize(new Vector4 (0.4f, 0.4f, -0.5f, 0f));
		#endregion

		#region GL

		UBOSharedData shaderSharedData;
		int uboShaderSharedData;

		public static Mat4InstancedShader piecesShader;
		public static SimpleColoredShader coloredShader;

		Tetra.IndexedVAO<VAOChessData> mainVAO;
		VAOItem<VAOChessData> boardVAOItem;
		VAOItem<VAOChessData> cellVAOItem;
		int[] piecesVAOIndexes;

		void initOpenGL()
		{
			GL.ClearColor(0.0f, 0.0f, 0.2f, 1.0f);
			GL.Enable (EnableCap.CullFace);
			GL.CullFace (CullFaceMode.Back);
			GL.Enable(EnableCap.DepthTest);
			GL.DepthFunc(DepthFunction.Less);
			GL.PrimitiveRestartIndex (int.MaxValue);
			GL.Enable (EnableCap.PrimitiveRestart);
			GL.Enable (EnableCap.Blend);
			GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

			piecesShader = new Mat4InstancedShader();
			coloredShader = new SimpleColoredShader ();

			shaderSharedData.Color = new Vector4(1,1,1,1);
			uboShaderSharedData = GL.GenBuffer ();
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferData(BufferTarget.UniformBuffer,Marshal.SizeOf(shaderSharedData),
				ref shaderSharedData, BufferUsageHint.DynamicCopy);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
			GL.BindBufferBase (BufferTarget.UniformBuffer, 0, uboShaderSharedData);

			GL.ActiveTexture (TextureUnit.Texture0);

			int b;
			GL.GetInteger(GetPName.StencilBits, out b);

			ErrorCode err = GL.GetError ();
			Debug.Assert (err == ErrorCode.NoError, "OpenGL Error");
		}
		void updateShadersMatrices(){
			shaderSharedData.projection = projection;
			shaderSharedData.view = modelview;
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
		void changeModelView(Matrix4 newModelView){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, (IntPtr)Vector4.SizeInBytes, Vector4.SizeInBytes * 4,
				ref newModelView);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);
		}

		VAOItem<VAOChessData> vaoiPawn;
		VAOItem<VAOChessData> vaoiBishop;
		VAOItem<VAOChessData> vaoiHorse;
		VAOItem<VAOChessData> vaoiTower;
		VAOItem<VAOChessData> vaoiQueen;
		VAOItem<VAOChessData> vaoiKing;

		Tetra.Mesh meshPawn;
		Tetra.Mesh meshBishop;
		Tetra.Mesh meshHorse;
		Tetra.Mesh meshTower;
		Tetra.Mesh meshQueen;
		Tetra.Mesh meshKing;

		void loadMeshes()
		{
			currentState = GameState.MeshesLoading;

			meshPawn = Tetra.OBJMeshLoader.Load ("Meshes/pawn.obj");
			ProgressValue+=20;
			meshBishop = Tetra.OBJMeshLoader.Load ("Meshes/bishop.obj");
			ProgressValue+=20;
			meshHorse = Tetra.OBJMeshLoader.Load ("Meshes/horse.obj");
			ProgressValue+=20;
			meshTower = Tetra.OBJMeshLoader.Load ("Meshes/tower.obj");
			ProgressValue+=20;
			meshQueen = Tetra.OBJMeshLoader.Load ("Meshes/queen.obj");
			ProgressValue+=20;
			meshKing = Tetra.OBJMeshLoader.Load ("Meshes/king.obj");
			ProgressValue+=20;

			currentState = GameState.VAOInit;
		}
		void createMainVAO(){
			VAOItem<VAOChessData> vaoi = null;

			mainVAO = new Tetra.IndexedVAO<VAOChessData> ();

			float
			x = 4f,
			y = 4f,
			width = 10f,
			height = 10f;

			boardVAOItem = mainVAO.Add (new Tetra.Mesh (
				new Vector3[] {
					new Vector3 (x - width / 2f, y + height / 2f, 0f),
					new Vector3 (x - width / 2f, y - height / 2f, 0f),
					new Vector3 (x + width / 2f, y + height / 2f, 0f),
					new Vector3 (x + width / 2f, y - height / 2f, 0f)
				},
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
				},
				new ushort[] { 0, 1, 2, 2, 1, 3 }));
			boardVAOItem.DiffuseTexture = new GGL.Texture ("Textures/chessBoard.jpg");
			boardVAOItem.Datas = new VAOChessData[1];
			boardVAOItem.Datas[0].modelMats = Matrix4.Identity;
			boardVAOItem.Datas[0].color = new Vector4(1f,1f,1f,1f);

			boardVAOItem.UpdateInstancesData ();

			x = 0f;
			y = 0.03f;
			width = 1.0f;
			height = 1.0f;

			cellVAOItem = mainVAO.Add (new Tetra.Mesh (
				new Vector3[] {
					new Vector3 (x - width / 2f, y + height / 2f, 0f),
					new Vector3 (x - width / 2f, y - height / 2f, 0f),
					new Vector3 (x + width / 2f, y + height / 2f, 0f),
					new Vector3 (x + width / 2f, y - height / 2f, 0f)
				},
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
				},
				new ushort[] { 0, 1, 2, 2, 1, 3 }));
			cellVAOItem.DiffuseTexture = new GGL.Texture ("Textures/marble.jpg");
			cellVAOItem.Datas = new VAOChessData[1];
			cellVAOItem.Datas[0].modelMats = Matrix4.CreateTranslation (new Vector3 (4.5f, 4.5f, 0f));
			cellVAOItem.Datas[0].color = new Vector4(1f,1f,1f,1f);
			cellVAOItem.UpdateInstancesData ();

			List<int> tmp = new List<int> ();

			vaoiPawn = mainVAO.Add (meshPawn);
			vaoiPawn.DiffuseTexture = new GGL.Texture ("Textures/pawn_backed.png");
			vaoiPawn.Datas = new VAOChessData[16];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiPawn));
			ProgressValue++;

			vaoiBishop = mainVAO.Add (meshBishop);
			vaoiBishop.DiffuseTexture = new GGL.Texture ("Textures/bishop_backed.png");
			vaoiBishop.Datas = new VAOChessData[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiBishop));
			ProgressValue++;

			vaoiHorse = mainVAO.Add (meshHorse);
			vaoiHorse.DiffuseTexture = new GGL.Texture ("Textures/horse_backed.png");
			vaoiHorse.Datas = new VAOChessData[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiHorse));
			ProgressValue++;

			vaoiTower = mainVAO.Add (meshTower);
			vaoiTower.DiffuseTexture = new GGL.Texture ("Textures/tower_backed.png");
			vaoiTower.Datas = new VAOChessData[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiTower));
			ProgressValue++;

			vaoiQueen = mainVAO.Add (meshQueen);
			vaoiQueen.DiffuseTexture = new GGL.Texture ("Textures/queen_backed.png");
			vaoiQueen.Datas = new VAOChessData[2];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiQueen));
			ProgressValue++;

			vaoiKing = mainVAO.Add (meshKing);
			vaoiKing.DiffuseTexture = new GGL.Texture ("Textures/king_backed.png");
			vaoiKing.Datas = new VAOChessData[2];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiKing));
			ProgressValue++;

			piecesVAOIndexes = tmp.ToArray ();


			meshPawn = null;
			meshBishop = null;
			meshHorse = null;
			meshTower = null;
			meshQueen = null;
			meshKing = null;
		}
		void computeTangents(){
			mainVAO.ComputeTangents();
			currentState = GameState.BuildBuffers;
		}

		void draw()
		{
			piecesShader.Enable ();

			mainVAO.Bind ();

			GL.Enable(EnableCap.StencilTest);

			//cut stencil
			GL.StencilFunc(StencilFunction.Always, 1, 0xff);
			GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
			GL.StencilMask (0xff);
			GL.DepthMask (false);

			//drawSquarre(Selection, new Vector4(0.3f,1.0f,0.3f,0.5f));
			changeShadingColor(new Vector4(1.0f,1.0f,1.0f,1.0f));
			mainVAO.Render (PrimitiveType.Triangles, boardVAOItem);

			//draw reflected items
			GL.CullFace(CullFaceMode.Front);

			GL.StencilFunc(StencilFunction.Equal, 1, 0xff);
			GL.StencilMask (0x00);
			GL.DepthMask (true);

			changeModelView (reflectedModelview);
			drawPieces (0.3f);

			//draw scene
			GL.CullFace(CullFaceMode.Back);
			GL.Disable(EnableCap.StencilTest);
			changeModelView (modelview);

			#region sel squarres
			GL.Disable (EnableCap.DepthTest);

			if (ValidPositionsForActivePce != null){
				foreach (Point vm in ValidPositionsForActivePce)
					drawSquarre(vm, new Vector4(0.0f,0.5f,0.7f,0.7f));
			}

			drawSquarre(Selection, new Vector4(0.3f,1.0f,0.3f,0.5f));

			if (active >= 0)
				drawSquarre(Active, new Vector4(0.2f,0.2f,1.0f,0.6f));

			if (currentState == GameState.Check)
				drawSquarre(CurrentPlayer.King.BoardCell, new Vector4(1.0f,0.2f,0.2f,0.6f));

			GL.Enable (EnableCap.DepthTest);
			#endregion

			drawPieces ();

			mainVAO.Unbind ();

			renderArrow ();

			GL.StencilMask (0xff);
		}
		void drawPieces(float alpha = 1.0f){
			changeShadingColor(new Vector4(1f,1f,1f,alpha));
			mainVAO.Render (PrimitiveType.Triangles, piecesVAOIndexes);
		}
		void drawSquarre(Point pos, Vector4 color){
			changeShadingColor(color);
			cellVAOItem.Datas[0].modelMats = Matrix4.CreateTranslation
				((float)pos.X+0.5f, (float)pos.Y+0.5f, 0);
			cellVAOItem.UpdateInstancesData ();
			mainVAO.Render (PrimitiveType.Triangles, cellVAOItem);
		}

		#region Arrows
		vaoMesh arrows;
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
			coloredShader.Enable ();
			changeShadingColor (new Vector4 (0.2f, 1.0f, 0.2f, 0.5f));

			GL.Disable (EnableCap.CullFace);
			arrows.Render (PrimitiveType.TriangleStrip);
			GL.Enable (EnableCap.CullFace);

		}
		#endregion

		#endregion



		#region Interface
		GraphicObject uiSplash;
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

		void initInterface(){
			MouseMove += Mouse_Move;
			MouseButtonDown += Mouse_ButtonDown;
			MouseButtonUp += Mouse_ButtonUp;
			MouseWheelChanged += Mouse_WheelChanged;
			KeyboardKeyDown += MainWin_KeyboardKeyDown;

			GraphicObject g = CrowInterface.LoadInterface ("#Chess.gui.fps.crow");
			g.DataSource = this;

			g = CrowInterface.LoadInterface ("#Chess.gui.log.crow");
			g.DataSource = this;

			g = CrowInterface.LoadInterface ("#Chess.gui.menu.crow");
			g.DataSource = this;
			NotifyValueChanged ("CurrentColor", Color.Ivory);

			CrowInterface.DeleteWidget (uiSplash);
			uiSplash = null;
		}

		#region LOGS
		List<string> logBuffer = new List<string> ();
		int logBuffPtr = 0;

		void AddLog(string msg)
		{
			if (string.IsNullOrEmpty (msg))
				return;
			foreach (string s in msg.Split('\n')) {
				logBuffer.Add (s);
			}
			logBuffPtr = 0;
			syncLogUi ();
		}
		void syncLogUi()
		{
			for (int i = 0; i < 4; i++) {
				int ptr = logBuffer.Count - (i + 1 + logBuffPtr);
				if (ptr < 0)
					break;
				NotifyValueChanged ("log" + (3 - i).ToString (), logBuffer [ptr]);
			}
		}
		void onLogsWheel(object sender, Crow.MouseWheelEventArgs e){
			logBuffPtr += e.Delta;
			int limUp = logBuffer.Count - 4;
			if (logBuffPtr > limUp)
				logBuffPtr = limUp;
			if (logBuffPtr < 0)
				logBuffPtr = 0;
			syncLogUi ();
		}
		#endregion

		void onQuitClick (object sender, MouseButtonEventArgs e){
			closeGame ();
		}
		void onHintClick (object sender, MouseButtonEventArgs e){
			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine ("go");
		}
		void onUndoClick (object sender, MouseButtonEventArgs e){
			undoLastMove ();
			undoLastMove ();
		}
		void onResetClick (object sender, MouseButtonEventArgs e){
			resetBoard ();
			syncStockfish ();
		}

		#endregion

		#region Stockfish
		Process stockfish;
		List<String> stockfishMoves = new List<string> ();
		List<String> StockfishMoves {
			get { return stockfishMoves; }
			set { stockfishMoves = value; }
		}
		string stockfishPositionCommand {
			get {
				string tmp = "position startpos moves ";
				return 
					StockfishMoves.Count == 0 ? tmp : tmp + StockfishMoves.Aggregate ((i, j) => i + " " + j); }
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
		}
		void syncStockfish(){
			string cmd = stockfishPositionCommand;
			AddLog (cmd);
			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine (cmd);
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

			if (CurrentPlayer.Type == PlayerType.Human) {
				if (tmp [0] == "bestmove") {
					AddLog ("Hint => " + tmp [1]);
					nextHint = tmp [1];
					updateArrows = true;
				}
				return;
			}

			if (tmp [0] == "bestmove" && currentState != GameState.Checkmate)
				processMove (tmp [1]);
		}

		#endregion

		#region game logic
		ChessPlayer[] Players;
		volatile GameState currentState = GameState.Init;
		int currentPlayerIndex = 0;

		ChessPiece[,] Board;
		Point selection;
		Point active = new Point(-1,-1);

		int testedPlayer = 0;
		List<Point> ValidPositionsForActivePce = null;

		int cptWhiteOut = 0;
		int cptBlackOut = 0;

		volatile bool updateArrows = false;
		string nextHint;

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

		Point Active {
			get {
				return active;
			}
			set {
				active = value;
				if (active < 0)
					NotifyValueChanged ("ActCell", "" );
				else
					NotifyValueChanged ("ActCell", getChessCell(active.X,active.Y) + " => ");

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

			addPiece (vaoiHorse, 0, 0, PieceType.Horse, 1, 0);
			addPiece (vaoiHorse, 1, 0, PieceType.Horse, 6, 0);
			addPiece (vaoiHorse, 2, 1, PieceType.Horse, 1, 7);
			addPiece (vaoiHorse, 3, 1, PieceType.Horse, 6, 7);

			addPiece (vaoiTower, 0, 0, PieceType.Tower, 0 ,0);
			addPiece (vaoiTower, 1, 0, PieceType.Tower, 7, 0);
			addPiece (vaoiTower, 2, 1, PieceType.Tower, 0, 7);
			addPiece (vaoiTower, 3, 1, PieceType.Tower, 7, 7);

			addPiece (vaoiQueen, 0, 0, PieceType.Queen, 3, 0);
			addPiece (vaoiQueen, 1, 1, PieceType.Queen, 3, 7);

			addPiece (vaoiKing, 0, 0, PieceType.King, 4, 0);
			addPiece (vaoiKing, 1, 1, PieceType.King, 4, 7);
		}
		void resetBoard(bool animate = true){
			currentState = GameState.Play;
			CurrentPlayerIndex = 0;
			cptWhiteOut = 0;
			cptBlackOut = 0;
			StockfishMoves.Clear ();
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
		string checkSingleMove(Point pos, int xDelta, int yDelta){
			int x = pos.X + xDelta;
			int y = pos.Y + yDelta;

			if (x < 0 || x > 7 || y < 0 || y > 7)
				return null;
			
			if (Board [x, y] == null) {
				if (Board [pos.X, pos.Y].Type == PieceType.Pawn && xDelta != 0)
					return null;//pawn diagonal moves only for capturing
				return getChessCell (pos.X, pos.Y) + getChessCell (x, y);
			}

			if (Board [x, y].Player == Board [pos.X, pos.Y].Player)
				return null;
			if (Board [pos.X, pos.Y].Type == PieceType.Pawn && xDelta == 0)
				return null;//pawn cant take in front

			if (Board [x, y].Type == PieceType.King)
				return getChessCell (pos.X, pos.Y) + getChessCell (x, y) + "K";
			
			return getChessCell (pos.X, pos.Y) + getChessCell (x, y);
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
			string tmp;

			if (p != null) {
				switch (p.Type) {
				case PieceType.Pawn:
					int pawnDirection = 1;
					if (CurrentPlayer.Color == ChessColor.Black)
						pawnDirection = -1;
					validMoves.AddMove (checkSingleMove (pos, 0, 1 * pawnDirection));
					if (Board [x, y + pawnDirection] == null && !p.HasMoved)
						validMoves.AddMove (checkSingleMove (pos, 0, 2 * pawnDirection));
					validMoves.AddMove (checkSingleMove (pos, -1, 1 * pawnDirection));
					validMoves.AddMove (checkSingleMove (pos, 1, 1 * pawnDirection));
					break;
				case PieceType.Tower:
					validMoves.AddMove (checkIncrementalMove (pos, 0, 1));
					validMoves.AddMove (checkIncrementalMove (pos, 0, -1));
					validMoves.AddMove (checkIncrementalMove (pos, 1, 0));
					validMoves.AddMove (checkIncrementalMove (pos, -1, 0));
					break;
				case PieceType.Horse:
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
		ChessPiece preview_Captured;

		void previewBoard(string move){
			preview_Move = move;

			Point pStart = getChessCell(preview_Move.Substring(0,2));
			Point pEnd = getChessCell(preview_Move.Substring(2,2));
			ChessPiece p = Board [pStart.X, pStart.Y];

			preview_MoveState = p.HasMoved;
			Board [pStart.X, pStart.Y] = null;
			p.HasMoved = true;
			preview_Captured = Board [pEnd.X, pEnd.Y];
			Board [pEnd.X, pEnd.Y] = p;
		}
		void restoreBoardAfterPreview(){
			Point pStart = getChessCell(preview_Move.Substring(0,2));
			Point pEnd = getChessCell(preview_Move.Substring(2,2));
			ChessPiece p = Board [pEnd.X, pEnd.Y];
			p.HasMoved = preview_MoveState;
			Board [pStart.X, pStart.Y] = p;
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
				x = -0.5f;
				y = 7.5f - cptWhiteOut;
				if (cptWhiteOut > 8) {
					x -= 1f;
					y += 8f;
				}
			} else {				
				x = 8.5f;
				y = 0.5f + cptBlackOut;
				if (cptBlackOut > 8) {
					x += 1f;
					y -= 8f;
				}
			}
			return new Vector3 (x, y, 0f);
		}

		void removePiece(ChessPiece p, bool animate = true){
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
			if (string.IsNullOrEmpty (move))
				return;
			if (move == "(none)")
				return;
			if (animate)
				AddLog (CurrentPlayer.ToString () + " => " + move);
			
			StockfishMoves.Add (move);

			Point pStart = getChessCell(move.Substring(0,2));
			Point pEnd = getChessCell(move.Substring(2,2));

			ChessPiece p = Board [pStart.X, pStart.Y];
			Board [pStart.X, pStart.Y] = null;
			ChessPiece pTarget = Board [pEnd.X, pEnd.Y];
			if (pTarget != null)
				removePiece (pTarget, animate);
			Board [pEnd.X, pEnd.Y] = p;
			p.HasMoved = true;

			Vector3 targetPosition = new Vector3 (pEnd.X + 0.5f, pEnd.Y + 0.5f, 0f);
			if (animate)
				Animation.StartAnimation (new PathAnimation (p, "Position",
					new BezierPath (
						p.Position,
						targetPosition, Vector3.UnitZ)),
					0, move_AnimationFinished);
			else
				p.Position = targetPosition;

			Active = -1;

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
			foreach (string m in moves)
				processMove (m, false);
		}

		void switchPlayer(){
			if (CurrentPlayerIndex == 0)
				CurrentPlayerIndex = 1;
			else
				CurrentPlayerIndex = 0;

			syncStockfish ();

			if (CurrentPlayer.Type == PlayerType.AI) {
				stockfish.WaitForInputIdle ();
				stockfish.StandardInput.WriteLine ("go");
			}
		}

		void move_AnimationFinished (Animation a)
		{
			switchPlayer ();

			if (checkKingIsSafe ())
				currentState = GameState.Play;
			else if (getLegalMoves ().Length == 0)
				currentState = GameState.Checkmate;
			else
				currentState = GameState.Check;			
		}

		void closeGame(){
			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine ("quit");
			stockfish.WaitForExit ();
			this.Quit (null,null);
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

			uiSplash = CrowInterface.LoadInterface("#Chess.gui.Splash.crow");
			uiSplash.DataSource = this;

			initOpenGL ();

			initStockfish ();

			Thread t = new Thread (loadMeshes);
			t.IsBackground = true;
			t.Start ();
		}
		public override void GLClear ()
		{
			GL.ClearColor(0.2f, 0.2f, 0.4f, 1.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
		}
		public override void OnRender (FrameEventArgs e)
		{
			if (currentState < GameState.Play)
				return;
			draw ();
		}
		protected override void OnResize (EventArgs e)
		{
			base.OnResize (e);
			UpdateViewMatrix();
		}
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

			switch (currentState) {
			case GameState.Init:
			case GameState.MeshesLoading:
			case GameState.ComputeTangents:
				ProgressValue++;
				return;
			case GameState.VAOInit:

				createMainVAO ();

				currentState = GameState.ComputeTangents;

				Thread t = new Thread (computeTangents);
				t.IsBackground = true;
				t.Start ();
				return;
			case GameState.BuildBuffers:
				mainVAO.BuildBuffers ();
				initBoard ();
				initInterface ();
				currentState = GameState.Play;
				break;
			case GameState.Play:
				if (updateArrows) {
					updateArrows = false;
					createArrows (nextHint);
				}
				break;
			}

			Animation.ProcessAnimations ();
		}
		protected override void OnClosing (System.ComponentModel.CancelEventArgs e)
		{
			closeGame ();
			base.OnClosing (e);
		}
		#endregion

		#region vLookCalculations
		public void UpdateViewMatrix()
		{
			Rectangle r = this.ClientRectangle;
			GL.Viewport( r.X, r.Y, r.Width, r.Height);
			projection = Matrix4.CreatePerspectiveFieldOfView (fovY, r.Width / (float)r.Height, zNear, zFar);
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

			if (currentState == GameState.Checkmate) {
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
						//check here if rocking
						Active = Selection;
						return;
					}
				}


				//move
				if (ValidPositionsForActivePce == null)
					return;
				if (ValidPositionsForActivePce.Contains(Selection))
					processMove(getChessCell(Active.X,Active.Y) + getChessCell(Selection.X,Selection.Y));

			}


		}
		void Mouse_ButtonUp (object sender, OpenTK.Input.MouseButtonEventArgs e)
		{
		}

		void Mouse_Move(object sender, OpenTK.Input.MouseMoveEventArgs e)
		{			

			if (e.XDelta != 0 || e.YDelta != 0)
			{
				if (e.Mouse.MiddleButton == OpenTK.Input.ButtonState.Pressed) {
					Vector3 tmp = Vector3.Transform (vLook,
						Matrix4.CreateRotationX (-e.YDelta * RotationSpeed)*
						Matrix4.CreateRotationZ (-e.XDelta * RotationSpeed));
					tmp.Normalize();
					if (tmp.Y >= 0f || tmp.Z <= 0f)
						return;
					vLook = tmp;
					UpdateViewMatrix ();
				}else if (e.Mouse.LeftButton == OpenTK.Input.ButtonState.Pressed) {
					return;
				}else if (e.Mouse.RightButton == OpenTK.Input.ButtonState.Pressed) {
					Matrix4 m = Matrix4.CreateTranslation (-e.XDelta*MoveSpeed, e.YDelta*MoveSpeed, 0);
					vEyeTarget = Vector3.Transform (vEyeTarget, m);
					UpdateViewMatrix();
				}
				Vector3 mv = unprojectMouse (new Vector2 (e.X, e.Y));
				Vector3 vRay = Vector3.Normalize(mv - vEye);
				float a = vEye.Z / vRay.Z;
				mv = vEye - vRay * a;
				Point newPos = new Point ((int)Math.Truncate (mv.X), (int)Math.Truncate (mv.Y));
				Selection = newPos;
			}

		}

		Vector3 unprojectMouse(Vector2 mouse){
			Vector4 vec;

			vec.X = 2.0f * mouse.X / (float)viewport [2] - 1;
			vec.Y = -(2.0f * mouse.Y / (float)viewport [3] - 1);
			vec.Z = 0;
			vec.W = 1.0f;

			Matrix4 viewInv = Matrix4.Invert(modelview);
			Matrix4 projInv = Matrix4.Invert(projection);

			Vector4.Transform(ref vec, ref projInv, out vec);
			Vector4.Transform(ref vec, ref viewInv, out vec);

			if (vec.W > float.Epsilon || vec.W < float.Epsilon)
			{
				vec.X /= vec.W;
				vec.Y /= vec.W;
				vec.Z /= vec.W;
			}

			return new Vector3(vec);
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
			: base(1024, 800, 32, 24, 1, 4, "test")
		{
			VSync = VSyncMode.Off;
		}

		[STAThread]
		static void Main ()
		{
			Console.WriteLine ("starting example");

			using (MainWin win = new MainWin( )) {
				win.Run (30.0);
			}
		}
		#endregion
	}
}