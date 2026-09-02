# Dokumentasi XAI Pipeline — SHAP untuk DDQN DDA
**Proyek:** *Lost In Time* — Dynamic Difficulty Adjustment (Thesis Research)
**Stack:** Python · SHAP 0.52 · PyTorch · ONNX Runtime · ML-Agents

---

## Daftar Isi

1. [Gambaran Umum](#1-gambaran-umum)
2. [Struktur File](#2-struktur-file)
3. [Alur Data End-to-End](#3-alur-data-end-to-end)
4. [Referensi Modul](#4-referensi-modul)
   - [constants.py](#41-constantspy)
   - [parse_dda_logs.py](#42-parse_dda_logspy)
   - [identify_model.py](#43-identify_modelpy)
   - [shap_net.py](#44-shap_netpy)
   - [explain_shap.py](#45-explain_shappy)
   - [report.py](#46-reportpy)
5. [Catatan Adaptasi SHAP 0.52](#5-catatan-adaptasi-shap-052)
6. [Self-Check Spec §9](#6-self-check-spec-9)
7. [Cara Menjalankan](#7-cara-menjalankan)
8. [Bug yang Telah Diperbaiki](#8-bug-yang-telah-diperbaiki)

---

## 1. Gambaran Umum

Pipeline XAI ini menjelaskan keputusan **DDQN DDA agent** menggunakan **SHAP (SHapley Additive exPlanations)**. Agent memilih tingkat kesulitan (Very Easy → Very Hard) berdasarkan 6 observasi dari kondisi player. SHAP mengukur kontribusi masing-masing observasi terhadap setiap keputusan Q-value.

```
Log Beta (JSONL)
      │
      ▼
parse_dda_logs.py   ──► states.npy / actions.npy / outcomes.npy / survival.npy
      │
      ▼
identify_model.py   ──► beta_model path → meta.json
      │
      ▼
shap_net.py         ──► ShapQNet (replica PyTorch dari .onnx)
      │
      ▼
explain_shap.py     ──► GradientExplainer (DeepSHAP)
      │                  └── KernelExplainer (fallback jika faithfulness gagal)
      ▼
Waterfall plots / Beeswarm / report.md  →  results/shap/<model>/
```

**Kenapa perlu replica PyTorch?**
`GradientExplainer` membutuhkan model PyTorch dengan `autograd` aktif. ML-Agents mengekspor model sebagai `.onnx` yang tidak kompatibel langsung — sehingga dibuat `ShapQNet` yang mereplikasi arsitektur dan mengekstrak weight dari `.onnx`.

---

## 2. Struktur File

```
tools/xai/
├── __init__.py
├── constants.py          # Konstanta tunggal: feature names, action map, outcome codes
├── parse_dda_logs.py     # Parsing log beta → numpy arrays
├── identify_model.py     # Identifikasi model beta via argmax match rate
├── shap_net.py           # ShapQNet (PyTorch replica) + ONNX extraction + faithfulness
├── explain_shap.py       # GradientExplainer wrapper, self-check, CLI utama
├── report.py             # Generate report.md: global ranking + failure patterns
│
├── states.npy            # [N, 6] float32 — observasi dari beta logs
├── actions.npy           # [N]    int64   — action yang diambil agent
├── outcomes.npy          # [N]    int64   — kode outcome (0/1/2)
├── survival.npy          # [N]    float32 — survival ratio hp_final/hp_initial
├── meta.json             # Metadata: beta_model path, match rate, decisions
├── log_paths.txt         # Daftar path log yang diparsing
│
└── tests/
    ├── test_constants.py
    ├── test_explain_shap.py    # Additivity check + KernelExplainer smoke
    ├── test_identify_model.py
    ├── test_parse_dda_logs.py
    ├── test_report.py
    ├── test_selfcheck.py
    └── test_shap_net.py
```

---

## 3. Alur Data End-to-End

### Tahap 1 — Parsing Log Beta

```python
from tools.xai.parse_dda_logs import parse_dda_logs
out = parse_dda_logs(log_paths, out_dir="tools/xai")
# out["states"]   → [N, 6] float32
# out["actions"]  → [N]    int64
# out["survival"] → [N]    float32
# out["outcomes"] → [N]    int64
```

**Cross-event pairing** (temuan empiris dari 98 log entry):
- Log real beta: `battle_start → dda_event → battle_end`
- `dda_action_taken` dalam `dda_event` = difficulty battle yang *baru selesai*, bukan berikutnya
- Sehingga: `state[i]` dipasangkan dengan `action[i+1]` (action yang dipilih dari obs_i)
- Setiap session kehilangan 1 data di ujung (tidak ada action berikutnya)

### Tahap 2 — Identifikasi Model Beta

```python
from tools.xai.identify_model import identify_model
result = identify_model(candidate_onnx_paths, states, actions)
# result["best_path"]       → path .onnx terbaik
# result["best_match_rate"] → float, misal 0.94
# result["verdict"]         → "pass" (≥90%) | "warn" (≥70%) | "flag" (<70%)
```

Dijalankan dari CLI: `python -m tools.xai.identify_model`

### Tahap 3 — Ekstraksi Weight & Replica PyTorch

```python
from tools.xai.shap_net import load_from_onnx, check_faithfulness
net = load_from_onnx("path/to/model.onnx")
chk = check_faithfulness(net, "path/to/model.onnx", states[:10])
# chk["passed"]   → True jika max_diff < 1e-4
# chk["max_diff"] → selisih terbesar antara ShapQNet dan ONNX
```

### Tahap 4 — SHAP Explanation

```python
from tools.xai.explain_shap import build_gradient_explainer, explain_all
expl = build_gradient_explainer(net, background=states)
sv, base = explain_all(states, actions, expl, expl.expected_value, out_dir="results/shap/")
# sv   → list of 5 arrays [N, 6] — SHAP values per action
# base → [5] float64 — E[Q(bg)] per action (base value)
```

### Tahap 5 — Visualisasi & Laporan

Output di `results/shap/<model_stem>/`:
| File                                   | Deskripsi                                    |
| -------------------------------------- | -------------------------------------------- |
| `waterfall_decisionD_actionA.png`      | Waterfall SHAP untuk keputusan D, action A   |
| `summary_beeswarm.png`                 | Beeswarm global atas chosen-action SHAP      |
| `report.md`                            | Global ranking + failure pattern per outcome |
| `counterfactual_hp055_to_030_diff.npy` | ΔQ saat HP Ratio diubah 0.55→0.30            |

---

## 4. Referensi Modul

### 4.1 `constants.py`

Satu-satunya sumber kebenaran untuk semua konstanta pipeline.

| Konstanta       | Nilai                                           | Keterangan                              |
| --------------- | ----------------------------------------------- | --------------------------------------- |
| `OBS_SIZE`      | `6`                                             | Jumlah fitur observasi                  |
| `ACTION_SIZE`   | `5`                                             | Jumlah action (Very Easy…Very Hard)     |
| `FEATURE_NAMES` | `[...]`                                         | Nama fitur untuk plot SHAP              |
| `OUTCOME_CODES` | `{"Subjugate":0, "Balanced":1, "Rebellious":2}` | Kode outcome                            |
| `SR_BALANCED`   | `(0.4, 0.6)`                                    | Rentang survival ratio untuk "Balanced" |

**Mapping Fitur Observasi:**
```
Index | Nama                | Deskripsi
  0   | HP Ratio            | player_hp_ratio (hp saat ini / hp awal)
  1   | Turn Count          | turn / 15
  2   | Player Level        | level / 5
  3   | Dmg Dealt Ratio     | areaTotalEnemyHP / damageDealt
  4   | QTE Accuracy        | successfulQTE / totalQTE
  5   | Resource Depletion  | tingkat konsumsi resource
```

**Mapping Outcome (survival_to_outcome):**
```
survival_ratio < 0.4  → Rebellious (2) — player banyak damage, musuh terlalu kuat
survival_ratio ∈ [0.4, 0.6] → Balanced (1) — pertempuran seimbang
survival_ratio > 0.6  → Subjugate (0) — player menang mudah, musuh terlalu lemah
```

> [!NOTE]
> Penamaan outcome mengikuti spec thesis: "Subjugate" = player *menguasai* musuh (sr tinggi), "Rebellious" = musuh *memberontak*/terlalu kuat (sr rendah).

---

### 4.2 `parse_dda_logs.py`

**Fungsi utama:** `parse_dda_logs(log_paths, out_dir=None) -> dict`

**Algoritma (dua tahap):**

**Stage A — bangun unit per session:**
```
Untuk setiap event dalam session (diurutkan berdasarkan timestamp):
  - battle_start  → simpan hp_initial
  - dda_event     → simpan obs, action_taken (pending)
  - battle_end    → hitung SR = hp_final / hp_initial, emit unit
```

**Stage B — cross-event emit:**
```
Untuk i = 0 .. len(units)-2:
  state    = units[i].obs          ← observasi yang dilihat agent
  action   = units[i+1].act_int   ← action yang dipilih dari obs_i
  survival = units[i+1].sr        ← outcome battle yang dikendalikan action itu
```

**Validasi:**
- Obs yang berada di luar `[0, 1]` di-clamp dan di-warn ke stdout.
- Unit dengan `hp_initial == 0` atau `hp_final == None` dibuang.

---

### 4.3 `identify_model.py`

**Fungsi:** `identify_model(candidate_paths, states_np, actions_np) -> dict`

Membandingkan `argmax(Q(states))` dari setiap kandidat `.onnx` terhadap `actions.npy`. Model dengan match rate tertinggi dianggap model beta yang digunakan saat pengumpulan data.

**Threshold:**
| Match Rate | Verdict | Interpretasi                                    |
| ---------- | ------- | ----------------------------------------------- |
| ≥ 90%      | `pass`  | Model beta teridentifikasi dengan aman          |
| 70–89%     | `warn`  | Mungkin benar, tapi ada noise eksplorasi tinggi |
| < 70%      | `flag`  | Model beta tidak ditemukan — **berhenti**       |

> [!IMPORTANT]
> `epsilon_final = 0.05` dalam training berarti ~5% action dipilih secara random. Match rate ≥ 90% sudah cukup aman.

---

### 4.4 `shap_net.py`

#### `ShapQNet` (nn.Module)

Replica PyTorch dari arsitektur DDQN yang digunakan dalam ML-Agents:

```
Input [B, 6]
  → Normalize: (x - running_mean) / sqrt(var + eps)    ← var = divisor²
  → Clamp [-5, 5]                                       ← dari Clip node di ONNX
  → Linear(6→128) → ReLU
  → Linear(128→128) → ReLU
  → Linear(128→5)
Output [B, 5]   ← Q-values untuk 5 action
```

**`forward(x)`** — catatan implementasi:
- `norm_var` di-clamp ke `≥ 0` sebelum `sqrt` untuk mencegah NaN akibat floating-point underflow (saat `eps=0.0`).
- `eps` di-set `0.0` karena ML-Agents menyimpan `sqrt(var+eps)` langsung sebagai divisor, sehingga `eps` sudah ter-bake.

#### `load_from_onnx(onnx_path) -> ShapQNet`

Mengekstrak weight dari `.onnx` melalui heuristic mapping:
1. Cari initializer `running_mean` dengan shape `(6,)`
2. Telusuri graph: `Sub(obs, mean) → Div(_, divisor) → Clip` untuk dapatkan divisor dan clip range
3. Recover `var = divisor²`, `eps = 0.0`
4. Map weight 2D: `(128,6)`, `(128,128)`, `(5,128)` ke layer 1/2/3
5. Match bias by name keyword (`seq_layers.0.bias`, `seq_layers.2.bias`, `extrinsic.bias`)

Raises `KeyError` jika mapping gagal → caller fall back ke `KernelExplainer`.

#### `check_faithfulness(shap_net, onnx_path, states_np, tol=1e-4) -> dict`

Verifikasi bahwa `ShapQNet` dan `.onnx` menghasilkan Q-value yang sama:
- Toleransi: `max(|Q_torch - Q_onnx|) < 1e-4`
- Returns: `{"max_diff": float, "passed": bool}`

#### `onnx_inference(onnx_path, states_np) -> ndarray [N, 5]`

Menjalankan `.onnx` dan mengembalikan raw Q-values `[N, 5]`. ML-Agents ONNX hanya mengexpose argmax output `[N, 1]`; fungsi ini menambahkan value-head Gemm output ke graph sebelum menjalankan ONNX Runtime.

---

### 4.5 `explain_shap.py`

Modul utama pipeline XAI. Entry point: `python -m tools.xai.explain_shap [args]`

#### `_GradExplainerWrapper`

Wrapper di atas `shap.GradientExplainer` untuk mengatasi breaking change SHAP 0.52:

| Fitur                      | Perilaku Lama (≤ 0.44) | Perilaku Baru (≥ 0.45) | Solusi Wrapper                   |
| -------------------------- | ---------------------- | ---------------------- | -------------------------------- |
| `shap_values` return type  | `list of [N,6]`        | `ndarray (N,6,5)`      | Normalisasi ke list-of-5 `[N,6]` |
| `expected_value` attribute | Tersedia `[5]`         | Tidak ada              | Hitung manual: `E[f(bg)]`        |

```python
expl = _GradExplainerWrapper(net, background)
sv   = expl.shap_values(X, nsamples=200, rseed=42)  # list of 5 [N,6]
base = expl.expected_value                           # [5] float64
```

**Properti `expected_value`:**
Dihitung sebagai rata-rata output model di atas background: `E_{bg}[Q(bg)]`.
Ini adalah *base value* DeepSHAP sehingga **`base[a] + Σ(shap[a][i]) ≈ Q[i, a]`** berlaku.

#### `build_gradient_explainer(net, background) -> _GradExplainerWrapper`

Primary backend — gunakan jika faithfulness check lulus.

#### `build_kernel_explainer(infer_fn, background) -> shap.KernelExplainer`

Fallback backend — jika `load_from_onnx` atau `check_faithfulness` gagal. `infer_fn(states) -> [N,5]` biasanya `lambda s: onnx_inference(onnx_path, s)`.

> [!WARNING]
> KernelExplainer jauh lebih lambat dari GradientExplainer (model-agnostic, tidak menggunakan gradient). Gunakan hanya sebagai fallback.

#### `explain_all(states, actions, expl, expected_value, out_dir, ...) -> (sv, base)`

Menghitung SHAP values dan menyimpan waterfall PNG untuk setiap (decision, action) yang diminta.

Parameter penting:
- `decision_indices`: list indeks baris yang ingin dijelaskan (`None` = semua)
- `all_actions`: `True` = waterfall untuk semua 5 action per decision
- `nsamples`: jumlah expected-gradient samples (lebih tinggi = lebih presisi)
- `rseed`: seed untuk reproducibility (`42` secara default)

#### `explain_beeswarm(states, actions, sv, base, out_dir) -> (path, chosen_sv)`

Membuat beeswarm summary plot menggunakan SHAP value dari **action yang dipilih** agent, bukan semua action.

#### `check_additivity(net, states, actions, tol=5e-2, nsamples=2000) -> bool`

Verifikasi properti lokalitas SHAP: `|base[a] + Σshap[a][i] - Q[i,a]| < tol` untuk semua (state, action).

Toleransi `5e-2` (bukan `1e-3`) karena expected-gradients sampling bersifat approximate; `nsamples=2000` sudah cukup untuk ReLU networks dengan magnitude Q kecil.

#### `run_self_check(...) -> (bool, dict)`

Menjalankan 7 pemeriksaan agregat spec §9:

| Check              | Spec | Kondisi Lulus                                                 |
| ------------------ | ---- | ------------------------------------------------------------- |
| `range`            | §9.2 | Semua obs ∈ `[0, 1]`, shape `[N, 6]`                          |
| `beta_probe`       | §9.7 | `identify_model` verdict = `pass` atau `warn`                 |
| `faithfulness`     | §9.1 | `max(                                                         | Q_torch - Q_onnx | ) < 1e-4` |
| `additivity`       | §9.3 | `max err < 5e-2` pada nsamples ≥ 2000                         |
| `determinism`      | §9.4 | Net (bukan SHAP) deterministik: `allclose(q1, q2, atol=1e-7)` |
| `outcome_coverage` | §9.5 | Ketiga kode outcome (0/1/2) muncul minimal sekali             |
| `survival_sanity`  | §9.6 | `survival_to_outcome(sr)` konsisten dengan `outcomes.npy`     |

> [!NOTE]
> `determinism` mengecek ShapQNet, bukan SHAP — karena SHAP 0.52 expected-gradient sampling memang non-deterministik (walaupun dengan `rseed` tertentu). Ini adalah perilaku yang benar.

---

### 4.6 `report.py`

**Fungsi:** `generate_report(states, actions, outcomes, sv, base, model_meta, out_dir) -> path`

Menghasilkan `report.md` berisi:
1. **Global feature ranking** — mean |SHAP| atas chosen-action, semua decisions
2. **Validitas** — penjelasan metodologi dan batasan (descriptive, bukan causal)
3. **Failure pattern: Subjugate** — ranking fitur untuk decisions beroutcome "Subjugate"
4. **Failure pattern: Rebellious** — ranking fitur untuk decisions beroutcome "Rebellious"
5. **Catatan counterfactual** — referensi ke file `*.npy` diff

---

## 5. Catatan Adaptasi SHAP 0.52

SHAP mengalami beberapa breaking change sejak versi 0.45. Berikut adaptasi yang dilakukan:

### `GradientExplainer.shap_values` — Layout Output Berubah

```python
# ≤ shap 0.44 (layout lama):
sv = explainer.shap_values(X)
# → list of 5 arrays, masing-masing [N, 6]
# → explainer.expected_value → [5]

# ≥ shap 0.45 (layout baru, termasuk 0.52):
sv = explainer.shap_values(X, nsamples=200)
# → ndarray [N, 6, 5]  (bukan list!)
# → TIDAK ada .expected_value
```

**Solusi:** `_GradExplainerWrapper` menormalisasi keduanya ke list-of-5 `[N,6]` dan menghitung `expected_value = E[Q(background)]` secara manual.

### Additivity Adalah Approximate

Expected-gradients sampling tidak menghasilkan additivity yang eksak. Ini normal:
- Toleransi: `5e-2` (untuk nsamples ≥ 2000)
- Semakin tinggi `nsamples` → semakin ketat approximation
- Untuk nsamples 8000, biasanya error < `1e-2`

---

## 6. Self-Check Spec §9

Untuk menjalankan semua 7 pemeriksaan:

```bash
# Aktifkan venv Python ML-Agents
venv\Scripts\activate

# Jalankan self-check
python -m tools.xai.explain_shap --self-check
```

Output:
```
=== self-check (spec §9) ===
  range: True
  beta_probe: True
  beta_match_rate: 0.94
  faithfulness: True
  faithfulness_max_diff: 3.45e-06
  additivity: True
  additivity_max_error: 0.031
  additivity_nsamples: 2000
  determinism: True
  outcome_coverage: True
  survival_sanity: True
=== overall: PASS ===
```

Jika `additivity` gagal pada nsamples 2000, pipeline secara otomatis retry dengan 4000 lalu 8000.

---

## 7. Cara Menjalankan

### Setup Awal

```bash
# Aktifkan Python venv
venv\Scripts\activate

# Pastikan SHAP dan dependencies terinstall
pip install -r requirements.txt
```

### Langkah 1: Parse Log Beta

```bash
python -m tools.xai.parse_dda_logs \
  --log-dir "E:\path\to\DataPost" \
  --out-dir tools/xai
```

Output: `tools/xai/states.npy`, `actions.npy`, `outcomes.npy`, `survival.npy`, `meta.json`

### Langkah 2: Identifikasi Model Beta

```bash
python -m tools.xai.identify_model \
  --states tools/xai/states.npy \
  --actions tools/xai/actions.npy \
  --models-dir Assets/Resources/DDA/Models \
  --meta tools/xai/meta.json
```

### Langkah 3: Jalankan SHAP Explanation

```bash
# Penjelasan semua decisions, semua action, 2000 samples
python -m tools.xai.explain_shap \
  --states tools/xai/states.npy \
  --actions tools/xai/actions.npy \
  --outcomes tools/xai/outcomes.npy \
  --meta tools/xai/meta.json \
  --decisions all \
  --nsamples 2000 \
  --out-dir results/shap/beta

# Hanya 10 keputusan representatif (lebih cepat)
python -m tools.xai.explain_shap --decisions representative --nsamples 2000

# Filter berdasarkan outcome
python -m tools.xai.explain_shap --filter-outcome Rebellious --nsamples 2000

# Dengan counterfactual HP Ratio 0.55 → 0.30
python -m tools.xai.explain_shap --decisions representative --counterfactual
```

### Langkah 4: Self-Check

```bash
python -m tools.xai.explain_shap --self-check
```

### Menjalankan Unit Tests

```bash
# Dari root project (bukan dotnet test — lihat AGENTS.md)
venv\Scripts\activate
python -m pytest tools/xai/tests/ -v
```

---

## 8. Bug yang Telah Diperbaiki

### Bug #1 — Dead Code: `sv = sv` (explain_shap.py)

**File:** [`explain_shap.py`](file:///e:/COLLEGE/CodeLabs/Game/SpaceJam/tools/xai/explain_shap.py#L70-L73)

**Sebelum:**
```python
if sv.ndim == 3 and sv.shape[-1] == C.ACTION_SIZE:
    sv = sv  # (N, 6, 5)   ← assignment no-op, tidak melakukan apa-apa
    return [sv[:, :, a] for a in range(C.ACTION_SIZE)]
```

**Sesudah:**
```python
# shap 0.52 layout: (N, n_features, n_outputs) == (N, 6, 5).
# Slice per action axis to produce list-of-5 [N, 6] arrays.
if sv.ndim == 3 and sv.shape[-1] == C.ACTION_SIZE:
    return [sv[:, :, a] for a in range(C.ACTION_SIZE)]
```

**Dampak:** Tidak ada dampak fungsional (kode tetap benar), namun menyesatkan karena terlihat seolah ada transformasi yang terjadi.

---

### Bug #2 — KernelExplainer Abaikan `--nsamples` (explain_shap.py)

**File:** [`explain_shap.py`](file:///e:/COLLEGE/CodeLabs/Game/SpaceJam/tools/xai/explain_shap.py#L419)

**Sebelum:**
```python
sv_raw = expl.shap_values(states)  # selalu pakai default SHAP (2n+1 samples)
```

**Sesudah:**
```python
# Pass nsamples dari CLI (--nsamples) sehingga Kernel path menghormati
# budget yang sama dengan GradientExplainer. rseed=42 untuk reproducibility.
sv_raw = expl.shap_values(states, nsamples=a.nsamples, rseed=42)
```

**Dampak:** Sebelumnya, argumen `--nsamples` dari CLI diabaikan sepenuhnya saat GradientExplainer fallback ke KernelExplainer. Ini berarti akurasi SHAP bisa berbeda jauh dari yang diharapkan user dan tidak reproducible.

---

### Bug #3 — Potensi NaN di Normalisasi `forward()` (shap_net.py)

**File:** [`shap_net.py`](file:///e:/COLLEGE/CodeLabs/Game\SpaceJam/tools/xai/shap_net.py#L69-L75)

**Sebelum:**
```python
def forward(self, x):
    x = (x - self.norm_mean) / torch.sqrt(self.norm_var + self.eps)
    # norm_var = divisor² — secara teori ≥ 0, tapi float32 round-trip
    # bisa menghasilkan nilai seperti -1e-14, sehingga sqrt(-1e-14) = NaN
```

**Sesudah:**
```python
def forward(self, x):
    # Clamp norm_var >= 0 untuk guard floating-point underflow sebelum sqrt;
    # eps=0.0 intentional (baked ke dalam divisor ONNX), namun var negatif
    # sangat kecil (misal -1e-14 dari f32 round-trip) akan menghasilkan NaN.
    safe_var = torch.clamp(self.norm_var, min=0.0)
    x = (x - self.norm_mean) / torch.sqrt(safe_var + self.eps)
```

**Dampak:** Dalam kondisi normal tidak terjadi, namun edge case saat loading model dengan precision loss bisa menyebabkan seluruh pipeline menghasilkan `NaN` dan faithfulness check gagal secara misterius. Guard ini memastikan robustness.

---

## Catatan untuk Thesis

- **Basis data:** 87 keputusan real dari closed-beta (cross-event pairing, BCM 15.31%)
- **Metode:** DeepSHAP (expected gradients) via `shap.GradientExplainer`
- **SHAP bersifat deskriptif**, bukan kausal — menjelaskan keputusan kebijakan yang ter-deploy, bukan mekanisme training
- **Counterfactual di luar rentang observasi** adalah ekstrapolasi (di-flag otomatis)
- **Additivity tolerance 5e-2** adalah konsekuensi dari expected-gradients sampling (bukan kesalahan implementasi)
