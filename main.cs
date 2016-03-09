using System;
using Crow;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using GGL;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Chess
{
	enum ChessColor { White, Black};
	enum PieceType { Pawn, Tower, Horse, Bishop, King, Queen };

	class ChessPiece{
		public Tetra.VAOItem Mesh;
		public int InstanceIndex;
		public ChessColor Color;
		public PieceType Type;
		public bool HasMoved;

		float x, y, z, xAngle;

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
			x = xPos + 0.5f;
			y = yPos + 0.5f;
			z = 0f;
			xAngle = 0f;
			HasMoved = false;
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
		public static ChessShader mainShader;

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
			mainShader = new ChessShader ();

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

			createScene ();

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

		void createScene()
		{
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
				new ushort[] { 0, 1, 2, 2,1,3 }));			
			boardVAOItem.DiffuseTexture = new Texture("Textures/chessBoard.jpg");
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
				new ushort[] { 0, 1, 2, 2, 1, 3}));			
			cellVAOItem.DiffuseTexture = new Texture("Textures/marble.jpg");
			cellVAOItem.modelMats = new Matrix4[1];
			cellVAOItem.modelMats [0] = Matrix4.CreateTranslation(new Vector3(4.5f,4.5f,0f));

			cellVAOItem.UpdateInstancesData ();

			List<int> tmp = new List<int> ();

			vaoi = mainVAO.Add (Tetra.OBJMeshLoader.Load ("Meshes/pawn.obj"));
			vaoi.DiffuseTexture = new Texture("Textures/pawn_backed.png");
			vaoi.modelMats = new Matrix4[16];
			for (int i = 0; i < 8; i++)
				addPiece (vaoi, i, ChessColor.White, PieceType.Pawn, i, 1);
			for (int i = 0; i < 8; i++)
				addPiece (vaoi, i+8, ChessColor.Black, PieceType.Pawn, i, 6);

			tmp.Add(mainVAO.Meshes.IndexOf (vaoi));


			vaoi = mainVAO.Add (Tetra.OBJMeshLoader.Load ("Meshes/bishop.obj"));
			vaoi.DiffuseTexture = new Texture("Textures/bishop_backed.png");
			vaoi.modelMats = new Matrix4[4];
			addPiece (vaoi, 0, ChessColor.White, PieceType.Bishop, 2, 0);
			addPiece (vaoi, 1, ChessColor.White, PieceType.Bishop, 5, 0);
			addPiece (vaoi, 2, ChessColor.Black, PieceType.Bishop, 2, 7);
			addPiece (vaoi, 3, ChessColor.Black, PieceType.Bishop, 5, 7);

			tmp.Add(mainVAO.Meshes.IndexOf (vaoi));

			vaoi = mainVAO.Add (Tetra.OBJMeshLoader.Load ("Meshes/horse.obj"));
			vaoi.DiffuseTexture = new Texture("Textures/horse_backed.png");
			vaoi.modelMats = new Matrix4[4];
			addPiece (vaoi, 0, ChessColor.White, PieceType.Horse, 1, 0);
			addPiece (vaoi, 1, ChessColor.White, PieceType.Horse, 6, 0);
			addPiece (vaoi, 2, ChessColor.Black, PieceType.Horse, 1, 7);
			addPiece (vaoi, 3, ChessColor.Black, PieceType.Horse, 6, 7);

			tmp.Add(mainVAO.Meshes.IndexOf (vaoi));

			vaoi = mainVAO.Add (Tetra.OBJMeshLoader.Load ("Meshes/tower.obj"));
			vaoi.DiffuseTexture = new Texture("Textures/tower_backed.png");
			vaoi.modelMats = new Matrix4[4];
			addPiece (vaoi, 0, ChessColor.White, PieceType.Tower, 0 ,0);
			addPiece (vaoi, 1, ChessColor.White, PieceType.Tower, 7, 0);
			addPiece (vaoi, 2, ChessColor.Black, PieceType.Tower, 0, 7);
			addPiece (vaoi, 3, ChessColor.Black, PieceType.Tower, 7, 7);

			tmp.Add(mainVAO.Meshes.IndexOf (vaoi));

			vaoi = mainVAO.Add (Tetra.OBJMeshLoader.Load ("Meshes/queen.obj"));
			vaoi.DiffuseTexture = new Texture("Textures/queen_backed.png");
			vaoi.modelMats = new Matrix4[2];
			addPiece (vaoi, 0, ChessColor.White, PieceType.Queen, 3, 0);
			addPiece (vaoi, 1, ChessColor.Black, PieceType.Queen, 4, 7);

			tmp.Add(mainVAO.Meshes.IndexOf (vaoi));

			vaoi = mainVAO.Add (Tetra.OBJMeshLoader.Load ("Meshes/king.obj"));
			vaoi.DiffuseTexture = new Texture("Textures/king_backed.png");
			vaoi.modelMats = new Matrix4[2];
			addPiece (vaoi, 0, ChessColor.White, PieceType.King, 4, 0);
			addPiece (vaoi, 1, ChessColor.Black, PieceType.King,3, 7);

			tmp.Add(mainVAO.Meshes.IndexOf (vaoi));

			mainVAO.ComputeTangents();
			mainVAO.BuildBuffers ();

			piecesVAOIndexes = tmp.ToArray ();
		}
		void addPiece(Tetra.VAOItem vaoi, int idx, ChessColor _color, PieceType _type, int col, int line){
			Board [col, line] = new ChessPiece (vaoi, idx, _color, _type, col, line);
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


		#region Interface
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
				if (tmp [0] == "bestmove")
					AddLog ("Hint => " + tmp[1]);
				return;
			}

			stockfish.WaitForInputIdle ();
			stockfish.StandardInput.WriteLine (stockfishMoves);

			if (tmp [0] == "bestmove")
				processMove (tmp [1]);
		}			

		#endregion

		ChessColor currentPlayer = ChessColor.White;

		public ChessColor CurrentPlayer {
			get { return currentPlayer;}
			set {
				currentPlayer = value;
				NotifyValueChanged ("CurrentPlayer", currentPlayer);
			}
		}

		ChessPiece[,] Board = new ChessPiece[8, 8];
		Point selection;
		Point active = new Point(-1,-1);
		List<Point> ValidMoves = null;

		int cptWhiteOut = 0;
		int cptBlackOut = 0;

		string stockfishMoves = "position startpos moves";


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
			Animation.StartAnimation(new FloatAnimation(p, "X", x, 0.2f));
			Animation.StartAnimation(new FloatAnimation(p, "Y", y, 0.2f));
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

			Animation.StartAnimation(new FloatAnimation(p, "X", (float)pEnd.X+0.5f, 0.2f));
			Animation.StartAnimation(new FloatAnimation(p, "Y", (float)pEnd.Y+0.5f, 0.2f));

			Active = -1;

			//check if rockMove
			if (p.Type == PieceType.King) {
				int xDelta = pStart.X - pEnd.X;
				if (Math.Abs (xDelta) == 2) {
					//rocking
					ChessPiece tower;
					if (xDelta > 0) {
						tower = Board [0, pStart.Y];
						Board [0, pStart.Y] = null;
						pEnd = new Point (pEnd.X + 1, pStart.Y);
					} else {
						tower = Board [7, pStart.Y];
						Board [7, pStart.Y] = null;
						pEnd = new Point (pEnd.X - 1, pStart.Y);
					}
					Board [pEnd.X, pEnd.Y] = tower;
					tower.HasMoved = true;
					Animation.StartAnimation(new FloatAnimation(tower, "X", (float)pEnd.X+0.5f, 0.2f));
					Animation.StartAnimation(new FloatAnimation(tower, "Y", (float)pEnd.Y+0.5f, 0.2f));
				}
			}

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

			initInterface ();

			initOpenGL ();

			initStockfish ();
		}			
		public override void GLClear ()
		{
			GL.ClearColor(0.2f, 0.2f, 0.4f, 1.0f);
			GL.Clear (ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
		}
		public override void OnRender (FrameEventArgs e)
		{
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
//				Point diff = Active - Selection;
//				float length = (float)Math.Sqrt (diff.X * diff.X + diff.Y * diff.Y);
//				FloatAnimation fa = new FloatAnimation (p, "Z", 1f, 1f / length*0.4f);
//				Animation.StartAnimation(fa,0,new AnimationEventHandler(delegate(Animation a) {
//					Animation.StartAnimation(new FloatAnimation (p, "Z", 0f, (a as FloatAnimation).Step));
//						}));
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
				Debug.WriteLine (e.Position.ToString());
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