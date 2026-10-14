# Third-party notices

The application and its optional local privacy scanner use the following projects. Model downloads and the
portable inference runtime are not checked into the repository.

| Component | Version / source | License |
| --- | --- | --- |
| .NET 10 Core and Windows Desktop runtime (self-contained release) | [dotnet](https://github.com/dotnet/runtime) | MIT and bundled third-party notices |
| Inno Setup (setup/uninstall runtime) | [Inno Setup](https://jrsoftware.org/) | Inno Setup License; bundled in `licenses/Inno-Setup-LICENSE.txt` |
| Microsoft.Data.Sqlite / Microsoft.Data.Sqlite.Core | [10.0.13](https://github.com/dotnet/efcore) | MIT |
| SQLitePCLRaw | [2.1.13](https://github.com/ericsink/SQLitePCL.raw) | Apache-2.0 |
| SQLite | [SQLite](https://sqlite.org/copyright.html), bundled by SQLitePCLRaw | Public domain |
| RapidOcrNet | [4.2.0](https://github.com/BobLd/RapidOcrNet) | Apache-2.0 |
| RapidOCR model conversion | [RapidAI/RapidOCR](https://github.com/RapidAI/RapidOCR) | Apache-2.0 |
| PaddleOCR PP-OCRv5 mobile detection, orientation and eslav recognition weights | [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR), distributed by [RapidAI](https://modelscope.cn/models/RapidAI/RapidOCR) | Apache-2.0 |
| ONNX Runtime | [Microsoft](https://github.com/microsoft/onnxruntime) | MIT |
| SkiaSharp | [Mono](https://github.com/mono/SkiaSharp) | MIT |
| Clipper2 | [Angus Johnson](https://github.com/AngusJohnson/Clipper2) | BSL-1.0 |
| Ollama | [v0.40.3](https://github.com/ollama/ollama/tree/v0.40.3) | MIT; bundled inference libraries have their own notices |
| Qwen3-VL 4B Instruct | [Qwen](https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct) | Apache-2.0 |
| Qwen3-VL 2B Instruct | [Qwen](https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct) | Apache-2.0 |
| Qwen3-VL 8B Instruct | [Qwen](https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct) | Apache-2.0 |
| Gemma 3 4B | [Google](https://huggingface.co/google/gemma-3-4b-it) | [Gemma Terms of Use](https://ai.google.dev/gemma/terms) |

Ollama Windows archive SHA-256:
`E5ECFF02DFC7CAE8DDF6D6B7A7AC5F727FFEEDF4491F44AE75FF5B0820DBBBD5`

Ollama model manifest digest (`qwen3-vl:4b-instruct`):
`ee4b975b58c17ce268cd19d40db35d5edc64603035d2ffc1fee1968eb0947f7b`

Additional pinned upstream OCI manifest digests:

- `qwen3-vl:2b-instruct`: `ea422f1e73652a95479954d8572d3c8c6022f628ce2d38a1a04aae1b7f2d5300`
- `qwen3-vl:8b-instruct`: `0533d74300e4f9bc367d675d4e64ffd073d50ff16a2b4096cc2e8a1cf8c96319`
- `gemma3:4b`: `68ceab7e468a31c988b44d5a4984a0e21f58c4f7158c6ce29c1804babac38df7`

The original upstream manifests are embedded from `Resources/PrivacyModels`. The matching upstream
and pinned Ollama-generated local digests are listed in `Constants/PrivacyModelNames.cs` and
`Constants/PrivacyNames.cs`. Model layers, including bundled license layers, are checked after download.
Review the corresponding model's terms before redistributing its downloaded weights.

The custom recognition assets copied to `Resources/Privacy` come from the official RapidAI
`onnx/PP-OCRv5/rec` and dictionary paths listed by RapidOCR v3.10.0:

- `eslav_PP-OCRv5_rec_mobile.onnx`, SHA-256
  `08705D6721849B1347D26187F15A5E362C431963A2A62BFFF4FEAC578C489AAB`.
- `ppocrv5_eslav_dict.txt`, SHA-256
  `3E95F1581557162870CACDBA5AF91A4C6BE2890710D395B0C3C7578E7EE5E6EB`.

The NuGet package also supplies the detection/orientation models and native runtime dependencies.
Preserve bundled license files when distributing published binaries. This notice does not replace
upstream licenses. See each linked project's license for redistribution terms.


The local manifest emitted by pinned Ollama after pulling this OCI model has SHA-256
`3c9f45126ab947c70afd5602e74427b3ef96cc4408589f172840003f294c90b8`.
Ollama can report either the upstream OCI digest or this local manifest digest depending on whether
it has just pulled the model or reloaded it after a restart. Both identifiers are pinned.
Full dependency license texts are included in `licenses/` and copied to published packages.

Release packaging copies LICENSE and THIRD-PARTY-NOTICES from the exact .NET runtime NuGet packs
used by the self-contained publish into licenses/. Project MPL-2.0 does not change upstream terms.
