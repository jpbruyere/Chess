using System;
using Crow;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using GGL;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;

namespace Chess
{
	enum GameState { Init, MeshesLoading, VAOInit, ComputeTangents, BuildBuffers, Play}
	enum ChessColor { White, Black};
	enum PieceType { Pawn, Tower, Horse, Bishop, King, Queen };

	class ChessPiece{
		public Tetra.VAOItem Mesh;
		public int InstanceIndex;
		public ChessColor Color;
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
			Mesh.modelMats [InstanceIndex] =
				Matrix4.CreateRotationX(xAngle) *
				Matrix4.CreateTranslation(new Vector3(x, y, z));
			Mesh.UpdateInstancesData ();
		}
		public ChessPiece(Tetra.VAOItem vaoi, int idx, ChessColor _color , PieceType _type, int xPos, int yPos){
			Mesh = vaoi;
			InstanceIndex = idx;
			Color = _color;
			Type = _type;
			InitX= xPos;
			InitY= yPos;
			x = xPos + 0.5f;
			y = yPos + 0.5f;
			z = 0f;
			xAngle = 0f;
			HasMoved = false;
			Captured = false;
			update ();
		}
		public void Reset(){
			xAngle = 0f;
			z = 0f;
			if (HasMoved)
				Animation.StartAnimation(new PathAnimation(this, "Position",
					new BezierPath(
						Position,
						new Vector3(InitX + 0.5f, InitY + 0.5f, 0f), Vector3.UnitZ)));

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
			public Matrix4 projection;
			public Matrix4 view;
			public Matrix4 normal;
			public Vector4 LightPosition;
		}

		#region  scene matrix and vectors
		public static Matrix4 modelview;
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
		public float zFar = 6000.0f;
		public float zNear = 1.0f;
		public float fovY = (float)Math.PI / 4;

		float eyeDist = 12f;
		float eyeDistTarget = 12f;
		float MoveSpeed = 0.02f;
		float RotationSpeed = 0.005f;
		float ZoomSpeed = 1f;

		public Vector4 vLight = new Vector4 (0.5f, 0.5f, -1f, 0f);
		#endregion

		#region GL

		UBOSharedData shaderSharedData;
		int uboShaderSharedData;

		public static Mat4InstancedShader piecesShader;
		public static SimpleColoredShader coloredShader;

		Tetra.IndexedVAO mainVAO;
		Tetra.VAOItem boardVAOItem;
		Tetra.VAOItem cellVAOItem;
		int[] piecesVAOIndexes;

