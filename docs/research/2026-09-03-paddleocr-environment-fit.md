# PaddleOCR environment fit for hr-swiss-army-tools

**Date:** 2026-09-03
**Question:** Does PaddleOCR fit this Windows/.NET application, and can it run on machines with modest specifications?
**Scope:** CPU-first OCR for scanned CV pages, using the in-process `Sdcb.PaddleOCR` binding when possible.

## Short answer

**Yes, conditionally.** PaddleOCR is a reasonable fit for the planned background, per-page OCR fallback. A GPU is not required: the .NET binding provides Windows x64 CPU runtimes, including an OpenBLAS/no-AVX option for older CPUs. However, upstream does not publish a minimum RAM or CPU specification, and the published benchmarks are not measurements on low-spec machines. The actual acceptance decision must come from the repository's OCR spike.

Use one OCR consumer at first, process pages sequentially, keep PdfPig as the fast path, and OCR only pages whose text layer is too thin. This matches [ADR-0009](../adr/0009-full-text-requirement-matching-with-hybrid-ocr-extraction.md). It is a poor fit for synchronous request-time OCR, OCR-every-page processing, or a hard real-time throughput requirement on weak hardware.

## Fit with this repository

The solution targets `net10.0`. The proposed architecture already calls for PdfPig first, per-page rasterization and OCR fallback, persisted extracted text, and a background worker. That keeps the common born-digital CV path away from the neural OCR runtime and prevents an eight-to-ten-page CV from blocking the import request. See [ADR-0009](../adr/0009-full-text-requirement-matching-with-hybrid-ocr-extraction.md).

