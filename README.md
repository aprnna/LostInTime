# Lost In Time - Dynamic Difficulty Adjustment (DDA) with DDQN

Game 2D turn-based battle berbasis Unity dengan implementasi Reinforcement Learning (RL) menggunakan algoritma **Double Deep Q-Network (DDQN)** untuk Dynamic Difficulty Adjustment (DDA). Proyek dikembangkan untuk penelitian/skripsi komparasi DDA berbasis RL adaptif terhadap performa dan retensi pemain.

---

## 📌 Ringkasan Proyek

- **Engine Game**: Unity 2022+ (Universal Render Pipeline / URP)
- **Framework RL**: Unity ML-Agents Toolkit + Custom DDQN Trainer Plugin (`mlagents-plugin-ddqn`)
- **Backend Analytics**: PlayFab Client API (pencatatan sesi, battle log, telemetri DDA)
- **Asinkron**: UniTask
- **Model Format**: PyTorch (`.pt`) untuk checkpoint training, ONNX (`.onnx`) untuk inferensi runtime di Unity via Unity Sentis / Barracuda

---

## 🧠 Arsitektur RL DDA (DDQN)

Sistem DDA memantau metrik performa pemain tiap battle area dan secara dinamis menyesuaikan tingkat kesulitan untuk mempertahankan *flow state* (sweet spot HP ~50%).

### 1. Struktur Episode & Credit Assignment
- **1 Episode** = 1 *run* utuh (12 area peta bercabang: Battle, Rest, Shop, Boss).
- `RequestDecision()` dipanggil tiap akhir battle area.
- Reward area ke-*N* diatribusikan ke aksi yang dipilih setelah area *N-1* (Area 0 skip reward / baseline).

### 2. State / Observasi (6 Fitur Vektor Kontinu, Normalisasi [0, 1])
1. `HP Ratio`: Sisa HP pemain ($HP_{current} / HP_{max}$).
2. `Turn Count`: Jumlah giliran battle (dinormalisasi: $\min(turn / 15, 1.0)$).
3. `Player Level`: Level progres pemain (dinormalisasi: $\min(level / 5, 1.0)$).
4. `Damage Dealt Ratio`: Efisiensi damage terhadap musuh ($EnemyHP_{total} / DamageDealt$).
5. `QTE Accuracy`: Rasio keberhasilan minigame QTE ($QTE_{sukses} / QTE_{total}$).
6. `Resource Depletion`: Tingkat konsumsi resource/item pemain.

### 3. Action Space (5 Aksi Diskrit)
Memilih tingkat kesulitan langsung (Index 0 - 4) dengan step interval 0.15:
- `0`: Very Easy (Level 0 — Multiplier: `0.70x`)
- `1`: Easy (Level 1 — Multiplier: `0.85x`)
- `2`: Normal (Level 2 — Multiplier: `1.00x` / Baseline)
- `3`: Hard (Level 3 — Multiplier: `1.15x`)
- `4`: Very Hard (Level 4 — Multiplier: `1.30x`)

*Catatan: Multiplier diatur via ScriptableObject `DefaultDifficultySettings.asset` dan diaplikasikan ke HP dan Damage musuh.*

### 4. Reward Function
- **Sweet Spot Target (Gaussian / RBF)**:
  $$\text{Reward} = \exp\left(-8 \cdot (r - 0.5)^2\right)$$
  dimana $r = \frac{HP_{end}}{HP_{start}} \in [0, 1]$. Nilai puncak $+1.0$ tercapai saat $r = 0.5$ (50% sisa HP).
- **Kekalahan Area**: $-0.5$ jika pemain kalah pada area pertempuran.
- **Run Bonus (End of Episode)**: $+0.5$ jika pemain berhasil menamatkan run (Area 12 selesai), atau $-0.3$ jika run gagal.
- **Death Penalty**: $-0.5$ jika pemain mati di tengah run sebelum selesai.

---

## 🛠️ Instalasi & Prasyarat

### 1. Prasyarat Lingkungan
- Python 3.9 - 3.10
- Unity Editor 2022.3 LTS+ (dengan modul URP)
- PyTorch (kompatibel CUDA jika menggunakan GPU)

### 2. Setup Python & DDQN Plugin
Clone repositori dan pasang dependensi virtual environment:

```bash
# Buat dan aktifkan virtual environment
python -m venv venv
# Windows
venv\Scripts\activate
# Linux/macOS
source venv/bin/activate

# Install dependensi dasar
pip install -r requirements.txt

# Install plugin custom DDQN
cd ml_agents_plugin
pip install -e .
cd ..
```

