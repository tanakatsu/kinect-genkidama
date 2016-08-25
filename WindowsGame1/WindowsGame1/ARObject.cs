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
    class ARObject
    {
        public bool IsVisible = true;
        public Vector3 Color = new Vector3(1.0f, 1.0f, 1.0f);

        protected string modelName;
        protected Model model;
        protected float rot_x, rot_y, rot_z; // 回転角
        protected Matrix world;
        protected AttachedPos attatchedTo; // アタッチする部位

 	    protected float[] persParam = new float[10000]; // 距離ピクセル変換テーブル   
        float scale = 50.0f; // オブジェクトの基準スケール値（とりあえず決め打ちの固定値)

        public ARObject(string modelName)
        {
            this.IsVisible = false;
            this.modelName = modelName;
            this.attatchedTo = new AttachedPos(JointID.Head); // 何も指定されないときは頭にアタッチ

            Initialize();
        }

        public ARObject(string modelName, AttachedPos attachedTo)
        {
            this.IsVisible = false;
            this.modelName = modelName;
            this.attatchedTo = attachedTo;

            Initialize();
        }
 
        //
        // Initialzie
        //
        public void Initialize()
        {
            // 距離係数テーブル作成
            for (int i = 0; i < 10000; i++)
            {
                persParam[i] = 117846 * (float)Math.Pow((double)i, -1.01f);
            }
        }

        //
        // Load
        //
        public virtual void Load(ContentManager content)
        {
            //------------------
            // 3Dモデルのロード
            //------------------
            try
            {
                model = content.Load<Model>(modelName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.ToString());
                Environment.Exit(-1);
            }              
        }

        //
        // Update
        //
        public virtual void Update(Game1 game)
        {
            if (IsVisible)
            {
                Vector3 objPos, pos, dirSrc, dirDst;
                float dirScale;

                pos = game.getJointPos2D(this.attatchedTo.pos);
                dirSrc = game.getJointPos2D(this.attatchedTo.dirSrc);
                dirDst = game.getJointPos2D(this.attatchedTo.dirDst);
                dirScale = this.attatchedTo.dirScale;

                objPos = pos + dirScale * (dirDst - dirSrc);

                // 変換行列作成
                world = getScaleMatrix(objPos.Z) *
                    getRotateMatrixFromFv(game.getJointPosture("BodyForward")) * // 回転
                    getRotateMatrixFromUv(game.getJointPosture("BodyUp")) * // 回転
                    getTranslationMatrix(objPos); // 移動
            }
            else
            {
                world = Matrix.CreateScale(0.0f); // サイズ0にする
            }
        }

        //
        // Draw
        //
        public virtual void Draw(Matrix view, Matrix projection)
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
                    effect.DiffuseColor = Color;
                }
                mesh.Draw();
            }
        }

        ///
        /// 汎用関数
        ///

        public Matrix getScaleMatrix(float depth)
        {
            float scale_param = persParam[(int)depth] / persParam[1800]; // とりあえず1.8mの立ち位置を基準にする
            return Matrix.CreateScale(scale * scale_param);
        }

        public Matrix getTranslationMatrix(Vector3 v)
        {
            Vector3 v_tmp = new Vector3(v.X, 480.0f - v.Y, 0.0f); // 2D座標と3D座標でY軸の向き反対、Z座標は0にする
            return Matrix.CreateTranslation(v_tmp);
        }

        public Matrix getRotateMatrixFromFv(Vector3 v)
        {
            Vector3 v_tmp = new Vector3(v.X, 0, v.Z);

            rot_y = (float)Math.Acos((double)v_tmp.Z / v_tmp.Length()); // Y軸に対する回転

            Vector3 nv = Vector3.Cross(new Vector3(0.0f, 0.0f, 1.0f), v_tmp); // 外積の法線ベクトルから回転方向を求める
            if (nv.Y < 0)
            {
                rot_y = -rot_y; 
            }

            return Matrix.CreateRotationY(rot_y);
        }

        public Matrix getRotateMatrixFromUv(Vector3 v)
        {
            Vector3 v_tmp1 = new Vector3(0, v.Y, v.Z);
            Vector3 v_tmp2 = new Vector3(v.X, v.Y, 0);

            rot_x = (float)Math.Acos((double)v_tmp1.Y / v_tmp1.Length()); // X軸に対する回転
            Vector3 nv1 = Vector3.Cross(new Vector3(0.0f, 1.0f, 0.0f), v_tmp1); // 外積の法線ベクトルから回転方向を求める
            if (nv1.X < 0)
            {
                rot_x = -rot_x; 
            }

            rot_z = (float)Math.Acos((double)v_tmp2.Y / v_tmp2.Length()); // Z軸に対する回転
            Vector3 nv2 = Vector3.Cross(new Vector3(0.0f, 1.0f, 0.0f), v_tmp2); // 外積の法線ベクトルから回転方向を求める
            if (nv2.Z > 0)
            {
                rot_z = -rot_z; 
            }

            return Matrix.CreateRotationX(rot_x) * Matrix.CreateRotationZ(rot_z);
        }
    }
}