The .NET binding's documented API accepts an OpenCV `Mat`, while the official Python PaddleOCR module documents image and PDF input. Therefore the .NET slice still needs a PDF-page renderer that produces images before `PaddleOcrAll.Run`; PdfPig alone is not the rasterizer. This is an implementation dependency to settle in the spike, not evidence that PaddleOCR can consume the repository's `PdfDocument` directly. Sources: [Sdcb OCR usage](https://github.com/sdcb/PaddleSharp/blob/master/docs/ocr.md), [PaddleOCR general OCR pipeline](https://www.paddleocr.ai/latest/en/version3.x/pipeline_usage/OCR.html), [text detection input API](https://www.paddleocr.ai/latest/en/version3.x/module_usage/text_detection.html).

## Which PaddleOCR capability is needed?

For scanned CV text extraction, the smallest relevant pipeline is:

1. Text detection: find text regions.
2. Text recognition: convert each region to text.
3. Optional orientation handling only when the input requires it.

The official general pipeline also exposes document orientation classification, text image unwarping, and text-line orientation classification as optional modules. Start with those optional modules disabled for ordinary upright CV scans and enable them only when the anonymized sample set demonstrates a need. The official documentation explicitly describes these modules as optional and exposes switches for them. Sources: [general OCR pipeline](https://www.paddleocr.ai/latest/en/version3.x/pipeline_usage/OCR.html), [Sdcb OCR technical details and options](https://github.com/sdcb/PaddleSharp/blob/master/docs/ocr.md).

Do not choose PaddleOCR-VL, PP-StructureV3, table parsing, or other document-understanding models for this requirement. They solve broader document-parsing problems and are unnecessary for plain full-text extraction; the repository's matching contract needs extracted text, not document layout reconstruction. The PaddleOCR project distinguishes the PP-OCR text pipeline from PP-Structure and VLM capabilities in its [project README](https://github.com/PaddlePaddle/PaddleOCR).

## Important version boundary

The official PaddleOCR documentation currently defaults the Python general OCR pipeline to PP-OCRv6 and lists PP-OCRv6 tiny, small, and medium models. The current `Sdcb.PaddleOCR` package documentation is a separate .NET wrapper line: `Sdcb.PaddleOCR` 3.3.1 targets `netstandard2.0`, and `Sdcb.PaddleOCR.Models.Local` documents maintained local **V5** model entry points such as `LocalFullModels.ChineseV5`. Do not take PP-OCRv6 model names, sizes, or benchmarks as proof that the current .NET package can load them. Sources: [PaddleOCR general pipeline](https://www.paddleocr.ai/latest/en/version3.x/pipeline_usage/OCR.html), [`Sdcb.PaddleOCR` 3.3.1 on NuGet](https://www.nuget.org/packages/Sdcb.PaddleOCR/3.3.1), [Sdcb local-model documentation](https://github.com/sdcb/PaddleSharp/blob/master/docs/ocr.md).

The binding's project source targets `netstandard2.0`; its inference package source targets `net6`, `net8`, `netstandard2.0`, and `net45`. That is compatible with the repository's `net10.0` projects through .NET Standard compatibility, but the native runtime still has to match the deployed operating system and architecture. Sources: [`Sdcb.PaddleOCR.csproj`](https://github.com/sdcb/PaddleSharp/blob/master/src/Sdcb.PaddleOCR/Sdcb.PaddleOCR.csproj), [`Sdcb.PaddleInference.csproj`](https://github.com/sdcb/PaddleSharp/blob/master/src/Sdcb.PaddleInference/Sdcb.PaddleInference.csproj), [repository project files](https://github.com/sdcb/PaddleSharp).

A Python sidecar has different prerequisites. The official PaddleOCR installation guide states that the base `paddleocr` package supports Python 3.8 and later, while most optional capability groups require Python 3.9 or later; local inference also requires installing the selected inference engine. The official PaddlePaddle guide documents a CPU installation path, so a sidecar does not require a GPU, but it does add a separate Python runtime and process. None of these Python dependencies are required for the in-process .NET path. Sources: [PaddleOCR installation](https://www.paddleocr.ai/latest/en/version3.x/installation.html), [PaddlePaddle installation](https://www.paddleocr.ai/latest/en/version3.x/paddlepaddle_installation.html).

## Windows deployment shape

For the documented Windows local-model path, the binding lists these packages:

```text
Sdcb.PaddleInference
Sdcb.PaddleOCR
Sdcb.PaddleOCR.Models.Local
Sdcb.PaddleInference.runtime.win64.mkl
OpenCvSharp4.runtime.win
```

The binding recommends `Sdcb.PaddleInference.runtime.win64.mkl` for most users. It also lists `win64.openblas` and `win64.openblas-noavx`; the latter is intended for older CPUs without AVX. The binding describes MKL-DNN as generally fast, while OpenBLAS is slower but has smaller dependencies and lower memory use. Use exactly one native inference runtime, and pin the managed and native package versions together. Source: [PaddleSharp package-selection guide](https://github.com/sdcb/PaddleSharp/blob/master/README.md).

Known Windows prerequisites and failure modes documented by the binding:

- Install the current Microsoft Visual C++ Redistributable; otherwise `paddle_inference_c` may fail to load.
- The native packages are Windows x64 packages. A 32-bit process or an unsupported architecture is not a viable deployment target for this path.
- OpenCvSharp may require Media Foundation on older Windows Server 2012 R2 installations.
- Legacy Windows 7 workarounds are documented, but should not be the deployment baseline for a new .NET 10 application.

Source: [PaddleSharp Windows FAQ](https://github.com/sdcb/PaddleSharp/blob/master/README.md).

For this repository, CPU-only is the simpler baseline. The GPU packages add CUDA/cuDNN/TensorRT and GPU-specific package selection. The binding documents those as manual prerequisites; none is needed to establish whether background OCR is viable. Source: [PaddleSharp devices and GPU FAQ](https://github.com/sdcb/PaddleSharp/blob/master/README.md).

## What low-spec machines can reasonably do

There is no upstream minimum RAM, CPU model, or end-to-end low-spec benchmark in the sources reviewed. The following is therefore an engineering recommendation, not a vendor guarantee.

A modest machine is a plausible target when it has:

- Windows x64 and a CPU compatible with the selected native runtime;
- enough memory headroom for the ASP.NET process, database/client services, one decoded page image, and one loaded OCR pipeline;
- tolerance for queueing and background latency rather than an immediate HTTP response;
- enough local disk for the native runtime, model files, temporary rendered pages, and normal application growth.

An older CPU is not automatically disqualifying. Prefer MKL when the CPU supports the required instructions; select the documented `openblas-noavx` runtime for a CPU without AVX and expect lower performance. If memory is the limiting factor, OpenBLAS is the first alternative to benchmark because the binding says its dependency file is smaller and it consumes less memory than MKL-DNN. Source: [PaddleSharp package-selection and device guide](https://github.com/sdcb/PaddleSharp/blob/master/README.md).

The binding exposes controls that are useful on constrained machines:

- `PaddleConfig.MkldnnCacheCapacity` defaults to `10`. The binding documents a positive relationship between this cache capacity and peak memory, with a performance tradeoff for varying image sizes.
- `PaddleOcrAll.Detector.MaxSize` defaults to `960`. Lowering it can improve performance and reduce memory, at the cost of accuracy.
- `Enable180Classification` is documented as disabled by default; closing that extra step can improve speed when the input does not need it.
- The ASP.NET Core example registers one `QueuedPaddleOcrAll` singleton with `consumerCount: 1`, which is a sensible first setting for a weak host.

Source: [Sdcb OCR optimization and ASP.NET Core guidance](https://github.com/sdcb/PaddleSharp/blob/master/docs/ocr.md).

Do not describe a machine as "supported" based only on its RAM number. Peak usage depends on page dimensions, rendering resolution, cache behavior, model choice, concurrent consumers, and the rest of the application. The sources do not justify a universal claim such as "1 GB is enough", nor do they justify a fixed pages-per-second promise.

## Performance evidence

The official PaddleOCR module pages list model storage sizes and CPU inference measurements. For example, the current PP-OCRv6 tables list:

- detection: tiny `1.9 MB`, small `9.6 MB`, medium `59.4 MB`;
- recognition: tiny `4.4 MB`, small `20.4 MB`, medium `73.3 MB`;
- CPU model-inference times around `24.85/24.45/33.14 ms` for v6 tiny/small/medium detection and `5.04/7.41/8.08 ms` for v6 tiny/small/medium recognition.

These numbers are **not** a low-spec deployment estimate. The pages state that inference time excludes pre-processing and post-processing; the test environment uses an Intel Xeon Gold 6248 at 2.50 GHz with eight CPU threads, and the software/test data differ from a real CV workload. They also describe PP-OCRv6 metrics as using a different evaluation set from v5/v4, so the accuracy figures are not directly comparable across versions. Sources: [text detection models and benchmark](https://www.paddleocr.ai/latest/en/version3.x/module_usage/text_detection.html), [text recognition models and benchmark](https://www.paddleocr.ai/latest/en/version3.x/module_usage/text_recognition.html).

Those v6 figures must not be added together and converted into pages per second for this repository: the .NET local package documents V5 model entry points, PDF rasterization and post-processing add work, and the target machines are unknown. Treat model size as a model-file comparison only, not as total process memory or deployment footprint.

## Privacy, model downloads, and licensing

The local-model .NET example loads a local model, while the online-model example downloads model files on demand. For candidate documents, prefer a pinned local model and make model installation part of deployment rather than allowing a production worker to fetch weights during the first request. Source: [Sdcb OCR model usage](https://github.com/sdcb/PaddleSharp/blob/master/docs/ocr.md).

The PaddleOCR repository and the PaddleSharp repository publish Apache License 2.0 licenses. Apache 2.0 permits commercial use and redistribution subject to its notice and license conditions, but this is not a complete legal review. The application should inventory the licenses of OpenCvSharp, native runtime packages, model files, and all transitive dependencies before redistribution; the repository-level Apache notice alone does not prove that every bundled artifact has identical licensing. Sources: [PaddleOCR LICENSE](https://github.com/PaddlePaddle/PaddleOCR/blob/main/LICENSE), [PaddleSharp LICENSE](https://github.com/sdcb/PaddleSharp/blob/master/LICENSE), [Sdcb package license metadata](https://www.nuget.org/packages/Sdcb.PaddleOCR/3.3.1).

## Recommended spike before implementation

Use the existing `.scratch/ocr-spike/` plan from [ADR-0009](../adr/0009-full-text-requirement-matching-with-hybrid-ocr-extraction.md):

1. Use the current .NET binding's documented local V5 model path and the Windows x64 CPU runtime. Start with MKL-DNN; test OpenBLAS/no-AVX only where the target CPU requires or benefits from it.
2. Render the worst 5-10 anonymized PDFs into page images, then run only the pages that fail the PdfPig thinness threshold.
3. Measure peak process working set, model-load time, per-page p50/p95 latency, queue behavior with `consumerCount: 1`, and text accuracy for the exact requirement tokens, including `C#` and `.NET`.
4. Test at least two detector sizes and the orientation options against the same pages. Keep the smallest setting that preserves the matching-critical text.
5. Record the runtime package, model files, OS, CPU instruction support, and deployment disk footprint with the measurements.

The spike passes only if OCR accuracy is adequate and the worker leaves headroom for the API and database. If it fails because the V5 .NET model/language support is inadequate, or because the host cannot absorb the native runtime, escalate to the Python PaddleOCR sidecar described by ADR-0009. A sidecar may isolate native failures and expose newer PaddleOCR models, but it is not automatically cheaper in RAM because it adds another process and runtime.

## Recommendation

Proceed with PaddleOCR as a **conditional in-process CPU spike**, not as an unconditional package addition. It fits the repository's Windows/.NET 10 architecture and can run on modest x64 hardware when OCR is a serialized background task. Do not promise suitability for an unspecified "bad" machine until the spike measures the actual host. Keep the Python sidecar as the fallback for current PaddleOCR model/language coverage or an in-process binding failure.

## Evidence gaps

- No official minimum RAM or CPU specification for the Sdcb binding was found.
- No official end-to-end benchmark was found for Sdcb.PaddleOCR on this repository's PDF renderer, target .NET version, or low-spec Windows machines.
- The official PaddleOCR v6 model tables do not establish the disk or memory footprint of the binding's documented local V5 models.
- Exact model licensing and the complete native dependency license inventory require a separate release audit.

## Primary sources

All sources were accessed 2026-09-03.

1. [PaddleOCR general OCR pipeline](https://www.paddleocr.ai/latest/en/version3.x/pipeline_usage/OCR.html)
2. [PaddleOCR text detection module](https://www.paddleocr.ai/latest/en/version3.x/module_usage/text_detection.html)
3. [PaddleOCR text recognition module](https://www.paddleocr.ai/latest/en/version3.x/module_usage/text_recognition.html)
4. [PaddleOCR installation guide](https://www.paddleocr.ai/latest/en/version3.x/installation.html)
5. [PaddlePaddle installation guide](https://www.paddleocr.ai/latest/en/version3.x/paddlepaddle_installation.html)
6. [PaddleOCR repository README](https://github.com/PaddlePaddle/PaddleOCR)
7. [PaddleOCR Apache 2.0 license](https://github.com/PaddlePaddle/PaddleOCR/blob/main/LICENSE)
8. [PaddleSharp README](https://github.com/sdcb/PaddleSharp/blob/master/README.md)
9. [Sdcb PaddleOCR documentation](https://github.com/sdcb/PaddleSharp/blob/master/docs/ocr.md)
10. [`Sdcb.PaddleOCR` 3.3.1 package](https://www.nuget.org/packages/Sdcb.PaddleOCR/3.3.1)
11. [`Sdcb.PaddleInference` 3.3.1 package](https://www.nuget.org/packages/Sdcb.PaddleInference/3.3.1)
12. [Sdcb.PaddleOCR project source](https://github.com/sdcb/PaddleSharp/blob/master/src/Sdcb.PaddleOCR/Sdcb.PaddleOCR.csproj)
13. [Sdcb.PaddleInference project source](https://github.com/sdcb/PaddleSharp/blob/master/src/Sdcb.PaddleInference/Sdcb.PaddleInference.csproj)
14. [PaddleSharp Apache 2.0 license](https://github.com/sdcb/PaddleSharp/blob/master/LICENSE)
