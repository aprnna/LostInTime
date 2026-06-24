# Experiment Records

Record setiap experiment DDA/DDQN. Satu file `.md` per experiment, pakai [`TEMPLATE.md`](./TEMPLATE.md).

## Aturan (WAJIB)

**Sebelum run training, file experiment WAJIB dibuat dan diisi.**

Alasan: states, action, reward, dan hyperparameter saat run itu terjadi tidak bisa direkonstruksi setelahnya. `configuration.yaml` di `results/<run-id>/` cuma nyimpen hyperparameter — reward function, observation set, dan episode logic ada di source code yang terus berubah. Kalau tidak dicatat sebelum run, run itu pada dasarnya hilang untuk skripsi.

Urutan sebelum run:

1. Copy `TEMPLATE.md` → `EXPERIMENT_<run-id>.md`.
2. Isi bagian **States / Actions / Reward / Hyperparameter / Environment** dengan nilai yang *akan* dipakai di run ini. Cek `config/ddqn.yaml` untuk hyperparameter, dan source code (`DDAAgent.cs`, `DifficultySettings.cs`, `TrainingBattleSimulator.cs`) untuk states/action/reward yang berlaku saat itu.
3. Commit file + config sebelum `mlagents-learn` jalan.
4. Setelah run selesai, isi bagian **Results / Notes** (dari `results/<run-id>/run_logs/training_status.json` dan TensorBoard).

## Run pertama tanpa dokumentasi

Run berikut sudah ada di `results/` tapi TIDAK punya dokumentasi pre-run (dibuat sebelum aturan ini berlaku). Hyperparameter bisa diambil dari `results/<run-id>/configuration.yaml`, tapi states/reward/actions saat run itu terjadi tidak diketahui persis — source code sudah berubah sejak itu. Run-run ini ditandai `UNDOCUMENTED` di tabel bawah.

| Run ID | Status | Catatan |
|--------|--------|---------|
| baseline, firstRun, secondRun | UNDOCUMENTED | hyperparameter ada di config, reward/states unknown |
| test1, test2, test3 | UNDOCUMENTED | config lama (gamma 0.9, 5-obs era?) |
| rl1, rl2, rl4, rl5 | UNDOCUMENTED | gamma 0.9, max_steps 750k |
| hyper1–hyper6 | UNDOCUMENTED | sweep hyperparameter |
| eightRun, nineRun, tenRun, fiveRun, sixRun, sevenRun | UNDOCUMENTED | |
| test6, test7, test8 | UNDOCUMENTED | |
| _run berikutnya_ | **WAJIB** pre-run doc | |

## Referensi source code saat ini

[`reference-current.md`](./reference-current.md) = snapshot states/actions/reward dari source code sekarang (HEAD `exp/1`). Pakai sebagai baseline untuk experiment berikutnya — tulis apa yang berubah dari snapshot ini.

## File per experiment

Format nama: `EXPERIMENT_<run-id>.md` (mis. `EXPERIMENT_ddqn_dda_v2.md`). `run-id` = `--run-id` yang dipakai saat `mlagents-learn`.