		void initOpenGL()
		{
			GL.ClearColor(0.0f, 0.0f, 0.2f, 1.0f);
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

		#endregion

		Tetra.VAOItem vaoiPawn;
		Tetra.VAOItem vaoiBishop;
		Tetra.VAOItem vaoiHorse;
		Tetra.VAOItem vaoiTower;
		Tetra.VAOItem vaoiQueen;
		Tetra.VAOItem vaoiKing;

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
			Tetra.VAOItem vaoi = null;

			mainVAO = new Tetra.IndexedVAO ();

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
			boardVAOItem.DiffuseTexture = new Texture ("Textures/chessBoard.jpg");
			boardVAOItem.modelMats = new Matrix4[1];
			boardVAOItem.modelMats [0] = Matrix4.Identity;

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
			cellVAOItem.DiffuseTexture = new Texture ("Textures/marble.jpg");
			cellVAOItem.modelMats = new Matrix4[1];
			cellVAOItem.modelMats [0] = Matrix4.CreateTranslation (new Vector3 (4.5f, 4.5f, 0f));

			cellVAOItem.UpdateInstancesData ();

			List<int> tmp = new List<int> ();

			vaoiPawn = mainVAO.Add (meshPawn);
			vaoiPawn.DiffuseTexture = new Texture ("Textures/pawn_backed.png");
			vaoiPawn.modelMats = new Matrix4[16];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiPawn));
			ProgressValue++;

			vaoiBishop = mainVAO.Add (meshBishop);
			vaoiBishop.DiffuseTexture = new Texture ("Textures/bishop_backed.png");
			vaoiBishop.modelMats = new Matrix4[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiBishop));
			ProgressValue++;

			vaoiHorse = mainVAO.Add (meshHorse);
			vaoiHorse.DiffuseTexture = new Texture ("Textures/horse_backed.png");
			vaoiHorse.modelMats = new Matrix4[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiHorse));
			ProgressValue++;

			vaoiTower = mainVAO.Add (meshTower);
			vaoiTower.DiffuseTexture = new Texture ("Textures/tower_backed.png");
			vaoiTower.modelMats = new Matrix4[4];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiTower));
			ProgressValue++;

			vaoiQueen = mainVAO.Add (meshQueen);
			vaoiQueen.DiffuseTexture = new Texture ("Textures/queen_backed.png");
			vaoiQueen.modelMats = new Matrix4[2];
			tmp.Add (mainVAO.Meshes.IndexOf (vaoiQueen));
			ProgressValue++;

			vaoiKing = mainVAO.Add (meshKing);
			vaoiKing.DiffuseTexture = new Texture ("Textures/king_backed.png");
			vaoiKing.modelMats = new Matrix4[2];
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

			changeShadingColor(new Vector4(1.0f,1.0f,1.0f,1.0f));
			mainVAO.Render (PrimitiveType.Triangles, boardVAOItem);

			#region sel squarres
			GL.Disable (EnableCap.DepthTest);

			if (ValidMoves != null){
				foreach (Point vm in ValidMoves)
					drawSquarre(vm, new Vector4(0.0f,0.5f,0.7f,0.7f));
			}

			drawSquarre(Selection, new Vector4(0.3f,1.0f,0.3f,0.5f));

			if (active >= 0)
				drawSquarre(Active, new Vector4(0.2f,0.2f,1.0f,0.6f));

			GL.Enable (EnableCap.DepthTest);
			#endregion

			foreach (int i in piecesVAOIndexes) {
				Tetra.VAOItem p = mainVAO.Meshes [i];
				int halfCount = p.modelMats.Length / 2;

				//White
				changeShadingColor(new Vector4(1.0f,1.0f,1.0f,1.0f));
				mainVAO.Render (PrimitiveType.Triangles, p, 0, halfCount);

				changeShadingColor(new Vector4(0.6f,0.6f,0.6f,1.0f));
				mainVAO.Render (PrimitiveType.Triangles, p, halfCount, halfCount);
			}


			mainVAO.Unbind ();

			renderArrow ();
		}
		void drawSquarre(Point pos, Vector4 color){
			changeShadingColor(color);
			cellVAOItem.modelMats [0] = Matrix4.CreateTranslation
				((float)pos.X+0.5f, (float)pos.Y+0.5f, 0);
			cellVAOItem.UpdateInstancesData ();
			mainVAO.Render (PrimitiveType.Triangles, cellVAOItem);
		}
		void changeShadingColor(Vector4 color){
			GL.BindBuffer (BufferTarget.UniformBuffer, uboShaderSharedData);
			GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, Vector4.SizeInBytes,
				ref color);
			GL.BindBuffer (BufferTarget.UniformBuffer, 0);

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
			changeShadingColor (new Vector4 (1f, 0f, 0f, 0.7f));

			GL.Disable (EnableCap.CullFace);
			arrows.Render (PrimitiveType.TriangleStrip);
			GL.Enable (EnableCap.CullFace);

		}
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
		void onResetClick (object sender, MouseButtonEventArgs e){
			resetBoard ();
		}

		#endregion

		#region Stockfish
		Process stockfish;
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

		void P_Exited (object sender, EventArgs e)
		{
			AddLog ("Stockfish Terminated");
		}

		void dataReceived (object sender, DataReceivedEventArgs e)
		{
			if (string.IsNullOrEmpty (e.Data))
				return;

			string[] tmp = e.Data.Split (' ');

			if (CurrentPlayer == ChessColor.White) {
				if (tmp [0] == "bestmove") {
					AddLog ("Hint => " + tmp [1]);
					nextHint = tmp [1];
					updateArrows = true;
				}
				return;
			}

			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine (stockfishMoves);

			if (tmp [0] == "bestmove")
				processMove (tmp [1]);
		}

		#endregion

		volatile GameState currentState = GameState.Init;
		ChessColor currentPlayer = ChessColor.White;

		public ChessColor CurrentPlayer {
			get { return currentPlayer;}
			set {
				currentPlayer = value;
				NotifyValueChanged ("CurrentPlayer", currentPlayer);
			}
		}

		ChessPiece[,] Board;
		List<ChessPiece> Whites;
		List<ChessPiece> Blacks;
		Point selection;
		Point active = new Point(-1,-1);
		List<Point> ValidMoves = null;

		int cptWhiteOut = 0;
		int cptBlackOut = 0;

		string stockfishMoves;

		volatile bool updateArrows = false;
		string nextHint;

		public Point Active {
			get {
				return active;
			}
			set {
				active = value;
				if (active < 0)
					NotifyValueChanged ("ActCell", "" );
				else
					NotifyValueChanged ("ActCell", getChessCell(active.X,active.Y) + " => ");
				computeValidMove ();
			}
		}

		public Point Selection {
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

		void addValidMove(int x, int y){
			Point p = new Point (x, y);
			if (ValidMoves.Contains (p))
				return;
			ValidMoves.Add (p);
		}
		void checkSingleMove(int xDelta, int yDelta, bool onlyForTaking = false){
			int x = Active.X + xDelta;
			int y = Active.Y + yDelta;

			if (x < 0 || x > 7 || y < 0 || y > 7)
				return;
			if (Board [x, y] != null) {
				if (Board [x, y].Color == CurrentPlayer)
					return;
			} else if (onlyForTaking)
				return;

			addValidMove (x, y);
		}
		void checkIncrementalMove(int xDelta, int yDelta){
			int x = Active.X + xDelta;
			int y = Active.Y + yDelta;

			while (x >= 0 && x < 8 && y >= 0 && y < 8) {
				if (Board [x, y] == null) {
					addValidMove (x, y);
					x += xDelta;
					y += yDelta;
					continue;
				}

				if (Board [x, y].Color != CurrentPlayer)
					addValidMove (x, y);
				break;
			}
		}
		void computeValidMove(){
			if (Active < 0) {
				ValidMoves = null;
				return;
			}

			int x = Active.X;
			int y = Active.Y;

			ValidMoves = new List<Point> ();
			ChessPiece p = Board [x, y];

			switch (p.Type) {
			case PieceType.Pawn:
				if (Board [x, y + 1] == null) {
					addValidMove (x, y + 1);
					if (!p.HasMoved && Board [x, y + 2] == null)
						addValidMove (x, y + 2);
				}
				checkSingleMove (-1, 1, true);
				checkSingleMove (1, 1, true);
				break;
			case PieceType.Tower:
				checkIncrementalMove (0, 1);
				checkIncrementalMove (0, -1);
				checkIncrementalMove (1, 0);
				checkIncrementalMove (-1, 0);
				break;
			case PieceType.Horse:
				checkSingleMove (2, 1);
				checkSingleMove (2, -1);
				checkSingleMove (-2, 1);
				checkSingleMove (-2, -1);
				checkSingleMove (1, 2);
				checkSingleMove (-1, 2);
				checkSingleMove (1, -2);
				checkSingleMove (-1, -2);
				break;
			case PieceType.Bishop:
				checkIncrementalMove (1, 1);
				checkIncrementalMove (-1, -1);
				checkIncrementalMove (1, -1);
				checkIncrementalMove (-1, 1);
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
									addValidMove (x - 2, y);
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
									addValidMove (x + 2, y);
							}
						}
					}
				}

				checkSingleMove (-1, -1);
				checkSingleMove (-1,  0);
				checkSingleMove (-1,  1);
				checkSingleMove ( 0, -1);
				checkSingleMove ( 0,  1);
				checkSingleMove ( 1, -1);
				checkSingleMove ( 1,  0);
				checkSingleMove ( 1,  1);

				break;
			case PieceType.Queen:
				checkIncrementalMove (0, 1);
				checkIncrementalMove (0, -1);
				checkIncrementalMove (1, 0);
				checkIncrementalMove (-1, 0);
				checkIncrementalMove (1, 1);
				checkIncrementalMove (-1, -1);
				checkIncrementalMove (1, -1);
				checkIncrementalMove (-1, 1);
				break;
			}
			if (ValidMoves.Count == 0)
				ValidMoves = null;
		}

		string getChessCell(int col, int line){
			char c = (char)(col + 97);
			return c.ToString () + (line + 1).ToString ();
		}
		Point getChessCell(string s){
			return new Point ((int)s [0] - 97, int.Parse (s [1].ToString ()) - 1);
		}
		void removePiece(ChessPiece p){
			float x, y;
			if (p.Color == ChessColor.White) {
				x = -1f;
				y = 7f - cptWhiteOut;
				cptWhiteOut++;
			} else {
				x = 9f;
				y = 1f + cptBlackOut;
				cptBlackOut++;
			}
			p.Captured = true;
			p.HasMoved = true;
			Animation.StartAnimation(new PathAnimation(p, "Position",
				new BezierPath(
					p.Position,
					new Vector3(x, y, 0f), Vector3.UnitZ)));
		}
		void processMove(string move){
			AddLog (CurrentPlayer.ToString () + " => " + move);

			stockfishMoves += " " + move;

			Point pStart = getChessCell(move.Substring(0,2));
			Point pEnd = getChessCell(move.Substring(2,2));

			ChessPiece p = Board [pStart.X, pStart.Y];
			Board [pStart.X, pStart.Y] = null;
			ChessPiece pTarget = Board [pEnd.X, pEnd.Y];
			if (pTarget != null)
				removePiece (pTarget);
			Board [pEnd.X, pEnd.Y] = p;
			p.HasMoved = true;

			Animation.StartAnimation(new PathAnimation(p, "Position",
				new BezierPath(
					p.Position,
					new Vector3(pEnd.X + 0.5f, pEnd.Y + 0.5f, 0f), Vector3.UnitZ)),
				0, move_AnimationFinished);

			Active = -1;

			//check if rockMove
			if (p.Type == PieceType.King) {
				int xDelta = pStart.X - pEnd.X;
				if (Math.Abs (xDelta) == 2) {
					//rocking
					ChessPiece tower;
					if (xDelta > 0) {
						pStart.X = 0;
						pEnd.X = pEnd.X + 1;
					} else {
						pStart.X = 7;
						pEnd.X = pEnd.X - 1;
					}
					tower = Board [pStart.X, pStart.Y];
					Board [pStart.X, pStart.Y] = null;
					Board [pEnd.X, pEnd.Y] = tower;
					tower.HasMoved = true;
					Animation.StartAnimation(new PathAnimation(tower, "Position",
						new BezierPath(
							tower.Position,
							new Vector3(pEnd.X + 0.5f, pEnd.Y + 0.5f, 0f), Vector3.UnitZ * 2f)));
				}
			}

		}

		void switchPlayer(){
			if (CurrentPlayer == ChessColor.White)
				CurrentPlayer = ChessColor.Black;
			else
				CurrentPlayer = ChessColor.White;

			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine (stockfishMoves);

			if (CurrentPlayer == ChessColor.Black) {
				stockfish.WaitForInputIdle ();
				stockfish.StandardInput.WriteLine ("go");
			}
		}

		void move_AnimationFinished (Animation a)
		{
			switchPlayer ();			
		}

		void addPiece(Tetra.VAOItem vaoi, int idx, ChessColor _color, PieceType _type, int col, int line){
			ChessPiece p = new ChessPiece (vaoi, idx, _color, _type, col, line);
			Board [col, line] = p;
			if (_color == ChessColor.White)
				Whites.Add (p);
			else
				Blacks.Add (p);
		}
		void initBoard(){
			CurrentPlayer = ChessColor.White;
			cptWhiteOut = 0;
			cptBlackOut = 0;
			stockfishMoves = "position startpos moves";
			Active = -1;

			Board = new ChessPiece[8, 8];
			Whites = new List<ChessPiece> ();
			Blacks = new List<ChessPiece> ();

			for (int i = 0; i < 8; i++)
				addPiece (vaoiPawn, i, ChessColor.White, PieceType.Pawn, i, 1);
			for (int i = 0; i < 8; i++)
				addPiece (vaoiPawn, i+8, ChessColor.Black, PieceType.Pawn, i, 6);

			addPiece (vaoiBishop, 0, ChessColor.White, PieceType.Bishop, 2, 0);
			addPiece (vaoiBishop, 1, ChessColor.White, PieceType.Bishop, 5, 0);
			addPiece (vaoiBishop, 2, ChessColor.Black, PieceType.Bishop, 2, 7);
			addPiece (vaoiBishop, 3, ChessColor.Black, PieceType.Bishop, 5, 7);

			addPiece (vaoiHorse, 0, ChessColor.White, PieceType.Horse, 1, 0);
			addPiece (vaoiHorse, 1, ChessColor.White, PieceType.Horse, 6, 0);
			addPiece (vaoiHorse, 2, ChessColor.Black, PieceType.Horse, 1, 7);
			addPiece (vaoiHorse, 3, ChessColor.Black, PieceType.Horse, 6, 7);

			addPiece (vaoiTower, 0, ChessColor.White, PieceType.Tower, 0 ,0);
			addPiece (vaoiTower, 1, ChessColor.White, PieceType.Tower, 7, 0);
			addPiece (vaoiTower, 2, ChessColor.Black, PieceType.Tower, 0, 7);
			addPiece (vaoiTower, 3, ChessColor.Black, PieceType.Tower, 7, 7);

			addPiece (vaoiQueen, 0, ChessColor.White, PieceType.Queen, 3, 0);
			addPiece (vaoiQueen, 1, ChessColor.Black, PieceType.Queen, 3, 7);

			addPiece (vaoiKing, 0, ChessColor.White, PieceType.King, 4, 0);
			addPiece (vaoiKing, 1, ChessColor.Black, PieceType.King, 4, 7);
		}
		void resetBoard(){
			CurrentPlayer = ChessColor.White;
			cptWhiteOut = 0;
			cptBlackOut = 0;
			stockfishMoves = "position startpos moves";
			Active = -1;
			Board = new ChessPiece[8, 8];
			foreach (ChessPiece p in Whites) {
				p.Reset ();
				Board [p.InitX, p.InitY] = p;
			}
			foreach (ChessPiece p in Blacks) {
				p.Reset ();
				Board [p.InitX, p.InitY] = p;
			}
		}

		void closeGame(){
			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine ("quit");
			stockfish.WaitForExit ();
			this.Quit (null,null);
		}

		#region OTK window overrides
		protected override void OnLoad (EventArgs e)
		{
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
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}
		public override void OnRender (FrameEventArgs e)
		{
			if (currentState != GameState.Play)
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
			this.CursorVisible = true;

			if (e.Mouse.LeftButton != OpenTK.Input.ButtonState.Pressed)
				return;

			clearArrows ();

			if (Active < 0) {
				ChessPiece p = Board [Selection.X, Selection.Y];
				if (p == null)
					return;
				if (p.Color != CurrentPlayer)
					return;
				Active = Selection;
			} else if (Selection == Active) {
				Active = -1;
				return;
			} else {
				ChessPiece p = Board [Selection.X, Selection.Y];
				if (p != null) {
					if (p.Color == CurrentPlayer) {
						//check here if rocking
						Active = Selection;
						return;
					}
				}


				//move
				if (ValidMoves == null)
					return;
				if (ValidMoves.Contains(Selection))
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
			if (eyeDistTarget < zNear+10)
				eyeDistTarget = zNear+10;
			else if (eyeDistTarget > zFar-100)
				eyeDistTarget = zFar-100;

			EyeDist = eyeDistTarget;
			//Animation.StartAnimation(new Animation<float> (this, "EyeDist", eyeDistTarget, (eyeDistTarget - eyeDist) * 0.2f));
		}
		#endregion

		#region CTOR and Main
		public MainWin ()
			: base(1024, 800,"test")
		{}

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