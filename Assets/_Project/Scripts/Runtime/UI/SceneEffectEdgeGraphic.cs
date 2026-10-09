using UnityEngine;
using UnityEngine.UI;

namespace DeepSleep.Runtime.UI
{
    /// <summary>四边同一纹理等比重复UV；长屏只增加重复数，不拉长素材。Mirror采样避免非循环原稿的接缝。</summary>
    public sealed class SceneEffectEdgeGraphic : MaskableGraphic
    {
        public Texture EdgeTexture;
        [Min(0)] public float Thickness;
        public float Scroll;
        public Vector4 EdgeWeights;
        public override Texture mainTexture => EdgeTexture;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (EdgeTexture == null || Thickness <= 0) return;
            Rect r = rectTransform.rect;
            float tile = Thickness * EdgeTexture.width / EdgeTexture.height;
            AddQuad(mesh, new Vector2(r.xMin,r.yMax), new Vector2(r.xMax,r.yMax), new Vector2(r.xMin,r.yMax-Thickness), new Vector2(r.xMax,r.yMax-Thickness), r.width/tile, EdgeWeights.x);
            AddQuad(mesh, new Vector2(r.xMax,r.yMin), new Vector2(r.xMin,r.yMin), new Vector2(r.xMax,r.yMin+Thickness), new Vector2(r.xMin,r.yMin+Thickness), r.width/tile, EdgeWeights.y);
            AddQuad(mesh, new Vector2(r.xMin,r.yMin), new Vector2(r.xMin,r.yMax), new Vector2(r.xMin+Thickness,r.yMin), new Vector2(r.xMin+Thickness,r.yMax), r.height/tile, EdgeWeights.z);
            AddQuad(mesh, new Vector2(r.xMax,r.yMax), new Vector2(r.xMax,r.yMin), new Vector2(r.xMax-Thickness,r.yMax), new Vector2(r.xMax-Thickness,r.yMin), r.height/tile, EdgeWeights.w);
        }

        private void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, float repeats, float weight)
        {
            int i = mesh.currentVertCount;
            Color tint = color; tint.a *= weight;
            mesh.AddVert(a,tint,new Vector2(Scroll,1)); mesh.AddVert(b,tint,new Vector2(Scroll+repeats,1));
            mesh.AddVert(c,tint,new Vector2(Scroll,0)); mesh.AddVert(d,tint,new Vector2(Scroll+repeats,0));
            mesh.AddTriangle(i,i+1,i+2); mesh.AddTriangle(i+2,i+1,i+3);
        }
    }
}
