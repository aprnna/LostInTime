using TMPro;
using UnityEngine;

namespace Player
{
    public class DamagePopup : MonoBehaviour
    {
        [Header("Popup Settings")]
        [SerializeField] private float _speed = 8f;
        // _upwardFactor tidak lagi krusial jika gerak murni ke atas, 
        // tapi bisa disimpan jika ingin variasi nanti.
        [SerializeField] private float _lifeTime = 1f; 
        [SerializeField] private float _fadeSpeed = 5f;
        [SerializeField] private float _growRate = 1f;

        [Header("Colors")]
        [SerializeField] private string _normalHex = "#FFB600";
        [SerializeField] private string _criticalHex = "#FF0000";

        private static int _sortingOrder;
        private TMP_Text _textMeshPro;
        private Color _textColor;
        private float _timer;
        private Vector3 _velocity;

        public static DamagePopup Create(Transform prefab, Vector3 position, float damage, bool isCritical, Transform parent = null)
        {
            var inst = Instantiate(prefab, position, Quaternion.identity, parent );
            var popup = inst.GetComponent<DamagePopup>();
            
            // 2. Hapus passing argument attackDir
            popup.Initialize(damage, isCritical); 
            
            return popup;
        }

        private void Awake()
        {
            _textMeshPro = GetComponent<TMP_Text>();
        }

        // 3. Hapus parameter attackDir di sini
        private void Initialize(float damage, bool isCritical)
        {
            // Set text & sorting
            _textMeshPro.SetText(damage.ToString());

            // Font size & color
            _textMeshPro.fontSize = isCritical ? 3f : 2f;
            ColorUtility.TryParseHtmlString(
                isCritical ? _criticalHex : _normalHex,
                out _textColor
            );
            _textMeshPro.color = _textColor;

            // Motion & lifetime
            // 4. GANTI logika velocity. 
            // Karena tidak ada attackDir, kita buat dia naik ke atas saja.
            // Opsi A (Lurus ke atas):
            // _velocity = Vector3.up * _speed;

            // Opsi B (Jika ingin sedikit menyebar acak kiri/kanan, uncomment baris bawah):
            _velocity = new Vector3(Random.Range(-0.5f, 0.5f), 1f, 0f) * _speed;

            _timer = _lifeTime;
        }

        private void Update()
        {
            // Movement (with damping)
            transform.position += _velocity * Time.deltaTime;
            _velocity *= 1f - (_speed * Time.deltaTime / _lifeTime);

            // Scale in–out: membesar lalu mengecil
            float scaleStep = _growRate * Time.deltaTime;
            if (_timer > _lifeTime * 0.5f)
                transform.localScale += Vector3.one * scaleStep;  // membesar
            else
                transform.localScale -= Vector3.one * scaleStep;  // mengecil
            
            // Lifetime & fade
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _textColor.a -= _fadeSpeed * Time.deltaTime;
                _textMeshPro.color = _textColor;
                if (_textColor.a <= 0f)
                    Destroy(gameObject);
            }
        }   
    }
}