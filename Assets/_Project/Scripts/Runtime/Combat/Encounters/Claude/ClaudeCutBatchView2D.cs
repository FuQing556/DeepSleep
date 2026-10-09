using DeepSleep.Runtime.Combat.Beams;
using UnityEngine;

namespace DeepSleep.Runtime.Combat.Encounters.Claude
{
    /// <summary>一批刀线一个网格；纹理按原宽高比重复，不整图拉长。组件由Prefab显式装配。</summary>
    public sealed class ClaudeCutBatchView2D : MonoBehaviour
    {
        public MeshFilter Filter;
        public MeshRenderer Renderer;
        public int Capacity;
        public bool RepeatTexture;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector2[] _uv;
        private MaterialPropertyBlock _properties;
        private readonly System.Collections.Generic.List<int> _triangles = new();
        public bool TryValidate(out string reason)
        {
            if (Filter == null || Renderer == null || Capacity < 1 || Renderer.sharedMaterials.Length == 0)
            { reason = "切割网格引用/容量/材质缺失。"; return false; }
            foreach (var m in Renderer.sharedMaterials)
                if (m == null || !m.HasProperty("_BaseColor") || !m.HasProperty("_BaseMap"))
                { reason = "切割材质必须支持BaseMap/BaseColor。"; return false; }
            reason = string.Empty; return true;
        }
        private void Awake()
        {
            if (!TryValidate(out var reason)) { Debug.LogError(reason, this); enabled = false; return; }
            _mesh = new Mesh { name = "ClaudeCutBatch" }; _mesh.MarkDynamic(); Filter.sharedMesh = _mesh;
            _vertices = new Vector3[Capacity * 4]; _uv = new Vector2[Capacity * 4];
            _properties = new MaterialPropertyBlock(); Hide();
        }
        public void SetLayout(BeamLaneSnapshot[] lanes, int count, float width)
        {
            if (_mesh == null) return;
            var materials = Renderer.sharedMaterials;
            _mesh.Clear(); _mesh.subMeshCount = materials.Length;
            for (int i = 0; i < count; i++)
            {
                var lane = lanes[i]; Vector2 side = new Vector2(-lane.Direction.y, lane.Direction.x) * (width * .5f);
                int v = i * 4;
                _vertices[v] = transform.InverseTransformPoint(lane.Origin - side);
                _vertices[v + 1] = transform.InverseTransformPoint(lane.Origin + side);
                _vertices[v + 2] = transform.InverseTransformPoint(lane.End + side);
                _vertices[v + 3] = transform.InverseTransformPoint(lane.End - side);
                var texture = materials[i % materials.Length].GetTexture("_BaseMap");
                float repeat = RepeatTexture && texture != null ? lane.Length / (width * texture.width / texture.height) : 1;
                _uv[v] = new Vector2(0, 0); _uv[v + 1] = new Vector2(0, 1);
                _uv[v + 2] = new Vector2(repeat, 1); _uv[v + 3] = new Vector2(repeat, 0);
            }
            _mesh.vertices = _vertices; _mesh.uv = _uv;
            for (int m = 0; m < materials.Length; m++)
            {
                _triangles.Clear();
                for (int i = m; i < count; i += materials.Length)
                { int v = i * 4; _triangles.Add(v); _triangles.Add(v + 1); _triangles.Add(v + 2);
                    _triangles.Add(v); _triangles.Add(v + 2); _triangles.Add(v + 3); }
                _mesh.SetTriangles(_triangles, m, false);
            }
            _mesh.RecalculateBounds();
        }
        public void Show(Color color)
        {
            if (_properties == null) return;
            _properties.SetColor("_BaseColor", color); Renderer.SetPropertyBlock(_properties); Renderer.enabled = color.a > 0;
        }
        public void Hide() { if (Renderer != null) Renderer.enabled = false; }
        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
