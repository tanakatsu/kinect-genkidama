using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using System.Diagnostics;
using Microsoft.Research.Kinect; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Nui; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Audio; // Microsoft.Research.Kinectへの参照の追加が必要

namespace WindowsGame1
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
       
        //----------
        // Kinect
        //----------

        Runtime nui; // Kinectセンサクラス

        Texture2D texture_image = null;  //実画像テクスチャ
        Texture2D texture_depth = null; //奥行テクスチャ

        Color[] imageColor;    //色情報の格納
        Color[] depthColor;    //色情報の格納

        //------------
        // スケルトン
        //------------
        BasicEffect basicEffect = null;

        VertexBuffer vertexBuffer = null; //頂点バッファ
        VertexPositionColor[] skeletonVertexData = null; // スケルトン頂点データ

        Color[] colors = { Color.Red, Color.Blue, Color.ForestGreen, Color.Yellow, Color.Orange, Color.Purple, Color.White };
        Color[] anticolors = { Color.Green, Color.Orange, Color.Red, Color.Purple, Color.Blue, Color.Yellow, Color.Black };
        int ncolors = 6;

        Dictionary<int, Dictionary<JointID, Vector3>> joints;  // ジョイントの情報
        Dictionary<int, Dictionary<JointID, Vector3>> joints_pre;  // ジョイントの情報 (1フレーム前)
        Dictionary<int, Dictionary<JointID, Vector3>> joints2D;  // ジョイントの情報 (2D座標系)
        Dictionary<int, Dictionary<JointID, Vector3>> joints2D_pre;  // ジョイントの情報 (2D座標系, 1フレーム前)
        Dictionary<int, Dictionary<string, Vector3>> postures; // 姿勢ベクトル

        int trackedPlayerCnt = 0; // スケルトントラッキングされている人数(0-2)
        int[] trackedPlayerIndex = new int[2]; // トラッキングされているプレイヤーID

        //------------------
        // cameraパラメータ
        //------------------
        Matrix view;
        Matrix view1; // Depth画像用
        Matrix view2; // 実画像用
        Matrix projection;

        //----
        // UI
        //----
        enum DisplayMode { DISPLAY_MODE_TWIN, DISPLAY_MODE_DEPTH, DISPLAY_MODE_IMAGE };
        protected int displayMode = (int)DisplayMode.DISPLAY_MODE_TWIN;

        KeyboardState keyboardState; // キーボード状態の宣言
        KeyboardState prekeyboardState;

        SpriteFont fontArial = null; //フォント
        bool debugMode = true;

        //----------------
        // ARオブジェクト
        //----------------
        List<ARObject> ARObjectList = null; // ARオブジェクトリスト
        ARObject sampleParts; // サンプルパーツ
        SampleVFX sampleVFX; // サンプルエフェクト

        Dictionary<int, Vector3> textureColorList = new Dictionary<int, Vector3>()
        {
            {0, new Vector3(1.0f, 1.0f, 1.0f)},
            {1, new Vector3(0.1f, 0.1f, 0.1f)},
            {2, new Vector3(1.0f, 1.0f, 0.0f)},
        }; // テクスチャ色リスト
        
        int textureColorNo = 0; // サンプルパーツの色 (番号)
        Vector3 samplePartsColor; // サンプルパーツの色

        //----------------
        // ジェスチャ認識
        //----------------
        public GestureDetect gestureDetect;

        //-----------
        // 状態管理
        //-----------
        public GameStateManager gameStateManager;

        //---------------
        // その他
        //---------------
        string version = "Ver 1.9.4";

        
        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            this.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 30.0); // 30fpsにする (Kinectは30fpsのためこれに合わせる)
            this.IsMouseVisible = true; // マウスカーソルを表示する

            // 画面の解像度を変更する (Kinectカメラに合わせる)
            this.graphics.PreferredBackBufferWidth = 640;
            this.graphics.PreferredBackBufferHeight = 480;

            //-----------------
            // Runtimeの初期化
            //-----------------
            nui = new Runtime();          
            try
            {
                nui.Initialize(RuntimeOptions.UseColor |
                    RuntimeOptions.UseDepth |
                    RuntimeOptions.UseDepthAndPlayerIndex |
                    RuntimeOptions.UseSkeletalTracking);
            }
            catch (InvalidOperationException)
            {
                Trace.WriteLine("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                return;
            }

            // ビデオストリームをオープン
            try
            {
                nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                Trace.WriteLine("Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }

            //-------------
            //ハンドラ登録
            //-------------
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_VideoFrameReady);
            nui.DepthFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_DepthFrameReady);
            nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);

            //---------------------------
            // ARオブジェクトインスタンス
            //---------------------------
            ARObjectList= new List<ARObject>();

            sampleParts = new ARObject("monkey", new AttachedPos(JointID.Head));
