﻿//  Copyright 2022 Google LLC. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

namespace NtApiDotNet.Win32.Security.Authentication.Kerberos.Client
{
    /// <summary>
    /// Configuration class for the client authentication context.
    /// </summary>
    public sealed class KerberosClientAuthenticationContextConfig
    {
        /// <summary>
        /// Specify the sub-key encryption type.
        /// </summary>
        public KerberosEncryptionType? SubKeyEncryptionType { get; set; }

        /// <summary>
        /// Specify an explicit sub-key. Used in preference to SubKeyEncryptionType.
        /// </summary>
        public KerberosAuthenticationKey SubKey { get; set; }

        /// <summary>
        /// Channel binding hash.
        /// </summary>
        public byte[] ChannelBinding { get; set; }
    }
}