---

## 🚀 Pelatihan Model (Training)

Pelatihan menggunakan environment simulasi Unity (`TrainingBattleSimulator.cs`) dengan mode *instant battle* untuk mempercepat *step rate*.

### Menjalankan Training
1. Buka Unity Editor, muat scene `Assets/Scenes/DDA ML Agents.unity`.
2. Jalankan perintah pelatihan di terminal:
   ```bash
   # Single environment (interaktif via Unity Editor Play button)
   mlagents-learn config/ddqn.yaml --run-id=ddqn_lostintime_v1 --train
   ```
3. Tekan tombol **Play** di Unity Editor saat terminal menampilkan *"Listening on port..."*.

### Pelatihan Paralel (Headless / Cepat)
Jika build executable environment tersedia:
```bash
mlagents-learn config/ddqn.yaml --run-id=ddqn_lostintime_fast --train --num-envs=4 --no-graphics
```

Hasil training (checkpoint model `.onnx` dan tensorboard log) tersimpan di direktori:
```
results/<run-id>/
```

Log evaluasi event training Unity disimpan lokal di:
```
%USERPROFILE%\AppData\LocalLow\SaltStudio\Lost In Time\DDA_Training\
```

---

## 🎮 Integrasi Inferensi Runtime di Unity

1. Pindahkan model hasil latih (`.onnx`) dari `results/<run-id>/` ke `Assets/Resources/DDA/` atau assign ke field `NNModel` pada komponen `DDAAgent`.
2. Pada `DDAAgent`, ubah komponen `Behavior Parameters`:
   - **Behavior Type**: `Inference Only`
   - **Model**: Assign file `.onnx`
   - **Inference Device**: `CPU` (atau `GPU` jika didukung Sentis)
3. Saat mode gameplay aktif, `DDAAgent` akan membaca observasi *real-time* pemain setiap pergantian area dan mengatur `DifficultySettings` otomatis.

---

## 📂 Struktur Repositori

```
SpaceJam/
├── Assets/
│   ├── Scenes/               # Unity Scenes (Battle, MainMenu, DDA ML Agents)
│   ├── Scripts/
│   │   ├── DDA/              # Inti DDA (DDAAgent, Simulator, Logger, DifficultySettings)
│   │   ├── Manager/          # GameManager, BattleSystem, MapSystem, AudioManager
│   │   │   └── State/        # FSM Battle System (PlayerTurn, EnemyTurn, Result, dll.)
│   │   └── State/            # Generic Finite State Machine
│   └── Resources/            # ScriptableObjects (Player Actions, Enemies, GameData)
├── config/
│   └── ddqn.yaml             # Konfigurasi hyperparameter trainer DDQN
├── ml_agents_plugin/         # Package custom trainer DDQN untuk ML-Agents
│   └── mlagents_plugin_ddqn/ # DDQN implementation (Double Q-learning, replay buffer)
├── tools/
│   └── xai/                  # Analisis explainable AI (SHAP / Counterfactual)
└── requirements.txt          # Dependensi Python
```

---

## 📊 Hyperparameter DDQN (`config/ddqn.yaml`)

| Parameter | Nilai | Keterangan |
|-----------|-------|------------|
| `trainer_type` | `ddqn` | Custom Double DQN trainer plugin |
| `learning_rate`| `0.0001` | Adam optimizer learning rate (constant) |
| `batch_size` | `128` | Ukuran batch sampling replay buffer |
| `buffer_size` | `10000` | Kapasitas pengalaman replay buffer |
| `gamma` | `0.999` | Discount factor episode |
| `exploration_initial_eps` | `1.0` | Epsilon eksplorasi awal |
| `exploration_final_eps` | `0.3` | Epsilon eksplorasi akhir |
| `exploration_decay_steps` | `300000` | Step penurunan epsilon eksplorasi |
| `tau` | `0.005` | Soft update rate target network |
| `steps_per_update` | `4` | Frekuensi update jaringan per environment step |
| `hidden_units` / `num_layers` | `128` / `2` | Arsitektur MLP feedforward |
| `max_steps` | `1000000` | Total step pelatihan |

---

## 📝 Lisensi & Catatan Riset
Proyek ini dikembangkan untuk keperluan akademik dan penelitian skripsi. Aset visual dan audio merupakan hak milik *SaltStudio / creator* terkait.
