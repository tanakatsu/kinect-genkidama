using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Diagnostics;
using Microsoft.Research.Kinect; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Nui; // Microsoft.Research.Kinectへの参照の追加が必要
using Microsoft.Research.Kinect.Audio; // Microsoft.Research.Kinectへの参照の追加が必要

namespace WindowsGame1
{
    class AttachedPos // パーツをアタッチする部位
    {
        public JointID pos; // 基準点
        public JointID dirSrc; // 方向ベクトル始点
        public JointID dirDst; // 方向ベクトル終点
        public float dirScale; // 方向ベクトル大きさ

        public AttachedPos(JointID pos)
        {
            this.pos = pos;
            this.dirSrc = JointID.Head;
            this.dirDst = JointID.Head;
            this.dirScale = 0.0f;
        }

        public AttachedPos(JointID pos, JointID dirSrc, JointID dirDst, float dirScale)
        {
            this.pos = pos;
            this.dirSrc = dirSrc;
            this.dirDst = dirDst;
            this.dirScale = dirScale;
        }
    }
}