//            sampleParts = new ARObject("monkey", new AttachedPos(JointID.Head, JointID.ShoulderCenter, JointID.Head, 1.5f)); // 頭の上に表示する(Test)
            sampleVFX = new SampleVFX("simplesphere");

            samplePartsColor = textureColorList[0]; // テクスチャ色の設定（for Demo)

            //---------------------------
            // ジェスチャ検出モジュール
            //--------------------------
            gestureDetect = new GestureDetect(this);
            Components.Add(gestureDetect);

            //--------------------------
            // ゲーム状態管理モジュール
            //--------------------------
            gameStateManager = new GameStateManager(this);
            Components.Add(gameStateManager);
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            //---------------
            // モデルの追加
            //---------------
            ARObjectList.Add(sampleParts);
            ARObjectList.Add(sampleVFX);

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // TODO: use this.Content to load your game content here

            // フォントの読み込み
            this.fontArial = this.Content.Load<SpriteFont>("Arial"); // ContentにArial.spritefontを追加しておく

            // エフェクトを作成
            this.basicEffect = new BasicEffect(this.GraphicsDevice);
            this.basicEffect.VertexColorEnabled = true;  // エフェクトで頂点カラーを有効にする

            // 頂点バッファ作成
            this.vertexBuffer = new VertexBuffer(this.GraphicsDevice,typeof(VertexPositionColor), 19 * 2 * 2, BufferUsage.None);

            // 頂点データを作成する
            this.skeletonVertexData = new VertexPositionColor[19 * 2 * 2]; // (20関節 - 1) * 2点 * 2プレイヤー
            
            // 関節データのテーブルを作成する
            joints = new Dictionary<int, Dictionary<JointID, Vector3>>();
            joints_pre = new Dictionary<int, Dictionary<JointID, Vector3>>();
            joints2D = new Dictionary<int, Dictionary<JointID, Vector3>>();
            joints2D_pre = new Dictionary<int, Dictionary<JointID, Vector3>>(); 
            postures = new Dictionary<int, Dictionary<string, Vector3>>();

            for (int playerIndex = 0; playerIndex < 8; playerIndex++)
            {
                joints.Add(playerIndex, new Dictionary<JointID, Vector3>());
                joints_pre.Add(playerIndex, new Dictionary<JointID, Vector3>());
                joints2D.Add(playerIndex, new Dictionary<JointID, Vector3>()); 
                joints2D_pre.Add(playerIndex, new Dictionary<JointID, Vector3>());
                postures.Add(playerIndex, new Dictionary<string, Vector3>());
            }

            //-----------
            //カメラ設定
            //-----------

            // ビューマトリックスをあらかじめ設定
            view1 = Matrix.CreateLookAt(
                new Vector3(320.0f, 240.0f, 575.0f), //  640x480がそのまま表示できるように適当な値 (Depth画像用)
                new Vector3(320.0f, 240.0f, 0.0f),
                Vector3.Up
            );
            view2 = Matrix.CreateLookAt(
                new Vector3(320.0f, 240.0f, 625.0f), //  640x480がそのまま表示できるように適当な値 (実画像用)
                new Vector3(320.0f, 240.0f, 0.0f),
                Vector3.Up
            );

            // プロジェクションマトリックスをあらかじめ設定
            projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45.0f),
                (float)this.GraphicsDevice.Viewport.Width / (float)this.GraphicsDevice.Viewport.Height,
                1.0f, 10000.0f
                );

            //-------------------------
            // ARオブジェクトのロード
            //-------------------------
            foreach (ARObject obj in ARObjectList)
            {
                obj.Load(Content);
            }
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            // TODO: Add your update logic here

            //Kinectのカメラ角度の取得
