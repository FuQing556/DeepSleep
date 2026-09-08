using UnityEngine;

namespace DeepSleep.Runtime.Combat.Beams.Presentation
{
    /// <summary>
    /// 以动态四边形网格显示束线，并沿局部 X 轴重复束体纹理。
    /// 网格起点与逻辑开火原点完全一致，不使用非等比整图拉伸。
    /// </summary>
    public sealed class BeamTiledMeshView2D : MonoBehaviour
    {
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId =
            Shader.PropertyToID("_BaseMap");

        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;

        [Header("排序来源")]
        [SerializeField] private Renderer _sortingReferenceRenderer;
        [SerializeField] private int _orderOffset;

        private readonly Vector3[] _vertices = new Vector3[4];
        private readonly Vector2[] _uv = new Vector2[4];
        private readonly int[] _triangles = { 0, 2, 1, 2, 3, 1 };
        private MaterialPropertyBlock _propertyBlock;
        private Mesh _mesh;
        private bool _isInitialized;

        private void Awake()
        {
            if (!TryValidateConfiguration(out string reason))
            {
                Debug.LogError(
                    $"[{nameof(BeamTiledMeshView2D)}] " +
                    $"束体网格装配无效：{reason}",
                    this);
                enabled = false;
                return;
            }

            _mesh = new Mesh
            {
                name = $"{name}_RuntimeBeamMesh",
            };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uv;
            _mesh.triangles = _triangles;
            _meshFilter.sharedMesh = _mesh;
            _propertyBlock = new MaterialPropertyBlock();
            _meshRenderer.sortingLayerID =
                _sortingReferenceRenderer.sortingLayerID;
            _meshRenderer.sortingOrder =
                _sortingReferenceRenderer.sortingOrder + _orderOffset;
            _meshRenderer.enabled = false;
            _isInitialized = true;
        }

        public void Show(
            Vector2 origin,
            Vector2 direction,
            float worldLength,
            float worldWidth,
            float textureRepeatWorldLength,
            float textureOffset,
            Color color)
        {
            if (!_isInitialized ||
                direction.sqrMagnitude <= Mathf.Epsilon ||
                worldLength <= 0f ||
                worldWidth <= 0f ||
                textureRepeatWorldLength <= 0f)
            {
                Hide();
                return;
            }

            direction.Normalize();
            float halfWidth = worldWidth * 0.5f;
            float textureStart = Mathf.Repeat(textureOffset, 1f);
            float textureEnd =
                textureStart + worldLength / textureRepeatWorldLength;

            _vertices[0] = new Vector3(0f, -halfWidth, 0f);
            _vertices[1] = new Vector3(worldLength, -halfWidth, 0f);
            _vertices[2] = new Vector3(0f, halfWidth, 0f);
            _vertices[3] = new Vector3(worldLength, halfWidth, 0f);
            _uv[0] = new Vector2(textureStart, 0f);
            _uv[1] = new Vector2(textureEnd, 0f);
            _uv[2] = new Vector2(textureStart, 1f);
            _uv[3] = new Vector2(textureEnd, 1f);

            _mesh.vertices = _vertices;
            _mesh.uv = _uv;
            _mesh.RecalculateBounds();

            Transform viewTransform = transform;
            float angleDegrees =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float inverseParentScale = GetInverseUniformParentScale();
            viewTransform.SetPositionAndRotation(
                origin,
                Quaternion.Euler(0f, 0f, angleDegrees));
            viewTransform.localScale = new Vector3(
                inverseParentScale,
                inverseParentScale,
                1f);

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, color);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
            _meshRenderer.enabled = true;
        }

        public void Hide()
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
        }

        public bool TryValidateConfiguration(out string reason)
        {
            if (_meshFilter == null)
            {
                reason = "未配置 Mesh Filter。";
                return false;
            }

            if (_meshRenderer == null)
            {
                reason = "未配置 Mesh Renderer。";
                return false;
            }

            if (_sortingReferenceRenderer == null)
            {
                reason = "未配置排序参考渲染器。";
                return false;
            }

            if (_meshRenderer.sharedMaterial == null)
            {
                reason = "Mesh Renderer 没有束体材质。";
                return false;
            }

            if (!_meshRenderer.sharedMaterial.HasProperty(BaseColorId))
            {
                reason = "束体材质不支持 _BaseColor 属性。";
                return false;
            }

            if (!_meshRenderer.sharedMaterial.HasProperty(BaseMapId) ||
                _meshRenderer.sharedMaterial.GetTexture(BaseMapId) == null)
            {
                reason = "束体材质没有配置 _BaseMap 纹理。";
                return false;
            }

            Transform parent = transform.parent;

            if (parent != null &&
                !Mathf.Approximately(
                    Mathf.Abs(parent.lossyScale.x),
                    Mathf.Abs(parent.lossyScale.y)))
            {
                reason = "父节点必须使用等比 XY 缩放。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private float GetInverseUniformParentScale()
        {
            Transform parent = transform.parent;

            if (parent == null)
            {
                return 1f;
            }

            return 1f / Mathf.Max(
                Mathf.Abs(parent.lossyScale.x),
                0.0001f);
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            Destroy(_mesh);
            _mesh = null;
        }
    }
}
