﻿//  Copyright 2016 Google Inc. All Rights Reserved.
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

using NtApiDotNet.Win32.SafeHandles;
using NtApiDotNet.Win32.Security.Authentication;
using NtApiDotNet.Win32.Security.Native;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NtApiDotNet.Win32
{
    /// <summary>
    /// Logon type
    /// </summary>
    public enum SecurityLogonType
    {
        /// <summary>
        /// This is used to specify an undefined logon type
        /// </summary>
        UndefinedLogonType = 0,
        /// <summary>
        /// Interactively logged on (locally or remotely)
        /// </summary>
        Interactive = 2,
        /// <summary>
        /// Accessing system via network
        /// </summary>
        Network,
        /// <summary>
        /// Started via a batch queue
        /// </summary>
        Batch,
        /// <summary>
        /// Service started by service controller
        /// </summary>
        Service,
        /// <summary>
        /// Proxy logon
        /// </summary>
        Proxy,
        /// <summary>
        /// Unlock workstation
        /// </summary>
        Unlock,
        /// <summary>
        /// Network logon with cleartext credentials
        /// </summary>
        NetworkCleartext,
        /// <summary>
        /// Clone caller, new default credentials
        /// </summary>
        NewCredentials,
        /// <summary>
        /// Remove interactive.
        /// </summary>
        RemoteInteractive,
        /// <summary>
        /// Cached Interactive.
        /// </summary>
        CachedInteractive,
        /// <summary>
        /// Cached Remote Interactive.
        /// </summary>
        CachedRemoteInteractive,
        /// <summary>
        /// Cached unlock.
        /// </summary>
        CachedUnlock
    }

    /// <summary>
    /// Utilities for user logon.
    /// </summary>
    public static class LogonUtils
    {
        /// <summary>
        /// Logon a user with a username and password.
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="domain">The user's domain.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="type">The type of logon token.</param>
        /// <returns>The logged on token.</returns>
        public static NtToken Logon(string user, string domain, string password, SecurityLogonType type)
        {
            if (!SecurityNativeMethods.LogonUser(user, domain, password, type, 0, out SafeKernelObjectHandle handle))
            {
                throw new SafeWin32Exception();
            }
            return NtToken.FromHandle(handle);
        }

        /// <summary>
        /// Logon a user with a username and password.
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="domain">The user's domain.</param>
        /// <param name="password">The user's password.</param>
        /// <param name="type">The type of logon token.</param>
        /// <param name="groups">Additional groups to add. Needs SeTcbPrivilege.</param>
        /// <returns>The logged on token.</returns>
        public static NtToken Logon(string user, string domain, string password, SecurityLogonType type, IEnumerable<UserGroup> groups)
        {
            TokenGroupsBuilder builder = new TokenGroupsBuilder();
            foreach (var group in groups)
            {
                builder.AddGroup(group.Sid, group.Attributes);
            }

            using (var group_buffer = builder.ToBuffer())
            {
                if (!SecurityNativeMethods.LogonUserExExW(user, domain, password, type, 0, group_buffer, 
                    out SafeKernelObjectHandle token, null, null, null, null))
                {
                    throw new SafeWin32Exception();
                }
                return new NtToken(token);
            }
        }

        /// <summary>
        /// Logon user using S4U
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="realm">The user's realm.</param>
        /// <param name="type">The type of logon token.</param>
        /// <param name="auth_package">The name of the auth package to user.</param>
        /// <param name="throw_on_error">True to throw on error.</param>
        /// <returns>The logged on token.</returns>
        public static NtResult<NtToken> LogonS4U(string user, string realm, SecurityLogonType type, string auth_package, bool throw_on_error)
        {
            NtStatus status;
            SafeLsaHandle hlsa;
            if (!SecurityNativeMethods.LsaRegisterLogonProcess(new LsaString("NtApiDotNet"), out hlsa, out uint _).IsSuccess())
            {
                status = SecurityNativeMethods.LsaConnectUntrusted(out hlsa);
                if (!status.IsSuccess())
                    return status.CreateResultFromError<NtToken>(throw_on_error);
            }

            using (hlsa)
            {
                status = SecurityNativeMethods.LsaLookupAuthenticationPackage(hlsa, new LsaString(auth_package), out uint authnPkg);
                if (!status.IsSuccess())
                    return status.CreateResultFromError<NtToken>(throw_on_error);
                byte[] user_bytes = Encoding.Unicode.GetBytes(user);
                byte[] realm_bytes = Encoding.Unicode.GetBytes(realm);

                using (var buffer = new SafeStructureInOutBuffer<KERB_S4U_LOGON>(user_bytes.Length + realm_bytes.Length, true))
                {
                    KERB_S4U_LOGON logon_struct = new KERB_S4U_LOGON
                    {
                        MessageType = KERB_LOGON_SUBMIT_TYPE.KerbS4ULogon
                    };
                    SafeHGlobalBuffer data_buffer = buffer.Data;

                    logon_struct.ClientUpn.Buffer = data_buffer.DangerousGetHandle();
                    data_buffer.WriteArray(0, user_bytes, 0, user_bytes.Length);
                    logon_struct.ClientUpn.Length = (ushort)user_bytes.Length;
                    logon_struct.ClientUpn.MaximumLength = (ushort)user_bytes.Length;

                    logon_struct.ClientRealm.Buffer = data_buffer.DangerousGetHandle() + user_bytes.Length;
                    data_buffer.WriteArray((ulong)user_bytes.Length, realm_bytes, 0, realm_bytes.Length);
                    logon_struct.ClientRealm.Length = (ushort)realm_bytes.Length;
                    logon_struct.ClientRealm.MaximumLength = (ushort)realm_bytes.Length;

                    Marshal.StructureToPtr(logon_struct, buffer.DangerousGetHandle(), false);

                    TOKEN_SOURCE tokenSource = new TOKEN_SOURCE("NT.NET");
                    SecurityNativeMethods.AllocateLocallyUniqueId(out tokenSource.SourceIdentifier);

                    LsaString originName = new LsaString("S4U");
                    QUOTA_LIMITS quota_limits = new QUOTA_LIMITS();

                    return SecurityNativeMethods.LsaLogonUser(hlsa, originName, type, authnPkg,
                        buffer, buffer.Length, IntPtr.Zero,
                        tokenSource, out SafeLsaReturnBufferHandle profile, 
                        out int cbProfile, out Luid logon_id, out SafeKernelObjectHandle token_handle,
                        quota_limits, out NtStatus subStatus).CreateResult(throw_on_error, () => {
                            using (profile)
                            {
                                return NtToken.FromHandle(token_handle);
                            }
                    });
                }
            }
        }

        /// <summary>
        /// Logon user using S4U
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="realm">The user's realm.</param>
        /// <param name="type">The type of logon token.</param>
        /// <returns>The logged on token.</returns>
        public static NtToken LogonS4U(string user, string realm, SecurityLogonType type)
        {
            return LogonS4U(user, realm, type, "Negotiate", true).Result;
        }

        /// <summary>
        /// Get a logon session.
        /// </summary>
        /// <param name="luid">The logon session ID.</param>
        /// <param name="throw_on_error">True to thrown on error.</param>
        /// <returns>The logon session.</returns>
        public static NtResult<LogonSession> GetLogonSession(Luid luid, bool throw_on_error)
        {
            return LogonSession.GetLogonSession(luid, throw_on_error);
        }

        /// <summary>
        /// Get a logon session.
        /// </summary>
        /// <param name="luid">The logon session ID.</param>
        /// <returns>The logon session.</returns>
        public static LogonSession GetLogonSession(Luid luid)
        {
            return GetLogonSession(luid, true).Result;
        }

        /// <summary>
        /// Get the logon session LUIDs
        /// </summary>
        /// <param name="throw_on_error">True throw on error.</param>
        /// <returns>The list of logon sessions. Only returns ones you can access.</returns>
        public static NtResult<IEnumerable<Luid>> GetLogonSessionIds(bool throw_on_error)
        {
            return LogonSession.GetLogonSessionIds(throw_on_error);
        }

        /// <summary>
        /// Get the logon session LUIDs
        /// </summary>
        /// <returns>The list of logon sessions. Only returns ones you can access.</returns>
        public static IEnumerable<Luid> GetLogonSessionIds()
        {
            return GetLogonSessionIds(true).Result;
        }

        /// <summary>
        /// Get the logon sessions.
        /// </summary>
        /// <param name="throw_on_error">True throw on error.</param>
        /// <returns>The list of logon sessions. Only returns ones you can access.</returns>
        public static NtResult<IEnumerable<LogonSession>> GetLogonSessions(bool throw_on_error)
        {
            return LogonSession.GetLogonSessions(throw_on_error);
        }

        /// <summary>
        /// Get the logon sessions.
        /// </summary>
        /// <returns>The list of logon sessions.</returns>
        public static IEnumerable<LogonSession> GetLogonSessions()
        {
            return GetLogonSessions(true).Result;
        }
    }
}
