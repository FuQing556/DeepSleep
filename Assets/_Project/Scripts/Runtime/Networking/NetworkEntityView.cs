using System.IO;
using UnityEngine;

namespace DeepSleep.Runtime.Networking
{
    /// <summary>纯显示镜像预制体。没有 Collider/Health/AI，不可能在客人端造成伤害。</summary>
    public sealed class NetworkEntityView : MonoBehaviour
    {
        public SpriteRenderer[] Layers;
        public UnityEngine.Rendering.SortingGroup Group;
        public float InterpolationSpeed { get; set; } = 30f;
        private Vector3[] _positions;
        private Quaternion[] _rotations;
        private bool _initialized;
        private void Awake() { _positions = new Vector3[Layers.Length]; _rotations = new Quaternion[Layers.Length]; }
        public void Clear()
        {
            _initialized = false;
            if (Group != null) Group.enabled = false;
            foreach (var layer in Layers) layer.enabled = false;
        }
        public void Read(BinaryReader r, NetworkSpriteCatalog catalog)
        {
            transform.position = ReadVector(r);
            Group.enabled = r.ReadBoolean(); Group.sortingLayerID = r.ReadInt32(); Group.sortingOrder = r.ReadInt32();
            byte count = r.ReadByte();
            if (count > Layers.Length) throw new IOException("Replica layer capacity exceeded");
            for (int i = 0; i < count; i++)
            {
                uint id = r.ReadUInt32(); bool shown = r.ReadBoolean();
                Vector3 position = ReadVector(r), scale = ReadVector(r); float angle = r.ReadSingle();
                var color = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                int layer = r.ReadInt32(), order = r.ReadInt32();
                bool flipX = r.ReadBoolean(), flipY = r.ReadBoolean();
                var view = Layers[i]; view.enabled = shown && catalog.TryResolve(id, out _);
                if (catalog.TryResolve(id, out var sprite)) view.sprite = sprite;
                _positions[i] = position; _rotations[i] = Quaternion.Euler(0, 0, angle);
                if (!_initialized) view.transform.SetPositionAndRotation(position, _rotations[i]);
                view.transform.localScale = scale; view.color = color;
                view.flipX = flipX; view.flipY = flipY;
                view.sortingLayerID = layer; view.sortingOrder = order;
            }
            for (int i = count; i < Layers.Length; i++) Layers[i].enabled = false;
            _initialized = true;
        }
        private void LateUpdate()
        {
            if (!_initialized) return;
            float t = 1 - Mathf.Exp(-InterpolationSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < Layers.Length; i++) if (Layers[i].enabled)
                Layers[i].transform.SetPositionAndRotation(Vector3.Lerp(Layers[i].transform.position, _positions[i], t),
                    Quaternion.Slerp(Layers[i].transform.rotation, _rotations[i], t));
        }
        public static void Write(BinaryWriter w, SpriteRenderer[] layers, NetworkSpriteCatalog catalog)
        {
            w.Write((byte)layers.Length);
            foreach (var layer in layers)
            {
                w.Write(catalog.GetId(layer.sprite)); w.Write(layer.enabled && layer.gameObject.activeInHierarchy);
                WriteVector(w, layer.transform.position); WriteVector(w, layer.transform.lossyScale);
                w.Write(layer.transform.eulerAngles.z);
                var c = layer.color; w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a);
                w.Write(layer.sortingLayerID); w.Write(layer.sortingOrder);
                w.Write(layer.flipX); w.Write(layer.flipY);
            }
        }
        private static void WriteVector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static Vector3 ReadVector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
