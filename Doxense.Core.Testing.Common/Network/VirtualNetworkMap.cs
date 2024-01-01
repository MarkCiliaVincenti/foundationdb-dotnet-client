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

namespace Doxense.Networking
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Networking.Http;

	/// <summary>Implémentation par défaut d'un <see cref="INetworkMap"/></summary>
	/// <remarks>Permet de register des "simulated hosts" qui utilisent un <see cref="HttpMessageHandler"/> spécifique</remarks>
	public class VirtualNetworkMap : NetworkMap, IVirtualNetworkMap
	{

		public VirtualNetworkMap(VirtualNetworkTopology topology, VirtualNetworkTopology.SimulatedHost host)
		{
			this.Topology = topology;
			this.Host = host;
		}

		public VirtualNetworkTopology Topology { get; }
		IVirtualNetworkTopology IVirtualNetworkMap.Topology => this.Topology;

		public VirtualNetworkTopology.SimulatedHost Host { get; }
		IVirtualNetworkHost IVirtualNetworkMap.Host => this.Host;

		private HttpMessageHandler CreateFailedHandler(VirtualNetworkTopology.SimulatedHost host, string hostOrAddress)
		{
			if (IPAddress.TryParse(hostOrAddress, out var ip))
				return VirtualDeadHttpClientHandler.SimulateConnectFailure($"Found not matching host for IP {ip} visible from host {this.Host.Id}");
			else
				return VirtualDeadHttpClientHandler.SimulateNameResolutionFailure($"Found no matching host for name '{hostOrAddress}' visible from host {this.Host.Id}");
		}

		public VirtualNetworkTopology.SimulatedHost? FindHost(string hostOrAddress)
		{
			if (IPAddress.TryParse(hostOrAddress, out var ip))
			{
				if (IPAddress.IsLoopback(ip))
				{ // localhost!
					return this.Host;
				}
			}
			else
			{
				if (string.Equals(hostOrAddress, "localhost", StringComparison.OrdinalIgnoreCase))
				{ // localhost!
					return this.Host;
				}
			}

			if (!this.Topology.HostsByNameOrAddress.TryGetValue(hostOrAddress, out var hostId))
			{
#if DEBUG
				// si ca ressemble a un host simulé et qu'il est inconnu... c'est probablement un oubli dans le setup du test!
				if (hostOrAddress.EndsWith(".simulated", StringComparison.OrdinalIgnoreCase) || hostOrAddress.StartsWith("83.73.77.", StringComparison.Ordinal))
				{
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					throw new InvalidOperationException($"You probably forget to register simulated device '{hostOrAddress}' during startup of the test!");
				}
				// note: en release on retourne "null" donc ca sera considéré comme un "vrai" device
#endif
				return null;
			}

			if (!this.Topology.HostsById.TryGetValue(hostId, out var host))
			{
				throw new InvalidOperationException($"Inconsistent host indexing in virtual network: '{hostOrAddress}' references host '{hostId}' which is not known??");
			}

			return host;
		}

		[Obsolete("Use CreateBetterHttpHandler instead")]
		public override HttpMessageHandler? CreateHttpHandler(string hostOrAddress, int port)
		{
			Contract.NotNullOrWhiteSpace(hostOrAddress);

			var host = FindHost(hostOrAddress);
			if (host == null) return base.CreateHttpHandler(hostOrAddress, port);

			var (local, remote) = FindNetworkPath(host, hostOrAddress);
			if (local != null && remote != null)
			{
				//TODO: check si les deux hosts veulent se parler quand meme!
				var handler = host.FindHandler(remote, port);
				if (handler != null)
				{
					return handler();
				}

				return VirtualDeadHttpClientHandler.SimulatePortNotBoundFailure($"Found not port {port} bound on location '{remote}' of target host '{host.Id}', visible from host '{this.Host.Id}'");
				//TODO: BUGBUG: ici le failed handler doit simuler un "connection reset be remote host" (ie: il existe mais port pas bindé!)
			}

			return CreateFailedHandler(host, hostOrAddress);
		}

		public (VirtualNetworkTopology.SimulatedNetwork? Local, VirtualNetworkTopology.SimulatedNetwork? Remote) FindNetworkPath(VirtualNetworkTopology.SimulatedHost target, string hostOrAddress)
		{
			if (target.Equals(this.Host))
			{ // localhost!
				if (hostOrAddress == "127.0.0.1" || hostOrAddress == "::1" || string.Equals(hostOrAddress, "localhost", StringComparison.OrdinalIgnoreCase)) //BUGBUG: TODO: proper check!
				{
					return (this.Host.Loopback, this.Host.Loopback);
				}
				else
				{
					return (this.Host.Locations[0], this.Host.Locations[0]); //HACKHACK
				}
			}

			// pour l'instant on fait du routage "direct"
			foreach (var loc in target.Locations.Intersect(this.Host.Locations))
			{
				//TODO: check si les deux hosts veulent se parler quand meme!
				return (loc, loc);
			}

			foreach (var loc in target.Locations)
			{
				// hackhack: on considère qu'un network cloud est accessible par tout le monde!
				if (loc.Type == VirtualNetworkType.Cloud) return (this.Host.Locations[0], loc);
			}

			return (null, null);
		}

		public override HttpMessageHandler CreateBetterHttpHandler(Uri baseAddress, BetterHttpClientOptions options)
		{
			return new VirtualHttpClientHandler(this, baseAddress, options);
		}

		[Obsolete("C'est charge a l'appelant de résolver l'IP")]
		public override ValueTask<IPAddress?> GetPublicIPAddressForHost(string hostNameOrAddress)
		{
			if (hostNameOrAddress == "127.0.0.1" || hostNameOrAddress == "localhost")
			{
				return new (IPAddress.Loopback);
			}

			if (hostNameOrAddress == "::1")
			{
				return new (IPAddress.IPv6Loopback);
			}
			//HACKHACK: on part du principe que la premiere location est le lan!
			return new (this.Host.Addresses[0]);
		}

		public override Task<IPHostEntry> DnsLookup(string hostNameOrAddress, AddressFamily? family, CancellationToken ct)
		{
			return this.Topology.DnsResolve(hostNameOrAddress, this.Host, family, ct);
		}

		public override IReadOnlyList<NetworkAdaptorDescriptor> GetNetworkAdaptors()
		{
			var res = new List<NetworkAdaptorDescriptor>();
			int idx = 0;
			foreach (var net in this.Host.Adapters)
			{
				res.Add(new NetworkAdaptorDescriptor()
				{
					Id = net.Id,
					Index = ++idx,
					Name = net.Name,
					Description = net.Type.ToString(),
					DnsSuffix = net.Location.Options.DnsSuffix,
					PhysicalAddress = net.PhysicalAddress,
					Speed = null, // TODO
					Type = NetworkInterfaceType.Ethernet,
					UnicastAddresses = net.UnicastAddresses.Select(x => new NetworkAdaptorDescriptor.UnicastAddressDescriptor()
					{
						Address = x.Address,
						IPv4Mask = x.Mask,
						PrefixLength = x.PrefixLength,
					}).ToArray(),
				});
			}
			return res;
		}
	}

	public class VirtualDeadHttpClientHandler : HttpClientHandler
	{

		private Func<Uri, Exception> Handler { get; }

		public VirtualDeadHttpClientHandler(Func<Uri, Exception> handler)
		{
			this.Handler = handler;
		}

		public static VirtualDeadHttpClientHandler SimulatePortNotBoundFailure(string debugReason)
		{
			return new VirtualDeadHttpClientHandler((uri) =>
			{
				var webEx = new System.Net.WebException($"No connection could be made because the target machine actively refused it {uri.DnsSafeHost}:{uri.Port}", WebExceptionStatus.ConnectFailure);
				return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
			});
		}

		public static VirtualDeadHttpClientHandler SimulateNameResolutionFailure(string debugReason)
		{
			return new VirtualDeadHttpClientHandler((uri) =>
			{
				var webEx = new System.Net.WebException($"The remote name could not be resolved: '{uri.Host}'", WebExceptionStatus.NameResolutionFailure);
				return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
			});
		}

		public static VirtualDeadHttpClientHandler SimulateConnectFailure(string debugReason)
		{
			return new VirtualDeadHttpClientHandler((uri) =>
			{
				var sockEx = new System.Net.Sockets.SocketException(10060); // TimedOut
				var webEx = new System.Net.WebException("Unable to connect to the remove server", sockEx, WebExceptionStatus.ConnectFailure, null);
				return new HttpRequestException($"An error occurred while sending the request. [{debugReason}]", webEx);
			});
		}

		protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			throw this.Handler(request.RequestUri);
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			throw this.Handler(request.RequestUri);
		}
	}

}
