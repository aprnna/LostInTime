# Dokumentasi Lengkap Pipeline Explainable AI (XAI) dengan SHAP
**Proyek Skripsi / Penelitian:** Dynamic Difficulty Adjustment (DDA) berbasis Deep Double Q-Network (DDQN) pada Game *Lost In Time*  
**Lokasi Modul:** `tools/xai/`  
**Metode XAI:** SHAP (*SHapley Additive exPlanations*) — Expected Gradients (`shap.GradientExplainer`)

---

## 1. Latar Belakang & Mengapa XAI Diperlukan

Dalam game *Lost In Time*, agen DDQN bertugas mengatur tingkat kesulitan pertarungan (*Very Easy*, *Easy*, *Normal*, *Hard*, *Very Hard*) secara dinamis agar pemain tetap berada pada kondisi *Flow* (tidak frustrasi dan tidak bosan).

Meskipun DDQN mampu memilih aksi penyesuaian kesulitan, jaringan saraf tiruan (*neural network*) bersifat **Black-Box**. Penguji sidang atau pembaca skripsi akan bertanya:
> *"Mengapa agen memilih aksi Very Hard pada ronde ke-5? Fitur kondisi pemain apa yang paling memicu keputusan tersebut?"*

**XAI dengan SHAP** menjawab pertanyaan tersebut dengan menghitung kontribusi matematis (*attribution score*) dari setiap fitur observasi terhadap *Q-value* yang diprediksi oleh agen.

---

## 2. Landasan Teori: Bagaimana SHAP Bekerja

### 2.1 Konsep Shapley Values
SHAP berakar dari *Cooperative Game Theory* (Lloyd Shapley, Nobel Ekonomi). Dalam konteks DDA:
* **Pemain (Game Players):** 6 Fitur Observasi kondisi pemain (HP Ratio, Turn Count, Player Level, Dmg Dealt Ratio, QTE Accuracy, Resource Depletion).
* **Hasil (Payout):** Nilai estimasi kualitas aksi / Q(s, a).
* **Nilai SHAP ($\phi_i$):** Kontribusi marginal fitur ke-i terhadap perubahan Q-value dari rata-rata ekspektasi basis ($\phi_0$ / Base Value).

### 2.2 Sifat Additivity (Local Accuracy)
Salah satu keunggulan utama SHAP adalah sifat **Additivity**. Jumlah seluruh nilai SHAP ditambah *Base Value* sama persis dengan Q-value yang dikeluarkan model:

$$Q(s, a) = \text{Base Value}(a) + \sum_{i=1}^{M} \phi_i(s, a)$$

Di mana:
* $Q(s, a)$: Output nilai Q dari model untuk observasi state $s$ dan aksi $a$.
* $\text{Base Value}(a)$: Rata-rata output model pada seluruh background dataset untuk aksi $a$ ($E_{bg}[Q(bg, a)]$).
* $\phi_i(s, a)$: Kontribusi fitur ke-$i$. Jika $\phi_i > 0$, fitur tersebut menaikkan preferensi agen terhadap aksi $a$; jika $\phi_i < 0$, fitur tersebut menurunkannya.

### 2.3 Mengapa Menggunakan Expected Gradients (`GradientExplainer`)?
Model DDQN game diekspor oleh Unity ML-Agents dalam format **ONNX**. 
* `shap.TreeExplainer` tidak bisa digunakan karena model berbasis Neural Network (bukan pohon keputusan seperti XGBoost).
* `shap.DeepExplainer` (DeepLIFT) membutuhkan akses hook layer native PyTorch yang sangat kaku dan sering bermasalah pada model konversi.
* **`shap.GradientExplainer` (Expected Gradients)** mengintegrasikan gradien output model terhadap input di sepanjang jalur antara baseline background $x'$ dan sampel $x$:

