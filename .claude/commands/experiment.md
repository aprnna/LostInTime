---
description: Create a pre-run experiment record from template + current source/config
argument-hint: <run-id> [tujuan/hypothesis]
allowed-tools: Read, Write, Edit, Bash, Glob, Grep
---

# Record experiment before running

User invoked: `$ARGUMENTS`

## Steps

1. Parse argumen. Token pertama = **run-id** (folder `results/<run-id>/`). Sisanya = tujuan/hypothesis (boleh kosong).
2. Tentukan nama file output: `docs/experiments/EXPERIMENT_<run-id>.md`. Kalau file udah ada, jangan overwrite — tanya user, jangan diam-demi-diam ngerusak.
3. Baca 3 file referensi:
   - `docs/experiments/TEMPLATE.md` (struktur)
   - `docs/experiments/reference-current.md` (nilai canonical states/actions/reward/hyperparameter saat ini)
   - `config/ddqn.yaml` (hyperparameter aktual yang akan dipakai)
4. Bikin file baru dari TEMPLATE, isi bagian pre-run (States, Actions, Reward, Episode, Hyperparameter, Environment, Difficulty) **dengan nilai dari reference-current.md dan config/ddqn.yaml**. Jangan tinggal kosong — itu gunanya command ini. Tulis exact values, bukan placeholder.
5. Isi header:
   - Run ID, Tanggal doc (hari ini), Branch (git branch sekarang), Git commit (`git rev-parse HEAD`), Tujuan (dari argumen sisanya).
6. Bagian **Results / Notes** biarkan kosong (tandai `> Isi setelah run selesai`).
7. Jangan commit otomatis. Kasih tau user file udah jadi + path, biar user review dulu sebelum commit + run.

## Aturan

- Pre-run sections HARUS berisi nilai real, bukan template kosong. Tujuan command = ngisiin yang berat dari source/config.
- Kalau `reference-current.md` dan `config/ddqn.yaml` konflik (mis. multiplier beda), source code menang — catat di notes.
- Kalau user bilang ganti nilai tertentu (mis. "gamma 0.9"), pakai itu dan catat di section "Apa yang berubah dari baseline/referensi".
- Bagian "Apa yang berubah dari baseline/referensi" = cuma diff vs `reference-current.md`. Kalau gak ada selain hyperparameter, tulis "hanya hyperparameter".

## Output ke user

Setelah file jadi, kasih ringkasan 3 baris: path file, run-id, git commit. Jangan essay.