//            Trace.WriteLine(nui.NuiCamera.ElevationAngle);

            if (gameStateManager.gameState == (int)GameState.Playing)
            {
                //-------------------------
                // ARオブジェクトの状態更新
                //-------------------------
                foreach (ARObject obj in ARObjectList)
                {
                    if (trackedPlayerCnt > 0)
                    {
                        obj.IsVisible = true;
                    }
                    else
                    {
                        obj.IsVisible = false; // トラッキングされてなければ非表示にする
                    }

                    /*
                    // ARオブジェクトの色を変える (for Demo)
                    if (gestureDetect.gesture_swipedown)
                    {
                        textureColorNo++;
                        if (textureColorNo >= textureColorList.Keys.Count)
                        {
                            textureColorNo = 0;
                        }
                        samplePartsColor = textureColorList[textureColorNo];
                    }
                    obj.Color = samplePartsColor;
                    */

                    obj.Update(this);
                }
            }
            else // GameState.Ready
            {
                // すべて非表示
                foreach (ARObject obj in ARObjectList)
                {
                    obj.IsVisible = false;
                }
            }

            //------------------------
            // マウス、キーボード操作
            //------------------------

            keyboardState = Keyboard.GetState();
            // ESCキーで終了
            if (keyboardState.IsKeyDown(Keys.Escape) && prekeyboardState.IsKeyUp(Keys.Escape))
            {
                this.Exit();
            }
            // 1,2,3キーで表示モード切り替え
            if (keyboardState.IsKeyDown(Keys.D1) && prekeyboardState.IsKeyUp(Keys.D1))
            {
                displayMode = (int)DisplayMode.DISPLAY_MODE_TWIN;
            }
            else if (keyboardState.IsKeyDown(Keys.D2) && prekeyboardState.IsKeyUp(Keys.D2))
            {
                displayMode = (int)DisplayMode.DISPLAY_MODE_DEPTH;
            }
            else if (keyboardState.IsKeyDown(Keys.D3) && prekeyboardState.IsKeyUp(Keys.D3))
            {
                displayMode = (int)DisplayMode.DISPLAY_MODE_IMAGE;
            }
            // Aキーで全画面表示トグル切り替え
            if (keyboardState.IsKeyDown(Keys.A) && prekeyboardState.IsKeyUp(Keys.A))
            {
                this.graphics.ToggleFullScreen();
            }
            // Dキーでデバッグモードトグル切り替え
            if (keyboardState.IsKeyDown(Keys.D) && prekeyboardState.IsKeyUp(Keys.D))
            {
                debugMode = debugMode == true ? false : true;
            }

            prekeyboardState = keyboardState; // キーボードの状態を保存


            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // TODO: Add your drawing code here

            //---------------------
            // 2D描画（カメラ画像)
            //---------------------
            spriteBatch.Begin();
            if (displayMode == (int)DisplayMode.DISPLAY_MODE_TWIN)
            {
                spriteBatch.Draw(this.texture_image, new Vector2(0, 0), null,
                    Color.White, 0.0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0.0f); //実画像Textureの描写
                spriteBatch.Draw(this.texture_depth, new Vector2(320, 0), null,
                    Color.White, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0.0f); //奥行きTextureの描写
            }
            else if (displayMode == (int)DisplayMode.DISPLAY_MODE_DEPTH)
            {
                spriteBatch.Draw(this.texture_depth, new Vector2(0, 0), null,
                    Color.White, 0.0f, Vector2.Zero, 2.0f, SpriteEffects.None, 0.0f); //奥行きTextureの描写
            }
            else if (displayMode == (int)DisplayMode.DISPLAY_MODE_IMAGE)
            {
                spriteBatch.Draw(this.texture_image, new Vector2(0, 0), null,
                    Color.White, 0.0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0.0f); //実画像Textureの描写
            }

            // バージョン情報表示
            spriteBatch.DrawString(fontArial, version, new Vector2(640 - version.Length * 12, 480 - 20), Color.White);
            spriteBatch.End();

            //-------------------
            // 3D描画(スケルトン)
            //-------------------

            // カメラパラメータのセット
            if (displayMode == (int)DisplayMode.DISPLAY_MODE_DEPTH || displayMode == (int)DisplayMode.DISPLAY_MODE_TWIN)
            {
                view = view1;
            }
            else
            {
                view = view2;
            }
            basicEffect.View = view;
            basicEffect.Projection = projection;

            // 頂点バッファにデータをセット
            this.vertexBuffer.SetData(this.skeletonVertexData);

            // 頂点バッファをセットします
            this.GraphicsDevice.SetVertexBuffer(this.vertexBuffer);

            foreach (EffectPass pass in this.basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                this.GraphicsDevice.DrawPrimitives(PrimitiveType.LineList, 0, 19 * 2);
            }

            //----------------------
            // ARオブジェクトの描画
            //----------------------
            foreach (ARObject obj in ARObjectList)
            {
                if (obj.IsVisible)
                {
                    obj.Draw(view, projection);
                }
            }

            //-----------------------
            // Status表示、Debug表示
            //-----------------------         
            gameStateManager.Draw(spriteBatch);
            if (debugMode)
            {
                gestureDetect.Draw(spriteBatch);
            }

            base.Draw(gameTime);
        }

        //-------------
        // 実画像処理
        //-------------
        void nui_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            lock (this)
            {
                PlanarImage Image = e.ImageFrame.Image;

                int no = 0;
                this.imageColor = new Color[Image.Height * Image.Width];
                this.texture_image = new Texture2D(graphics.GraphicsDevice,
                                                Image.Width, Image.Height);    //テクスチャの作成

                //画像取得
                for (int y = 0; y < Image.Height; ++y)
                { //y軸
                    for (int x = 0; x < Image.Width; ++x, no += 4)
                    { //x軸
                        this.imageColor[y * Image.Width + x] =
                                new Color(Image.Bits[no + 2], Image.Bits[no + 1], Image.Bits[no + 0]);
                    }
                }
                this.texture_image.SetData(this.imageColor);    //texture_imageにデータを書き込む
            }
        }

        //------------
        // 奥行き画像
        //------------
        void nui_DepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            lock (this)
            {
                PlanarImage Image = e.ImageFrame.Image;
                //  byte[] convertedDepthFrame = convertDepthFrame(Image.Bits);

                int no = 0;

                this.depthColor = new Color[Image.Height * Image.Width];
                this.texture_depth = new Texture2D(graphics.GraphicsDevice,
                                                 Image.Width, Image.Height);    //テクスチャの作成

                //画像取得
                for (int y = 0; y < Image.Height; ++y)
                { //y軸
                    for (int x = 0; x < Image.Width; ++x, no += 2)
                    { //x軸
                        int n = (y * Image.Width + x) * 2;
                        int realDepth = (Image.Bits[n + 1] << 5) | (Image.Bits[n] >> 3);
                        byte intensity = (byte)((255 - (255 * realDepth / 0x0fff)) / 2);
                        this.depthColor[y * Image.Width + x] = new Color(intensity, intensity, intensity);

                        // プレイヤー毎に色分けする
                        int playerIndex = Image.Bits[n] & 0x07;
                        if (playerIndex > 0)
                        {
                            Color labelColor = colors[playerIndex % ncolors];
                            this.depthColor[y * Image.Width + x] = new Color(labelColor.B * intensity / 256, labelColor.G * intensity / 256, labelColor.R * intensity / 256);
                        }
                    }
                }
                this.texture_depth.SetData(this.depthColor);    //texture_imageにデータを書き込む
            }
        }

        //------------
        // スケルトン
        //------------
        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;
            int playerIndex = 0;

            trackedPlayerCnt = 0; // リセット
            trackedPlayerIndex[0] = trackedPlayerIndex[1] = 0; // リセット

            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    /*
                    foreach (Joint joint in data.Joints)
                    {
                        Trace.WriteLine(joint.ID + "\t\t\t" +
                            joint.Position.X + "\t" +
                            joint.Position.Y + "\t" +
                            joint.Position.Z);
                    }
                    */

                    // 各ユーザのジョイント情報を取得
                    GetJoints(playerIndex, data.Joints);

                    // 各ユーザの姿勢ベクトルを計算
                    GetPostures(playerIndex);

                    // スケルトン頂点データを作成
                    SetSkeletonData(playerIndex, trackedPlayerCnt);

                    trackedPlayerIndex[trackedPlayerCnt] = playerIndex; // トラッキングされているプレイヤーインデックスを保持
                    ++trackedPlayerCnt;
                }
                ++playerIndex;
            }
        }

        //--------------------------------
        // 各ユーザのジョイント情報を取得
        //--------------------------------
        void GetJoints(int playerIndex, JointsCollection joints)
        {
            foreach (Joint joint in joints)
            {
                GetJoint(playerIndex, joint);
            }

            /*
            foreach (Joint joint in joints)
            {
                Trace.WriteLine(string.Format("{0} {1} {2}", joint.ID, joints2D[playerIndex][joint.ID].X, joints2D[playerIndex][joint.ID].Y));
            }
             */
        }

        void GetJoint(int playerIndex, Joint joint)
        {
            Vector3 pos = new Vector3();
            Vector3 pos2D = new Vector3();

            if (joints.ContainsKey(playerIndex) && joints[playerIndex].ContainsKey(joint.ID))
            {
                this.joints_pre[playerIndex][joint.ID] = this.joints[playerIndex][joint.ID];
                this.joints2D_pre[playerIndex][joint.ID] = this.joints2D[playerIndex][joint.ID];
            }

            if (joint.TrackingState == JointTrackingState.Tracked || joint.TrackingState == JointTrackingState.Inferred)
            {
                pos.X = joint.Position.X;
                pos.Y = joint.Position.Y;
                pos.Z = joint.Position.Z;

                float vx, vy;
                short depth;
                nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out vx, out vy, out depth); //スクリーン座標取得
                //convert to 640, 480 space
                vx = Math.Max(0, Math.Min(vx * 640, 640)); // vx = 0.0-1.0
                vy = Math.Max(0, Math.Min(vy * 480, 480)); // vy = 0.0-1.0

                pos2D.X = vx;
                pos2D.Y = vy;
                pos2D.Z = depth >> 3; // カメラからの距離(mm)
            }
            else // NotTracked
            {
            }

            this.joints[playerIndex][joint.ID] = pos;
            this.joints2D[playerIndex][joint.ID] = pos2D;
        }

        //----------------------------------------
        // スケルトン描画用の頂点データを作成する
        //----------------------------------------
        void SetSkeletonData(int playerIndex, int trackedPlayerCnt)
        {
            Color skeletonColor = Color.White;
            skeletonColor = anticolors[playerIndex % ncolors];

            int p = trackedPlayerCnt * 19 * 2;

            //頭
            skeletonVertexData[p + 0] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.Head]), skeletonColor);
            skeletonVertexData[p + 1] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderCenter]), skeletonColor); 
            //左肩
            skeletonVertexData[p + 2] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderCenter]), skeletonColor);
            skeletonVertexData[p + 3] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderLeft]), skeletonColor); 
            //左上腕
            skeletonVertexData[p + 4] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderLeft]), skeletonColor);
            skeletonVertexData[p + 5] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ElbowLeft]), skeletonColor);
            //左腕
            skeletonVertexData[p + 6] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ElbowLeft]), skeletonColor);
            skeletonVertexData[p + 7] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.WristLeft]), skeletonColor);
            //左手
            skeletonVertexData[p + 8] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.WristLeft]), skeletonColor);
            skeletonVertexData[p + 9] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HandLeft]), skeletonColor);
            //右肩
            skeletonVertexData[p + 10] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderCenter]), skeletonColor);
            skeletonVertexData[p + 11] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderRight]), skeletonColor);
            //右上腕
            skeletonVertexData[p + 12] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderRight]), skeletonColor);
            skeletonVertexData[p + 13] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ElbowRight]), skeletonColor);
            //右腕
            skeletonVertexData[p + 14] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ElbowRight]), skeletonColor);
            skeletonVertexData[p + 15] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.WristRight]), skeletonColor);
            //右手
            skeletonVertexData[p + 16] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.WristRight]), skeletonColor);
            skeletonVertexData[p + 17] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HandRight]), skeletonColor);
            //上腹部
            skeletonVertexData[p + 18] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.ShoulderCenter]), skeletonColor);
            skeletonVertexData[p + 19] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.Spine]), skeletonColor);
            //下腹部 
            skeletonVertexData[p + 20] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.Spine]), skeletonColor);
            skeletonVertexData[p + 21] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipCenter]), skeletonColor);
            //左腰
            skeletonVertexData[p + 22] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipCenter]), skeletonColor);
            skeletonVertexData[p + 23] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipLeft]), skeletonColor);
            //左大腿
            skeletonVertexData[p + 24] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipLeft]), skeletonColor);
            skeletonVertexData[p + 25] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.KneeLeft]), skeletonColor);
            //左脛
            skeletonVertexData[p + 26] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.KneeLeft]), skeletonColor);
            skeletonVertexData[p + 27] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.AnkleLeft]), skeletonColor);
            //左足
            skeletonVertexData[p + 28] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.AnkleLeft]), skeletonColor);
            skeletonVertexData[p + 29] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.FootLeft]), skeletonColor);
            //右腰
            skeletonVertexData[p + 30] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipCenter]), skeletonColor);
            skeletonVertexData[p + 31] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipRight]), skeletonColor);
            //右大腿
            skeletonVertexData[p + 32] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.HipRight]), skeletonColor);
            skeletonVertexData[p + 33] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.KneeRight]), skeletonColor);
            //右脛
            skeletonVertexData[p + 34] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.KneeRight]), skeletonColor);
            skeletonVertexData[p + 35] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.AnkleRight]), skeletonColor);
            //右足
            skeletonVertexData[p + 36] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.AnkleRight]), skeletonColor);
            skeletonVertexData[p + 37] = new VertexPositionColor(TransToXYPlane(joints2D[playerIndex][JointID.FootRight]), skeletonColor);
        }

        // 3D座標系のXY平面へ射影
        Vector3 TransToXYPlane(Vector3 v)
        {
            return new Vector3(v.X, 480.0f - v.Y, 0.0f); // 3D座標と2D座標はY軸の向きが違う
        }

        //------------------------
        // 姿勢ベクトルを計算する
        //------------------------

        void GetPostures(int playerIndex)
        {
            postures[playerIndex]["BodyForward"] = getBodyForwardVector(playerIndex);
            postures[playerIndex]["BodyUp"] = getBodyUpVector(playerIndex);
        }


        // 体の正面の向きベクトル
        Vector3 getBodyForwardVector(int playerIndex)
        {
            Vector3 fv;
            Vector3 p0, p1, p2;

            p0 = joints[playerIndex][JointID.ShoulderRight];
            p1 = joints[playerIndex][JointID.Spine];
            p2 = joints[playerIndex][JointID.ShoulderLeft];

            Vector3 v0 = new Vector3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
            Vector3 v1 = new Vector3(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);

            fv = Vector3.Cross(v0, v1);
            fv.Normalize();

            return fv;
        }

        // 体の上向きのベクトル
        Vector3 getBodyUpVector(int playerIndex)
        {
            Vector3 uv;
            Vector3 p0, p1;

            p0 = joints[playerIndex][JointID.Spine];
            p1 = joints[playerIndex][JointID.ShoulderCenter];

            uv = new Vector3(p1.X - p0.X, p0.Y - p1.Y, p1.Z - p0.Z); // Yの向きに注意
            uv.Normalize();

            return uv;
        }

        //--------------------------------
        // ジョイントデータのアクセス関数
        //--------------------------------
        public Vector3 getJointPos(JointID id)
        {
            if (joints[this.trackedPlayerIndex[0]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints[this.trackedPlayerIndex[0]][id];
        }

        public Vector3 getJointPos(JointID id, int skeletonIndex)
        {
            if (joints[this.trackedPlayerIndex[skeletonIndex]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints[this.trackedPlayerIndex[skeletonIndex]][id];
        }

        public Vector3 getJointPos2D(JointID id)
        {
            if (joints[this.trackedPlayerIndex[0]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints2D[this.trackedPlayerIndex[0]][id];
        }

        public Vector3 getJointPos2D(JointID id, int skeletonIndex)
        {
            if (joints[this.trackedPlayerIndex[skeletonIndex]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints2D[this.trackedPlayerIndex[skeletonIndex]][id];
        }

        public Vector3 getJointMotion(JointID id)
        {
            if (joints[this.trackedPlayerIndex[0]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints[this.trackedPlayerIndex[0]][id] - joints_pre[this.trackedPlayerIndex[0]][id];
        }

        public Vector3 getJointMotion(JointID id, int skeletonIndex)
        {
            if (joints[this.trackedPlayerIndex[skeletonIndex]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints[this.trackedPlayerIndex[skeletonIndex]][id] - joints_pre[this.trackedPlayerIndex[skeletonIndex]][id];
        }

        public Vector3 getJointMotion2D(JointID id)
        {
            if (joints2D[this.trackedPlayerIndex[0]].ContainsKey(id) == false ||
                joints2D_pre[this.trackedPlayerIndex[0]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints2D[this.trackedPlayerIndex[0]][id] - joints2D_pre[this.trackedPlayerIndex[0]][id];
        }

        public Vector3 getJointMotion2D(JointID id, int skeletonIndex)
        {
            if (joints2D[this.trackedPlayerIndex[skeletonIndex]].ContainsKey(id) == false ||
                joints2D_pre[this.trackedPlayerIndex[skeletonIndex]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return joints2D[this.trackedPlayerIndex[skeletonIndex]][id] - joints2D_pre[this.trackedPlayerIndex[skeletonIndex]][id];
        }

        public Vector3 getJointPosture(string id)
        {
            if (postures[this.trackedPlayerIndex[0]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return postures[this.trackedPlayerIndex[0]][id];
        }

        public Vector3 getJointPosture(string id, int skeletonIndex)
        {
            if (postures[this.trackedPlayerIndex[skeletonIndex]].ContainsKey(id) == false)
            {
                return Vector3.Zero;
            }
            return postures[this.trackedPlayerIndex[skeletonIndex]][id];
        }
    }
}
