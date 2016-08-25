using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    enum SwipeDirection { SwipeRight, SwipeLeft, SwipeUp, SwipeDown };

    class SwipeDetect
    {
        List<Vector3> handDataList; // 直近のNサンプルデータ
        List<Vector3> elbowDataList; // 直近のNサンプルデータ
        List<Vector3> shoulderDataList; // 直近のNサンプルデータ
        int samplingNumber = 90; // 保持するデータ数

        public SwipeDetect()
        {
            handDataList = new List<Vector3>();
            elbowDataList = new List<Vector3>();
            shoulderDataList = new List<Vector3>();
        }

        public void PutData(Vector3 handData, Vector3 elbowData, Vector3 shoulderData)
        {
            List<Vector3> listHand = handDataList;
            List<Vector3> listElbow = elbowDataList;
            List<Vector3> listShoulder = shoulderDataList;

            if (listHand.Count == samplingNumber)
            {
                listHand.RemoveAt(listHand.Count - 1); // 末尾のデータを削除
                listElbow.RemoveAt(listElbow.Count - 1);
                listShoulder.RemoveAt(listShoulder.Count - 1);
            }

            listHand.Insert(0, handData); // 先頭にデータを追加
            listElbow.Insert(0, elbowData);
            listShoulder.Insert(0, shoulderData);
        }

        private void Clear()
        {
            handDataList.Clear();
            elbowDataList.Clear();
            shoulderDataList.Clear();
        }

        public bool DetectSwipe(int targetDirection, float margin)
        {
            bool result = false;
            int state = 0;

            if (handDataList.Count <= 1)
            {
                return result;
            }

            // 8サンプル以内で最も動きがない点（動き開始点となるデータ）を探す
            int refPointIndex1 = 0; // 動き開始点
            float dx;
            float min = Math.Abs(handDataList[0].X - handDataList[1].X);
            for (int i = 2; i < 8 && i < handDataList.Count - 1; i++)
            {
                dx = Math.Abs(handDataList[i - 1].X - handDataList[i].X);

                if (Math.Abs(dx) < min)
                {
                    min = Math.Abs(dx);
                    refPointIndex1 = i;
                }
            }

            if (min < 2) // 開始点は静止状態かをチェック
            {
                ++state;
            }

            if (state == 1)
            {
                switch (targetDirection)
                {
                    case (int)SwipeDirection.SwipeRight:
                        if (handDataList[0].X - handDataList[refPointIndex1].X > 45) // X方向に動いた量をチェック
                        {
                            ++state;
                        }
                        break;

                    case (int)SwipeDirection.SwipeLeft:
                        if (handDataList[0].X - handDataList[refPointIndex1].X < -45) // X方向に動いた量をチェック
                        {
                            ++state;
                        }
                        break;

                    case (int)SwipeDirection.SwipeUp:
                        if (handDataList[0].Y - handDataList[refPointIndex1].Y < -30) // Y方向に動いた量をチェック
                        {
                            ++state;
                        }
                        break;

                    case (int)SwipeDirection.SwipeDown:
                        if (handDataList[0].Y - handDataList[refPointIndex1].Y > 30) // Y方向に動いた量をチェック
                        {
                            ++state;
                        }
                        break;
                }
            }

            if (state == 2)
            {
                switch (targetDirection)
                {
                    case (int)SwipeDirection.SwipeRight:
                    case (int)SwipeDirection.SwipeLeft:
                        if (Math.Abs(handDataList[0].Y - handDataList[refPointIndex1].Y) < 15) // Y方向に動いた量をチェック
                        {
                            ++state;
                        }
                        break;

                    case (int)SwipeDirection.SwipeUp:
                    case (int)SwipeDirection.SwipeDown:
                        if (Math.Abs(handDataList[0].X - handDataList[refPointIndex1].X) < 15) // X方向に動いた量をチェック
                        {
                            ++state;
                        }
                        break;
                }
            }

            if (state == 3)
            {
                switch (targetDirection)
                {
                    case (int)SwipeDirection.SwipeRight:
                    case (int)SwipeDirection.SwipeLeft:
                        if (Math.Abs(handDataList[refPointIndex1].X - elbowDataList[refPointIndex1].X) < margin) // 開始地点の位置をチェック（手と肘の位置関係をチェック）
                        {
                            ++state;
                        }
                        break;

                    case (int)SwipeDirection.SwipeUp:
                    case (int)SwipeDirection.SwipeDown:
                        if (Math.Abs(handDataList[refPointIndex1].X - elbowDataList[refPointIndex1].X) < margin &&
                            Math.Abs(handDataList[refPointIndex1].Y - shoulderDataList[refPointIndex1].Y) < margin) // 開始地点の位置をチェック（手と肘の位置関係をチェック）
                        {
                            ++state;
                        }
                        break;
                }
            }

            if (state == 4)
            {
                result = true;

                // 検出したら一度クリアすべし (二重検知を抑制するため）
                Clear();
            }

            return result;
        }

    }
}
