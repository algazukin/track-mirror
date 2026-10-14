// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

namespace TrackMirror.Constants;

internal static class PrivacyDiagnosticsSql
{
    internal const string Select = """
        SELECT capture_key,category,request_bytes,ocr_blocks,ocr_characters,prompt_tokens,reply_tokens,
        duration_milliseconds,failure_code,request_id FROM privacy_request_diagnostics ORDER BY position;
        """;

    internal const string Insert = """
        INSERT INTO privacy_request_diagnostics(capture_key,category,position,request_bytes,ocr_blocks,
        ocr_characters,prompt_tokens,reply_tokens,duration_milliseconds,failure_code,request_id)
        VALUES($key,$category,$position,$requestBytes,$blocks,$characters,
        $promptTokens,$replyTokens,$duration,$failure,$request);
        """;

    internal const string RequestBytes = "$requestBytes";
    internal const string Blocks = "$blocks";
    internal const string Characters = "$characters";
    internal const string PromptTokens = "$promptTokens";
    internal const string ReplyTokens = "$replyTokens";
}