$$\phi_i(x) = (x_i - x'_i) \times \int_0^1 \frac{\partial f(x' + \alpha (x - x'))}{\partial x_i} d\alpha$$

Metode ini sangat fleksibel, cepat, dan terbukti stabil untuk arsitektur MLP (*Multi-Layer Perceptron*) DDA.

---

## 3. Alur Kerja (Pipeline) End-to-End di `tools/`

Pipeline XAI terdiri dari 5 modul utama yang bekerja secara berurutan:

```
[ Log Pertarungan .jsonl ]
           │
           ▼ (1. parse_dda_logs.py)
[ states.npy, actions.npy, outcomes.npy, survival.npy, meta.json ]
           │
           ▼ (2. identify_model.py)
[ Identifikasi Model ONNX Beta Terbaik ]
           │
           ▼ (3. shap_net.py)
[ Ekstraksi Bobot ONNX ──► ShapQNet (Replika PyTorch) ]
           │
           ▼ (4. explain_shap.py)
[ Komputasi SHAP GradientExplainer & Self-Check ]
           │
           ▼ (5. report.py & Visualisasi Matplotlib)
[ Waterfall Plots, Beeswarm Plot, report.md ]
```

---

### Tahap 1: Parsing Log Pertarungan (`xai/parse_dda_logs.py`)
Mengonversi log sesi pemain format `.jsonl` menjadi array matriks NumPy.

* **Tantangan Empiris (Cross-Event Pairing):**
  Dalam log game, event `dda_event` dicatat di akhir pertarungan yang baru saja selesai. Artinya:
  * Observasi $obs_k$ adalah kondisi pemain di akhir ronde $k$.
  * Aksi $act_{k+1}$ pada event berikutnya adalah aksi yang dipilih oleh agen berdasarkan $obs_k$ untuk mengatur ronde $k+1$.
  * Modul ini menerapkan **Cross-Event Pairing** ($obs_k \leftrightarrow act_{k+1}$) agar asosiasi keputusan agen 100% akurat.
* **Fitur Observasi (6 Fitur):**
  1. `HP Ratio` ($x_0$): Sisa rasio darah pemain $[0, 1]$.
  2. `Turn Count` ($x_1$): Jumlah giliran dibagi 15.
  3. `Player Level` ($x_2$): Level pemain dibagi 5.
  4. `Dmg Dealt Ratio` ($x_3$): Total damage musuh dibagi damage yang dihasilkan.
  5. `QTE Accuracy` ($x_4$): Rasio keberhasilan Quick Time Event $[0, 1]$.
  6. `Resource Depletion` ($x_5$): Tingkat konsumsi item/resource pemain.

---

### Tahap 2: Identifikasi Model Beta (`xai/identify_model.py`)
Jika di folder model Unity terdapat beberapa file `.onnx` kandidat, modul ini melakukan simulasi inferensi argmax Q-value terhadap 87 state beta untuk mendeteksi model mana yang memiliki tingkat kesesuaian (*match rate*) tertinggi dengan aksi yang tercatat di log.

---

### Tahap 3: Ekstraksi Weight & Replika PyTorch (`xai/shap_net.py`)
Library SHAP membutuhkan model PyTorch aktif untuk menghitung turunan gradien (*autograd*), sedangkan Unity menjalankan `.onnx`.
* Modul ini membaca initializer ONNX: `running_mean`, `divisor`, bobot matriks `Linear(6->128->128->5)`, dan fungsi aktivasi `ReLU`.
* Membangun model PyTorch native: **`ShapQNet`**.
* **Faithfulness Check:** Memverifikasi bahwa output PyTorch dan ONNXRuntime identik dengan toleransi error ketat ($< 10^{-4}$). Pada pengujian aktual tercapai error $\approx 1.19 \times 10^{-7}$ (hampir 0).

---

### Tahap 4: Komputasi SHAP (`xai/explain_shap.py`)
* Menggunakan `_GradExplainerWrapper` yang menjembatani API modern SHAP 0.52.
* Menghitung nilai atribusi SHAP per-keputusan untuk semua 5 aksi (Very Easy s.d. Very Hard).
* **Self-Check (§9 Spec Validasi):** Menguji 7 kriteria keabsahan:
  1. *Range:* Semua nilai observasi berada pada interval valid $[0, 1]$.
  2. *Beta Probe:* Match rate model di atas ambang batas.
  3. *Faithfulness:* Kesesuaian ONNX vs PyTorch $< 10^{-4}$.
  4. *Additivity:* Error selisih $|Q - (\text{Base} + \sum \phi)| < 0.05$.
  5. *Determinism:* Inferensi model konsisten.
  6. *Outcome Coverage:* Semua skenario hasil (*Subjugate, Balanced, Rebellious*) terwakili.
  7. *Survival Sanity:* Konsistensi rasio bertahan hidup dengan label hasil.

---

### Tahap 5: Visualisasi & Analisis Kegagalan (`xai/report.py`)
Menghasilkan grafik visual dan laporan analisis dalam format Markdown.

---

## 4. Cara Membaca Hasil Visualisasi untuk Skripsi

### 4.1 Membaca Grafik Waterfall (`waterfall_decisionX_actionY.png`)
Grafik ini menjelaskan **satu keputusan spesifik** (Local Explanation).

```
f(x) = 1.45 (Nilai Q Akhir)
       ▲
       │  [+] Player Level = 0.8  (Merah +0.35) ──► Menaikkan Q-value
       │  [+] QTE Accuracy = 1.0  (Merah +0.20) ──► Menaikkan Q-value
       │  [-] Turn Count = 0.47   (Biru  -0.10) ──► Menurunkan Q-value
       │
E[f(x)] = 1.00 (Base Value / Rata-rata Ekspektasi)
```
* **Sumbu Y:** Fitur dan nilai aktualnya pada saat keputusan diambil.
* **Panah Merah / Nilai Positif:** Fitur mendorong agen untuk **memilih** aksi ini (menaikkan nilai Q).
* **Panah Biru / Nilai Negatif:** Fitur menahan agen untuk **tidak memilih** aksi ini (menurunkan nilai Q).
* **E[f(X)] (Bawah):** Nilai dasar sebelum melihat observasi spesifik.
* **f(x) (Atas):** Nilai akhir $Q(s, a)$.

---

### 4.2 Membaca Grafik Beeswarm (`summary_beeswarm.png`)
Grafik ini merangkum **seluruh keputusan pemain secara global** (Global Explanation).

* Setiap baris mewakili 1 fitur observasi, diurutkan dari yang **paling berpengaruh** di baris paling atas hingga yang paling tidak berpengaruh di baris paling bawah.
* Setiap titik mewakili 1 keputusan pertarungan.
* **Warna Titik:**
  * **Merah:** Nilai fitur tinggi (misal: HP pemain penuh, Level tinggi).
  * **Biru:** Nilai fitur rendah (misal: HP sekarat, Level rendah).
* **Posisi Horizontal (SHAP Value):**
  * Di sebelah **kanan garis 0** $\rightarrow$ Meningkatkan Q-value.
  * Di sebelah **kiri garis 0** $\rightarrow$ Menurunkan Q-value.

**Contoh Interpretasi Skripsi:**
> *"Pada grafik Beeswarm terlihat bahwa titik merah pada fitur Player Level terkumpul di sisi kanan garis 0. Hal ini membuktikan bahwa semakin tinggi level pemain, model DDQN secara konsisten memberikan nilai Q yang tinggi pada aksi penambahan kesulitan (Hard/Very Hard)."*

---

### 4.3 Membaca Laporan Analisis Pola Kegagalan (`report.md`)
Laporan mengelompokkan keputusan berdasarkan hasil pertarungan:
1. **Subjugate (Pemain Menang Mudah / Survival Ratio > 0.6):**
   Menganalisis fitur apa yang membuat model terlambat menaikkan kesulitan saat pemain terlalu dominan.
2. **Rebellious (Pemain Kalah Telak / Survival Ratio < 0.4):**
   Menganalisis fitur apa yang menyebabkan model menaikkan kesulitan terlalu ekstrem sehingga pemain kewalahan.
3. **Balanced (Pertarungan Seimbang / Survival Ratio 0.4 – 0.6):**
   Kondisi ideal yang ditargetkan sistem DDA.

---

## 5. Panduan Praktis Perintah CLI (Cheatsheet)

Semua perintah dijalankan dari dalam direktori `tools/`:

```powershell
# Pindah ke direktori tools
cd e:\COLLEGE\CodeLabs\Game\SpaceJam\tools

# 1. Menjalankan Self-Check Validasi Formal (Spec §9)
uv run python -m xai.explain_shap --self-check

# 2. Menghasilkan Interpretasi SHAP Lengkap untuk Model Final
uv run python -m xai.explain_shap --model ddqn_dda_final.onnx --decisions representative --nsamples 2000

# 3. Menganalisis Seluruh Keputusan (87 Keputusan)
uv run python -m xai.explain_shap --model ddqn_dda_final.onnx --decisions all --nsamples 2000

# 4. Menganalisis Skenario Counterfactual (Eksperimen 'What-If' jika HP diubah)
uv run python -m xai.explain_shap --model ddqn_dda_final.onnx --decisions representative --counterfactual

# 5. Filter Khusus Skenario Kegagalan Tertentu
uv run python -m xai.explain_shap --model ddqn_dda_final.onnx --filter-outcome Rebellious

# 6. Menjalankan Pengujian Unit Test Otomatis (15 Test Cases)
uv run pytest xai/tests/ -v
```

---

## 6. Struktur File Output (`tools/results/shap/<model>/`)

Setiap kali eksekusi SHAP selesai, output disimpan secara terstruktur:
* `waterfall_decision<ID>_action<A>.png`: Grafik waterfall untuk keputusan ke-`ID` dan aksi `A` (0=Very Easy ... 4=Very Hard).
* `summary_beeswarm.png`: Grafik beeswarm global.
* `counterfactual_hp055_to_030_diff.npy`: Data selisih nilai Q pada simulasi counterfactual.
* `report.md`: Laporan ranking kepentingan fitur dan ringkasan pola kegagalan.
