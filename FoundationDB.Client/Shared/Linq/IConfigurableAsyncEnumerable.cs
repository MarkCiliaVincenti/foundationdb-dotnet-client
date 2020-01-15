﻿#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Linq
{
	using System;
	using System.Threading;
	using System.Collections.Generic;
	using JetBrains.Annotations;

	/// <summary>Asynchronous version of the <see cref="System.Collections.Generic.IEnumerable{T}"/> interface, allowing elements of the enumerable sequence to be retrieved asynchronously.</summary>
	public interface IConfigurableAsyncEnumerable<out T> : IAsyncEnumerable<T>
	{

		/// <summary>Gets an asynchronous enumerator over the sequence.</summary>
		/// <param name="ct">Token used to cancel the iterator from the outside</param>
		/// <param name="hint">Defines how the enumerator will be used by the caller. The source provider can use the mode to optimize how the results are produced.</param>
		/// <returns>Enumerator for asynchronous enumeration over the sequence.</returns>
		IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct, AsyncIterationHint hint);
	}

}

#endif
