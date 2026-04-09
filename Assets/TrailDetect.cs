using System;
using System.Collections.Generic;
using UnityEngine;

namespace GestureRecognition {
    // 定义点云中的点
    [System.Serializable]
    public class GesturePoint {
        public Vector3 Pos;//获取点的三维坐标
        public int StrokeID;//区分不同笔画的ID

        public GesturePoint(Vector3 pos, int strokeId) {
            this.Pos = pos;
            this.StrokeID = strokeId;
        }
    }

    // 手势模板类
    [Serializable]
    public class Gesture {
        public string Name;
        public GesturePoint[] Points;

        public Gesture(string name, GesturePoint[] points) {
            this.Name = name;
            this.Points = RecognizeUtils.Normalize(points);
        }
    }

    public static class RecognizeUtils {
        private const int ResampleCount = 32; // 采样点数，数值越大越精确但消耗越高

        // 预处理：重采样 -> 平移至原点 -> 缩放
        public static GesturePoint[] Normalize(GesturePoint[] points) {
            GesturePoint[] resampled = Resample(points, ResampleCount);
            GesturePoint[] translated = TranslateToOrigin(resampled);
            GesturePoint[] scaled = Scale(translated);
            return scaled;
        }

        private static GesturePoint[] Resample(GesturePoint[] points, int n) {
            double I = PathLength(points) / (n - 1);
            double D = 0;
            List<GesturePoint> newPoints = new List<GesturePoint> { points[0] };
            List<GesturePoint> srcPoints = new List<GesturePoint>(points);

            for (int i = 1; i < srcPoints.Count; i++) {
                if (srcPoints[i].StrokeID == srcPoints[i - 1].StrokeID) {
                    float d = Vector3.Distance(srcPoints[i - 1].Pos, srcPoints[i].Pos);
                    if (D + d >= I) {
                        float t = (float)((I - D) / d);
                        Vector3 q = Vector3.Lerp(srcPoints[i - 1].Pos, srcPoints[i].Pos, t);
                        GesturePoint p = new GesturePoint(q, srcPoints[i].StrokeID);
                        newPoints.Add(p);
                        srcPoints.Insert(i, p);
                        D = 0;
                    }
                    else D += d;
                }
            }

            if (newPoints.Count == n - 1)
                newPoints.Add(points[points.Length - 1]);
            
            return newPoints.ToArray();
        }

        private static GesturePoint[] TranslateToOrigin(GesturePoint[] points) {
            Vector3 centroid = Vector3.zero;
            foreach (var p in points) centroid += p.Pos;
            centroid /= points.Length;

            GesturePoint[] newPoints = new GesturePoint[points.Length];
            for (int i = 0; i < points.Length; i++) {
                newPoints[i] = new GesturePoint(points[i].Pos - centroid, points[i].StrokeID);
            }
            return newPoints;
        }

        private static GesturePoint[] Scale(GesturePoint[] points) {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var p in points) {
                maxX = Mathf.Max(maxX, p.Pos.x); minX = Mathf.Min(minX, p.Pos.x);
                maxY = Mathf.Max(maxY, p.Pos.y); minY = Mathf.Min(minY, p.Pos.y);
                maxZ = Mathf.Max(maxZ, p.Pos.z); minZ = Mathf.Min(minZ, p.Pos.z);
            }

            float size = Mathf.Max(maxX - minX, maxY - minY, maxZ - minZ);
            GesturePoint[] newPoints = new GesturePoint[points.Length];
            for (int i = 0; i < points.Length; i++) {
                newPoints[i] = new GesturePoint(points[i].Pos / size, points[i].StrokeID);
            }
            return newPoints;
        }

        private static float PathLength(GesturePoint[] points) {
            float d = 0;
            for (int i = 1; i < points.Length; i++) {
                if (points[i].StrokeID == points[i - 1].StrokeID)
                    d += Vector3.Distance(points[i - 1].Pos, points[i].Pos);
            }
            return d;
        }

        // 贪婪匹配：对比两个点云的相似度
        public static float GreedyCloudMatch(GesturePoint[] points1, GesturePoint[] points2) {
            int n = points1.Length;
            float eps = 0.5f; 
            int step = Mathf.FloorToInt(Mathf.Pow(n, 1.0f - eps));
            float minDistance = float.MaxValue;

            for (int i = 0; i < n; i += step) {
                float d1 = CloudDistance(points1, points2, i);
                float d2 = CloudDistance(points2, points1, i);
                minDistance = Mathf.Min(minDistance, d1, d2);
            }
            return minDistance;
        }

        private static float CloudDistance(GesturePoint[] points1, GesturePoint[] points2, int startIndex) {
            int n = points1.Length;
            bool[] matched = new bool[n];
            float sum = 0;
            int i = startIndex;
            do {
                int index = -1;
                float minD = float.MaxValue;
                for (int j = 0; j < n; j++) {
                    if (!matched[j]) {
                        float d = Vector3.Distance(points1[i].Pos, points2[j].Pos);
                        if (d < minD) {
                            minD = d;
                            index = j;
                        }
                    }
                }
                matched[index] = true;
                sum += minD;
                i = (i + 1) % n;
            } while (i != startIndex);
            return sum;
        }
    }
}

