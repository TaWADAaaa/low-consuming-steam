# Patcher memory benchmark

Measured on 2026-10-19. This compares the **patch tool's ASAR reader and repacker**, not Discord's running client. It does not establish lower idle Discord memory, CPU or GPU use, and does not verify the combined 2 GiB application budget.

The v0.5 implementation keeps unchanged archive members as slices of the original input buffer. v0.4 allocated another byte array for every member while reading. Both versions still validate the archive and its integrity hashes.

## Results

Each value below is the median of three independent processes per version. MiB means 1,048,576 bytes. Each row has its own median.

| Measurement | v0.4.0 | v0.5.0 local build | Change |
| --- | ---: | ---: | ---: |
| Additional live managed memory after `Read` | 64.50 MiB | 0.50 MiB | 64.00 MiB less |
| Additional live managed memory after `Read` and `Repack` | 128.50 MiB | 64.50 MiB | 64.00 MiB less |
| Process peak working set through repacking | 284.04 MiB | 156.20 MiB | 45.0% lower |
| `Read` elapsed time | 81.17 ms | 66.69 ms | 17.8% shorter |
| `Repack` elapsed time | 185.82 ms | 147.12 ms | 20.8% shorter |
| `Read` plus `Repack` elapsed time | 266.81 ms | 213.81 ms | 19.9% shorter |

The additional-memory rows subtract a baseline that already holds the complete 64 MiB input. The input, parsed archive and repacked output have the same deliberate lifetimes in both versions. These figures do not mean that the entire patch tool uses only 0.50 MiB.

## Method

1. A separate generator process used the published v0.4 ASAR implementation to create one synthetic archive with four 16 MiB binary members and a small UTF-8 text member. It contains no application files or personal data.
2. A separate x64 .NET Framework harness loaded either version's executable as an assembly and called the same public `AsarArchive.Read(byte[])`, `Repack()` and `GetFile(string)` APIs through reflection. The application's GUI and installed clients were never started by the benchmark.
3. The harness warmed up both ASAR operations on a tiny archive, loaded the common input, collected unreachable managed objects, then recorded its baseline. Disk loading and fixture generation are excluded from operation timings.
4. It measured `Read`, collected unreachable objects and measured live managed memory. It then measured `Repack` and repeated that memory sample. `GC.KeepAlive` retained the input, parsed archive and output through their samples. Garbage collection was only part of this measurement procedure; no client working-set trimming or production memory-cleaner feature was used.
5. It sampled Windows' process peak working set immediately after repacking, before the separate full-content verification. This peak includes harness startup, warmup and the input buffer. It is a working-set measure, not GPU memory or private commit.
6. It verified that the complete repacked archive was byte-identical to the original and then read all five members back and checked every payload byte. All six runs passed both checks.

Run order alternated v0.4 then v0.5 three times on the same Windows x64 host, CLR `4.0.30319.42000`, with 24 logical processors. No CPU affinity limit was applied. The host was not an isolated performance laboratory. These short timings and the three-run sample are useful local evidence, not a performance guarantee for a four-core PC.

## Individual runs

| Run | Version | Extra managed memory after read, bytes | Extra managed memory after repack, bytes | Peak working set, bytes | Read + repack, ms | Complete roundtrip |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| 1 | v0.4.0 | 67,632,424 | 134,742,536 | 297,836,544 | 262.6603 | Exact |
| 1 | v0.5.0 | 523,176 | 67,633,288 | 163,770,368 | 209.3490 | Exact |
| 2 | v0.4.0 | 67,632,504 | 134,742,616 | 297,754,624 | 266.8095 | Exact |
| 2 | v0.5.0 | 523,496 | 67,633,608 | 163,782,656 | 214.1010 | Exact |
| 3 | v0.4.0 | 67,632,424 | 134,742,536 | 297,877,504 | 271.7436 | Exact |
| 3 | v0.5.0 | 523,496 | 67,633,608 | 163,794,944 | 213.8141 | Exact |

## Tested inputs

These hashes identify the exact local snapshots measured. A later rebuild can have a different executable hash even when the ASAR algorithm is unchanged. Use a release's own checksum file for its downloadable assets.

| Input | Size, bytes | SHA-256 |
| --- | ---: | --- |
| Synthetic archive | 67,111,080 | `cce4df2161d73f7194c7bdf49fb9819886bb0e75b7f962c741ff5b7cdc1379e1` |
| v0.4.0 Discord patcher assembly | 86,016 | `20ac2b321c429ab0468b1e9f19f457ab3a7eaba07c1e1422d5e7a0d1ffb90a93` |
| v0.5.0 Discord patcher assembly snapshot | 111,616 | `c4af3a8d64dd2fbdebe6baac6d903c699afdfbf473155d01834ef3fd6c056947` |

The benchmark harness, raw local run files and generated archive were kept outside the publication package. The published ASAR implementation and this methodology describe the operation being compared.
