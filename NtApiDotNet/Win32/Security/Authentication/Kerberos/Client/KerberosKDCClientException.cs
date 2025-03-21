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

using System;

namespace NtApiDotNet.Win32.Security.Authentication.Kerberos.Client
{
    /// <summary>
    /// Exception class for a KDC client error.
    /// </summary>
    public sealed class KerberosKDCClientException : ApplicationException
    {
        /// <summary>
        /// The kerberos error code.
        /// </summary>
        public KerberosErrorType ErrorCode => Error.ErrorCode;

        /// <summary>
        /// The detailed kerberos error.
        /// </summary>
        public KerberosErrorAuthenticationToken Error { get; }

        internal KerberosKDCClientException(string message) : base(message)
        {
        }

        internal KerberosKDCClientException(KerberosErrorAuthenticationToken error) 
            : base($"Kerberos Error: {error.ErrorCode}")
        {
            Error = error;
        }
    }
}
