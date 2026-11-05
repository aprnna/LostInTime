# ML-Agents DDQN Plugin (`mlagents-plugin-ddqn`)

Custom trainer plugin untuk **Unity ML-Agents Toolkit** yang menambahkan dukungan algoritma **Double Deep Q-Network (DDQN)** untuk discrete action space.

Plugin ini didaftarkan otomatis ke CLI `mlagents-learn` melalui Python entry points (`trainer_type: ddqn`).

---

## 🚀 Fitur Utama

- **Double Q-Learning**: Mengurangi overestimation bias nilai Q. Jaringan *online* memilih aksi terbaik, jaringan *target* mengevaluasi nilai Q aksi tersebut:
  $$Y_t^{DoubleQ} = R_{t+1} + \gamma Q(S_{t+1}, \arg\max_a Q(S_{t+1}, a; \theta_t); \theta_t^-)$$
- **Aktivasi ReLU**: Menggantikan aktivasi default *Swish* ML-Agents (`ReLULinearEncoder`, `ReLUNetworkBody`, `ReLUValueNetwork`) untuk inferensi lebih stabil dan kompatibel dengan Unity Sentis / Barracuda ONNX runtime.
- **Soft Target Update**: Update bobot target network secara bertahap menggunakan parameter $\tau$ (`tau = 0.005`).
- **Seamless ML-Agents Integration**: Memperluas `OffPolicyTrainer` dan `TorchPolicy` bawaan ML-Agents.

---

## 📦 Instalasi

### Prasyarat
- Python `>= 3.8` (disarankan `3.9` - `3.10`)
- `mlagents >= 1.1.0`
- `torch >= 2.2.0`
- `numpy >= 1.23.0`

### Mode Editable (Development)
Jalankan di direktori ini:

```bash
pip install -e .
```

Verifikasi instalasi:
```bash
python -c "import mlagents_plugin_ddqn; print(mlagents_plugin_ddqn.__version__)"
```

---

## ⚙️ Konfigurasi (`ddqn.yaml`)

Gunakan `trainer_type: ddqn` pada file konfigurasi training:

```yaml
behaviors:
  DDAAgent:
    trainer_type: ddqn
    hyperparameters:
      learning_rate: 0.0001
      learning_rate_schedule: constant
      batch_size: 128
      buffer_size: 50000
      tau: 0.005
      steps_per_update: 1.0
      save_replay_buffer: false
      exploration_schedule: linear
      exploration_initial_eps: 1.0
      exploration_final_eps: 0.01
      exploration_decay_steps: 350000
    network_settings:
      normalize: true
      hidden_units: 64
      num_layers: 2
      vis_encode_type: simple
    reward_signals:
      extrinsic:
        gamma: 0.90
        strength: 1.0
    keep_checkpoints: 5
    max_steps: 500000
    time_horizon: 12
    summary_freq: 5000
```

---

## 🛠️ Hyperparameter Plugin

| Parameter | Default | Keterangan |
|-----------|---------|------------|
| `trainer_type` | `ddqn` | Tipe trainer (wajib `ddqn`) |
| `tau` | `0.005` | Koefisien soft update target network ($\theta^- \leftarrow \tau \theta + (1-\tau) \theta^-$) |
| `exploration_initial_eps` | `1.0` | Nilai epsilon awal $\epsilon$-greedy |
| `exploration_final_eps` | `0.01` | Nilai epsilon minimum setelah decay |
| `exploration_decay_steps`| `20000` | Jumlah step untuk penurunan epsilon |
| `exploration_schedule` | `linear` | Metode decay (`linear` atau `constant`) |
| `target_update_interval`| `10000` | Interval hard update (jika tau tidak digunakan) |
| `steps_per_update` | `1` | Rasio step environment per optimasi gradien |
| `save_replay_buffer` | `false` | Simpan buffer replay saat checkpoint |

---

## 🎮 Cara Menjalankan Training

Dari direktori root game project:

```bash
# Training dengan Unity Editor (Play mode)
mlagents-learn config/ddqn.yaml --run-id=ddqn_run_01 --train

# Training headless parallel environment (executable build)
mlagents-learn config/ddqn.yaml --run-id=ddqn_run_fast --train --num-envs=4 --no-graphics
```

---

## 📁 Struktur Direktori

```
ml_agents_plugin/
├── mlagents_plugin_ddqn/
│   ├── __init__.py          # Entry point export & metadata versi
│   ├── ddqn_trainer.py      # DDQNTrainer (subclass OffPolicyTrainer)
│   ├── ddqn_optimizer.py    # DDQNOptimizer & DDQNSettings
│   └── networks.py          # ReLU encoder, body, & QNetworkDDQN
├── dqn/                     # Template konfigurasi dqn dasar
├── setup.py                 # Package setup & entry_points registration
└── README.md
```
