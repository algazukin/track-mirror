// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class PrivacyNames
{
    internal const string ResultsFile = "privacy-results.json";
    internal const string TemporarySuffix = ".tmp";
    internal const string CorruptSuffix = ".corrupt-";
    internal const string JsonExtension = ".json";
    internal const string RuntimeDirectory = "privacy-runtime";
    internal const string RuntimeExecutable = "ollama.exe";
    internal const string ModelsDirectory = "models";
    internal const string BlobsDirectory = "blobs";
    internal const string OcrDirectory = "privacy-models";
    internal const string DefaultOcrDirectory = "v5";
    internal const string DetectionModel = "ch_PP-OCRv5_mobile_det.onnx";
    internal const string AngleModel = "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx";
    internal const string RecognitionModel = "eslav_PP-OCRv5_rec_mobile.onnx";
    internal const string Dictionary = "ppocrv5_eslav_dict.txt";
    internal const string Model = "qwen3-vl:4b-instruct";
    internal const string ModelDigest = "ee4b975b58c17ce268cd19d40db35d5edc64603035d2ffc1fee1968eb0947f7b";
    internal const string LocalManifestHash = "3c9f45126ab947c70afd5602e74427b3ef96cc4408589f172840003f294c90b8";
    internal const string Version = "ocr-v5-eslav-rules3-prompt6-activity2-qwen3vl-4b-instruct";
    internal const string LocalEndpoint = "http://127.0.0.1:11437";
    internal const string LocalHost = "127.0.0.1:11437";
    internal const int LocalPort = 11437;
    internal const string OcrFailure = "ocr_failed";
    internal const string VisionFailure = "vision_failed";
    internal const string StorageFailure = "storage_failed";
    internal const string MissingCopy = "copy_unavailable";
    internal const string OcrIncomplete = "ocr_incomplete";
    internal const string ModelUncertain = "model_uncertain";
    internal const string AnalysisLimit = "analysis_limit";
    internal const string ConfidentialVersion = "-ocr-batches1";
    internal const string Incomplete = "incomplete_analysis";
    internal const string Interrupted = "interrupted";
    internal const string Cancelled = "cancelled";
    internal const string RuntimeUnavailable = "runtime_unavailable";
    internal const string RuntimePortUnavailable = "runtime_port_unavailable";
    internal const string RuntimeStopped = "runtime_stopped";
    internal const string ModelUnavailable = "model_unavailable";
    internal const string ModelConnection = "model_connection_failed";
    internal const string ModelTimeout = "model_timeout";
    internal const string ModelMemory = "model_memory_failed";
    internal const string ModelServer = "model_server_failed";
    internal const string ModelRequest = "model_request_rejected";
    internal const string ModelContext = "model_context_exceeded";
    internal const string InvalidResponse = "invalid_model_response";
    internal const string InvalidEvidence = "invalid_model_evidence";
    internal const string TruncatedResponse = "truncated_model_response";
}

