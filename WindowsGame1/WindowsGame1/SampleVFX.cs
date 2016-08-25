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
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

using System.Diagnostics;
using Microsoft.Research.Kinect; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Nui; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Audio; // Microsoft.Research.Kinectへの参照の追加が必要

namespace WindowsGame1
{
    class SampleVFX : ARObject
    {
        int actionCnt = 0;
        bool pose_handsupY = false;
        bool pose_still = false;
        bool pose_sweepdown = false;
        bool pose_handsonwaist = false;

        int pose_handsupY_cnt = 0;
        int pose_still_cnt = 0;
        int pose_sweepdown_cnt = 0;
        int pose_handsonwaist_cnt = 0;

        int actionState; // 状態
        int effectLifeTimer = 0;

        public SampleVFX(string modelName) : base(modelName)
        {
        }

        public SampleVFX(string modelName, AttachedPos attachedTo)
            : base(modelName, attachedTo)
        {
        }

        //
        // Update
        //
        public override void Update(Game1 game)
        {
            if (IsVisible)
            {
                UpdateAction(game);
                UpdateVFX(game);
            }
        }

        private void UpdateAction(Game1 game)
        {
            //
            // Y字ポーズの認識
            //
            Vector3 head;
            Vector3 righthand, lefthand;
            Vector3 rightshoulder, leftshoulder;
            Vector3 handscenter;

            head = game.getJointPos2D(JointID.Head); 
            righthand = game.getJointPos2D(JointID.HandRight); 
            lefthand = game.getJointPos2D(JointID.HandLeft);
            handscenter = (righthand + lefthand) / 2;
            rightshoulder = game.getJointPos2D(JointID.ShoulderRight); 
            leftshoulder = game.getJointPos2D(JointID.ShoulderLeft);

            float handsdistance = Math.Abs(lefthand.X - righthand.X);
            float shoulderwidth = Math.Abs(leftshoulder.X - rightshoulder.X);

            if (handsdistance > shoulderwidth && // 両手の間隔が肩幅より大きい
               handscenter.Y < head.Y) // 両手が頭より高い位置にある
            {
                pose_handsupY = true;
                ++pose_handsupY_cnt;
            }
            else
            {
                pose_handsupY = false;
                pose_handsupY_cnt = 0;
            }

            //
            // 静止の認識
            //

            Vector3 righthand_mv = game.getJointMotion2D(JointID.HandRight);
            Vector3 lefthand_mv = game.getJointMotion2D(JointID.HandLeft);

            if (Math.Abs(righthand_mv.X) < 15
                && Math.Abs(righthand_mv.Y) < 15
                && Math.Abs(lefthand_mv.X) < 15
                && Math.Abs(lefthand_mv.Y) < 15)
            {
                pose_still = true;
                ++pose_still_cnt;
            }
            else
            {
                pose_still = false;
                pose_still_cnt = 0;
            }

            //
            // 両手の降りおろしの認識
            //

            if (Math.Abs(righthand.Y - lefthand.Y) <= 30
                && Math.Abs(righthand_mv.Y) > 20)
            {
                pose_sweepdown = true;
                ++pose_sweepdown_cnt;
            }
            else
            {
                pose_sweepdown = false;
                pose_sweepdown_cnt = 0;
            }

            //
            // 両手が腰より下にある状態の認識
            //
            Vector3 torso = game.getJointPos(JointID.HipCenter);

            if (righthand.Y > torso.Y &&
                lefthand.Y > torso.Y)
            {
                pose_handsonwaist = true;
                ++pose_handsonwaist_cnt;
            }
            else
            {
                pose_handsonwaist = false;
                pose_handsonwaist_cnt = 0;
            }

            ///
            /// アクション認識ステートマシン
            ///

            switch (actionState)
            {
                case 0: // 初期状態
                    if (pose_handsupY_cnt > 60 && pose_still_cnt > 60)
                    {
                        ++actionState;
                    }
                    break;
                case 1: // 両手をあげた状態
                    if (pose_sweepdown)
                    {
                        ++actionState; // 振り下ろしていたら次の状態へ
                        effectLifeTimer = 90;
                    }
                    else if (pose_handsupY == false)
                    {
                        actionState = 0;
                    }
                    break;
                case 2: // 降りおろし中
                    if (pose_handsonwaist)
                    {
                        ++actionState; // 発動
                    }
                    else if (pose_sweepdown)
                    {
                        // 状態継続
                    }
                    else
                    {
                        actionState = 0;
                    }
                    break;
                case 3: // 発動
                    --effectLifeTimer;
                    if (effectLifeTimer == 0)
                    {
                        actionState = 0; // 初期状態へ
                    }
                    break;
            }
        }

        private void UpdateVFX(Game1 game)
        {
            Vector3 righthand, lefthand, handsCenter;

            righthand = game.getJointPos2D(JointID.HandRight);
            lefthand = game.getJointPos2D(JointID.HandLeft);

            handsCenter = (righthand + lefthand) / 2;

            if (actionState == 1)
            {
                if (actionCnt < 500)
                {
                    ++actionCnt;
                }
            }
            else if (actionState == 2)
            {
            }
            else if (actionState == 3)
            {
                actionCnt += 30;
            }
            else
            {
                actionCnt = 0;
            }

            // 変換行列作成
            world = Matrix.CreateScale(actionCnt / 10) *
                getTranslationMatrix(handsCenter); // 移動

        }

        //
        // Draw
        //
        public override void Draw(Matrix view, Matrix projection)
        {
            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.World = this.world;
                    effect.View = view;
                    effect.Projection = projection;

                    // 形状が認識しやすいように光をあてる for Debug
                    effect.EnableDefaultLighting();
                    effect.LightingEnabled = true;
                }
                mesh.Draw();
            }
        }
    }
}
