using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
// Contour 类
namespace JJEffectClipperTool
{
    public class Contour
    {
        public string name;
        public bool isDrawing = false;
        public List<Vector2> points = new List<Vector2>();
        public List<Contour> holes = new List<Contour>();

        public Contour(string name)
        {
            this.name = name;
        }
    }
}