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

using Microsoft.Research.Kinect; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Nui; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Audio; // Microsoft.Research.Kinectへの参照の追加が必要

namespace WindowsGame1
{
    enum DataType { Position, Motion }; // データタイプ

    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class GestureDetect : Microsoft.Xna.Framework.GameComponent
    {
        Game1 game;
        SpriteFont fontArial = null; //フォント(for debug)

        Dictionary<JointID, List<Vector3>> samplingData; // 直近のNサンプルデータ
        Dictionary<JointID, List<Vector3>> samplingDataMotion; // 直近のNサンプルデータ（動き量）
        int samplingNumber = 90; // 保持するデータ数

        public bool gesture_ok = false;
        public bool gesture_byebye = false;
        public bool gesture_swiperight = false;
        public bool gesture_swipeleft = false;
        public bool gesture_swipeup = false;
        public bool gesture_swipedown = false;

        SwipeDetect swipeDetect; // Swipe検出モジュール

        public GestureDetect(Game1 game)
            : base(game)
        {
            // TODO: Construct any child components here
            this.game = game;

            swipeDetect = new SwipeDetect();
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            // TODO: Add your initialization code here
          
            samplingData = new Dictionary<JointID, List<Vector3>>();
            samplingData.Add(JointID.Head, new List<Vector3>());
            samplingData.Add(JointID.ShoulderCenter, new List<Vector3>());
            samplingData.Add(JointID.ShoulderLeft, new List<Vector3>());
            samplingData.Add(JointID.ElbowLeft, new List<Vector3>());
            samplingData.Add(JointID.WristLeft, new List<Vector3>());
            samplingData.Add(JointID.HandLeft, new List<Vector3>());
            samplingData.Add(JointID.ShoulderRight, new List<Vector3>());
            samplingData.Add(JointID.ElbowRight, new List<Vector3>());
            samplingData.Add(JointID.WristRight, new List<Vector3>());
            samplingData.Add(JointID.HandRight, new List<Vector3>());
            samplingData.Add(JointID.Spine, new List<Vector3>());
            samplingData.Add(JointID.HipCenter, new List<Vector3>());
            samplingData.Add(JointID.HipLeft, new List<Vector3>());
            samplingData.Add(JointID.KneeLeft, new List<Vector3>());
            samplingData.Add(JointID.AnkleLeft, new List<Vector3>());
            samplingData.Add(JointID.FootLeft, new List<Vector3>());
            samplingData.Add(JointID.HipRight, new List<Vector3>());
            samplingData.Add(JointID.KneeRight, new List<Vector3>());
            samplingData.Add(JointID.AnkleRight, new List<Vector3>());
            samplingData.Add(JointID.FootRight, new List<Vector3>());

            samplingDataMotion = new Dictionary<JointID, List<Vector3>>();
            samplingDataMotion.Add(JointID.Head, new List<Vector3>());
            samplingDataMotion.Add(JointID.ShoulderCenter, new List<Vector3>());
            samplingDataMotion.Add(JointID.ShoulderLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.ElbowLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.WristLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.HandLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.ShoulderRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.ElbowRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.WristRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.HandRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.Spine, new List<Vector3>());
            samplingDataMotion.Add(JointID.HipCenter, new List<Vector3>());
            samplingDataMotion.Add(JointID.HipLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.KneeLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.AnkleLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.FootLeft, new List<Vector3>());
            samplingDataMotion.Add(JointID.HipRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.KneeRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.AnkleRight, new List<Vector3>());
            samplingDataMotion.Add(JointID.FootRight, new List<Vector3>());

            // フォントの読み込み
            this.fontArial = this.game.Content.Load<SpriteFont>("Arial"); // ContentにArial.spritefontを追加しておく

            base.Initialize();
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // TODO: Add your update code here

            // 状態をクリア
            gesture_ok = false;
            gesture_byebye = false;

            // データを格納
            PutData(JointID.HandRight, game.getJointPos2D(JointID.HandRight));
            PutData(JointID.HandLeft, game.getJointPos2D(JointID.HandLeft));
            PutData(JointID.ShoulderCenter, game.getJointPos2D(JointID.ShoulderCenter));
            PutData(JointID.ElbowRight, game.getJointPos2D(JointID.ElbowRight));
            PutData(JointID.ElbowLeft, game.getJointPos2D(JointID.ElbowLeft));
            PutData(JointID.ShoulderRight, game.getJointPos2D(JointID.ShoulderRight));

            swipeDetect.PutData(game.getJointPos2D(JointID.HandRight), game.getJointPos2D(JointID.ElbowRight), game.getJointPos2D(JointID.ShoulderRight));

            // OKジェスチャの認識
            if (samplingData[JointID.HandRight][0].X < samplingData[JointID.ElbowRight][0].X // 手が肘より内側
                && samplingData[JointID.HandLeft][0].X > samplingData[JointID.ElbowLeft][0].X
                && samplingData[JointID.HandRight][0].Y < samplingData[JointID.ElbowRight][0].Y // 手が肘より上側
                && samplingData[JointID.HandLeft][0].Y < samplingData[JointID.ElbowLeft][0].Y
                && samplingData[JointID.ElbowRight][0].Y < samplingData[JointID.ShoulderCenter][0].Y // 肘が肩より上側
                && samplingData[JointID.ElbowLeft][0].Y < samplingData[JointID.ShoulderCenter][0].Y)
            {
                gesture_ok = true;
            }

            // バイバイジェスチャの認識
            gesture_byebye = DetectByeBye();

            // Swipeジェスチャの認識
            float margin = Math.Abs(samplingData[JointID.ShoulderRight][0].X - samplingData[JointID.ShoulderCenter][0].X);
            gesture_swiperight = swipeDetect.DetectSwipe((int)SwipeDirection.SwipeRight, margin);
            gesture_swipeleft = swipeDetect.DetectSwipe((int)SwipeDirection.SwipeLeft, margin);
            gesture_swipeup = swipeDetect.DetectSwipe((int)SwipeDirection.SwipeUp, margin);
            gesture_swipedown = swipeDetect.DetectSwipe((int)SwipeDirection.SwipeDown, margin);

            base.Update(gameTime);
        }

        // データを可視化
        public void Draw(SpriteBatch spriteBatch)
        {
            if (samplingData[JointID.HandRight].Count == 0)
            {
                return;
            }


            PrimitiveLine brush = new PrimitiveLine(this.game.GraphicsDevice);
            spriteBatch.Begin();

            Vector2 line = new Vector2();
            Vector3 prevPoint = samplingData[JointID.HandRight][0];
            Vector3 prevprevPoint = samplingData[JointID.HandRight][0];
            Vector2 drawingPointFrom = new Vector2(0, 480.0f - prevPoint.X * 0.75f);
            foreach (Vector3 point in samplingData[JointID.HandRight])
            {
                line.X = 10;
                line.Y = (point.X - prevPoint.X) * -0.75f;

                brush.CreateLine(line);
                brush.Position = drawingPointFrom;
                brush.Render(spriteBatch);

                // 変曲点を表示
                /*
                if ((point.X - prevPoint.X) * (prevPoint.X - prevprevPoint.X) <= 0 &&
                    (point.X != prevPoint.X || prevPoint.X != prevprevPoint.X))
                 */
                if ((point.X - prevPoint.X) * (prevPoint.X - prevprevPoint.X) < 0)
                {
                    brush.CreateCircle(5, 100);
                    brush.Position = drawingPointFrom;
                    brush.Render(spriteBatch);
                }

                prevprevPoint = prevPoint;
                prevPoint = point;
                drawingPointFrom += line;
            }

            spriteBatch.End();

            // Swipe結果を表示 (for Debug)
            string swipeResult = "";
            if (gesture_swiperight)
            {
                swipeResult = "Swipe Right";
            }
            else if (gesture_swipeleft)
            {
                swipeResult = "Swipe Left";
            }
            else if (gesture_swipeup)
            {
                swipeResult = "Swipe Up";
            }
            else if (gesture_swipedown)
            {
                swipeResult = "Swipe Down";
            }

            spriteBatch.Begin();
            spriteBatch.DrawString(fontArial, swipeResult, new Vector2(0, 20), Color.White);           
            spriteBatch.End();
        }

        private void PutData(JointID id, Vector3 data)
        {
            List<Vector3> list = samplingData[id];

            if (list.Count == samplingNumber)
            {
                list.RemoveAt(list.Count - 1); // 末尾のデータを削除
            }

            list.Insert(0, data); // 先頭にデータを追加
        }

        private void PutData(JointID id, Vector3 data, int type)
        {
            List<Vector3> list;

            if (type == (int)DataType.Position)
            {
                list = samplingData[id];
            }
            else if (type == (int)DataType.Motion)
            {
                list = samplingDataMotion[id];
            }
            else
            {
                list = samplingData[id];
            }

            if (list.Count == samplingNumber)
            {
                list.RemoveAt(list.Count - 1); // 末尾のデータを削除
            }

            list.Insert(0, data); // 先頭にデータを追加
        }

        // バイバイのジェスチャを認識
        private bool DetectByeBye()
        {
            bool result = false;
            int state = 0;
            List<Vector3> dataList = samplingData[JointID.HandRight];

            // 15サンプル以内で最も離れた点（半周期となるデータ）を探す
            Vector3 initialPoint = dataList[0];
            Vector3 refPoint1 = initialPoint;
            int refPointIndex1 = 0; // 半周期の長さ
            float dx, max = 0.0f;
            for (int i = 1; i < 15 && i < dataList.Count; i++)
            {
                Vector3 point = dataList[i];
                dx = Math.Abs(point.X - initialPoint.X);

                if (dx > max)
                {
                    max = dx;
                    refPoint1 = point;
                    refPointIndex1 = i;
                }
            }

            if (refPointIndex1 > 3 && Math.Abs(refPoint1.X - initialPoint.X) > 30) // 周期と手のふり幅のチェック
            {
                ++state;
            }

            if (state == 1) 
            {
                if (dataList.Count > refPointIndex1 * 2)
                {
                    Vector3 point = dataList[refPointIndex1 * 2]; // 1周期となる点

                    if (Math.Abs(point.X - initialPoint.X) < 15) // 初期値との差をチェック
                    {
                        ++state;
                    }
                }
            }

            if (state == 2)
            {
                if (dataList.Count > refPointIndex1 * 3)
                {
                    Vector3 point = dataList[refPointIndex1 * 3]; // 1.5周期となる点

                    if (Math.Abs(point.X - refPoint1.X) < 15) // 半周期の点との差をチェック
                    {
                        ++state;
                    }
                }
            }

            if (state == 3)
            {
                if (dataList.Count > refPointIndex1 * 4)
                {
                    Vector3 point = dataList[refPointIndex1 * 4]; // 2周期となる点

                    if (Math.Abs(point.X - initialPoint.X) < 15) // 初期値との差をチェック
                    {
                        ++state;
                    }
                }
            }

            if (state == 4)
            {
                result = true;
            }

            return result;
        }

    }
}
