﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using JetBrains.Annotations;

	/// <summary>Database connection context.</summary>
	[PublicAPI]
	public interface IFdbTenant : IFdbRetryable
	{
		/// <summary>Name of the tenant</summary>
		FdbTenantName Name { get; }

		IFdbDatabase Database { get; }

		/// <summary>Start a new transaction on this tenant, with the specified mode</summary>
		/// <param name="mode">Mode of the transaction (read-only, read-write, ....)</param>
		/// <param name="ct">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <param name="context">Existing parent context, if the transaction needs to be linked with a retry loop, or a parent transaction. If null, will create a new standalone context valid only for this transaction</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken ct, FdbOperationContext? context = null);

	}

}
