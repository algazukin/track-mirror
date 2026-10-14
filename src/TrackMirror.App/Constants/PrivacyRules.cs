// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class PrivacyRules
{
    internal const string Credentials = "credentials";
    internal const string PersonalData = "personal_data";
    internal const string SensitiveDocument = "sensitive_document";
    internal const string Game = "game";
    internal const string PersonalChat = "personal_chat";
    internal const string High = "high";
    internal const string Medium = "medium";
    internal const string TokenReason = "recognized_token";
    internal const string PasswordReason = "revealed_password";
    internal const string PrivateKeyReason = "private_key_material";
    internal const string ContextReason = "visual_context";
    internal const string TokenPattern =
        @"\b(?:gh[pousr]_[A-Za-z0-9_]{20,255}|sk[-_](?:live|test|proj)[-_][A-Za-z0-9_-]{16,255})\b";
    internal const string PasswordPattern = @"(?i)\b(?:password|pwd|пароль)\s*[:=]\s*([^\s;]{6,128})";
    internal const string BearerPattern =
        @"(?i)\bBearer\s+([A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,})";
    internal const string PlaceholderPattern = @"(?i)(YOUR_|INSERT|EXAMPLE|SAMPLE|<|>|\*{3,}|•{3,})";
    internal const string PrivateKeyPattern = @"(?:BEGIN|END) (?:OPENSSH |RSA |EC )?PRIVATE KEY";
    internal const string Base64Pattern = @"^[A-Za-z0-9+/А-Яа-я]{32,}={0,2}$";
    internal const string PasswordLabelPattern = @"(?i)^(?:password|pwd|пароль)\s*[:=]\s*$";
    internal const string RevealedValuePattern = @"^[^\s;]{6,128}$";
}
