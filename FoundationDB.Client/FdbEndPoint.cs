﻿#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System;
	using System.Net;

	/// <summary>Represents a FoundationDB network endpoint as an IP address, port number and TLS mode.</summary>
	public sealed class FdbEndPoint : IPEndPoint
	{
		private readonly bool m_tls;

		public FdbEndPoint(IPAddress address, int port, bool tls)
			: base(address, port)
		{
			m_tls = tls;
		}

		/// <summary>Gets or sets the TLS mode of the endpoint.</summary>
		/// <remarks>True if the endpoint uses TLS</remarks>
		public bool Tls => m_tls;

		public override SocketAddress Serialize()
		{
			// we add a byte (0 = raw, 1 = tls) to the SocketAddress generated by the base

			var tmp = base.Serialize();
			int count = tmp.Size;

			var sockAddr = new SocketAddress(this.AddressFamily, count + 1);

			for(int i=0;i<count;i++) sockAddr[i] = tmp[i];
			sockAddr[count] = m_tls ? (byte)1 : (byte)0;

			return sockAddr;
		}

		public override EndPoint Create(SocketAddress socketAddress)
		{
			//note: this methods constructs a NEW endpoint, and does not change the current instance (why???)

			// Current implementation of IPEndPoint does really check the exact size of the buffer, and should not use the extra byte we added...
			// > Fix this if this is no longer the case in future .NET implementations (or Mono?)
			var tmp = (IPEndPoint)base.Create(socketAddress);

			bool tls = false;
			int count = socketAddress.Size;
			if ((socketAddress.Family == System.Net.Sockets.AddressFamily.InterNetwork && count == 17) || (socketAddress.Family == System.Net.Sockets.AddressFamily.InterNetworkV6 && count == 29))
			{
				tls = socketAddress[count - 1] != 0;
			}

			return new FdbEndPoint(tmp.Address, tmp.Port, tls);
		}

		public override string ToString()
		{
			string s = base.ToString();
			return m_tls ? (s + ":tls") : s;
		}

		public override bool Equals(object comparand)
		{
			return comparand is FdbEndPoint fep && fep.m_tls == m_tls && base.Equals(fep);
		}

		public override int GetHashCode()
		{
			int h = base.GetHashCode();
			return m_tls ? ~h : h;
		}
	}

}
