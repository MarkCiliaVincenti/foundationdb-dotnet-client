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

#nullable enable

namespace Doxense.Diagnostics
{
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.IO.Hashing;
	using JetBrains.Annotations;

	/// <summary>Helper class pour dumper des blobs binaires en hexa</summary>
	public static class HexaDump
	{
		/// <summary>Options de formatage du dump hexadécimal</summary>
		[Flags]
		public enum Options
		{
			/// <summary>Affichage standard</summary>
			Default = 0,
			/// <summary>N'affiche pas la preview ASCII</summary>
			NoPreview = 1,
			/// <summary>N'ajoutes pas les headers</summary>
			NoHeader = 2,
			/// <summary>N'ajoutes pas les footers</summary>
			NoFooter = 4,
			/// <summary>Affiche les informations de distribution des octets</summary>
			ShowBytesDistribution = 8,
			/// <summary>Le contenu est probablement du texte</summary>
			Text = 16,
		}

		private static void DumpHexaLine(StringBuilder sb, ReadOnlySpan<byte> bytes)
		{
			Contract.Debug.Requires(sb != null && bytes.Length <= 16);

			foreach (byte b in bytes)
			{
				sb.Append(' ').Append(b.ToString("X2"));
			}
			if (bytes.Length < 16) sb.Append(' ', (16 - bytes.Length) * 3);
		}

		private static void DumpRawLine(StringBuilder sb, ReadOnlySpan<byte> bytes)
		{
			Contract.Debug.Requires(sb != null && bytes.Length <= 16);

			foreach (byte b in bytes)
			{
				if (b == 0)
				{
					sb.Append('\u00B7'); // '·'
				}
				else if (b < 0x20)
				{ // C0 Controls
					if (b <= 9) sb.Append((char)(0x2080 + b)); // subscript '₁' to '₉'
					else sb.Append('\u02DA'); // '˚'
				}
				else
				{ // C1 Controls
					if (b <= 0x7E) sb.Append((char) b); // ASCII
					else if (b <= 0x9F | b == 0xAD | b == 0xA0) sb.Append('\u02DA'); // '˚'
					else if (b == 255) sb.Append('\uFB00'); // 'ﬀ'
					else sb.Append((char) b); // Latin-1
				}
			}
			if (bytes.Length < 16) sb.Append(' ', (16 - bytes.Length));
		}

		private static void DumpTextLine(StringBuilder sb, ReadOnlySpan<byte> bytes)
		{
			Contract.Debug.Requires(sb != null && bytes.Length <= 16);

			foreach (byte b in bytes)
			{
				if (b < 0x20)
				{ // C0 Controls
					if (b == 10) sb.Append('\u240A'); // '␊'
					else if (b == 13) sb.Append('\u240D'); // '␍'
					else sb.Append('\u00B7'); // '·'
				}
				else
				{ // C1 Controls
					if (b <= 0x7E) sb.Append((char)b); // ASCII
					else if (b <= 0x9F | b == 0xAD | b == 0xA0) sb.Append('\u00B7'); // '·'
					else sb.Append((char)b); // Latin-1
				}
			}
			if (bytes.Length < 16) sb.Append(' ', (16 - bytes.Length));
		}

		/// <summary>Dump un tableau de bytes en hexa décimal, formaté avec 16 octets par lignes</summary>
		public static string Format(byte[] bytes, Options options = Options.Default, int indent = 0)
		{
			Contract.NotNull(bytes);

			return Format(bytes.AsSlice(), options, indent);
		}

		/// <summary>Dump une séquence de bytes en hexa décimal, formaté avec 16 octets par lignes</summary>
		public static string Format(Slice bytes, Options options = Options.Default, int indent = 0)
		{
			return Format(bytes.Span, options, indent);
		}

		/// <summary>Dump une séquence de bytes en hexa décimal, formaté avec 16 octets par lignes</summary>
		public static string Format(ReadOnlySpan<byte> bytes, Options options = Options.Default, int indent = 0)
		{
			var sb = new StringBuilder();
			bool preview = (options & Options.NoPreview) == 0;

			string prefix = indent == 0 ? String.Empty : new string('\t', indent); // tabs ftw

			if ((options & Options.NoHeader) == 0)
			{
				sb.Append(prefix);
				sb.Append("HEXA : -0 -1 -2 -3 -4 -5 -6 -7 -8 -9 -A -B -C -D -E -F");
				if (preview) sb.Append(" : <---<---<---<--- :");
				sb.AppendLine();
			}

			int p = 0;
			int offset = 0;
			int count = bytes.Length;
			while (count > 0)
			{
				int n = Math.Min(count, 16);
				sb.Append(prefix);
				sb.Append(p.ToString("X4")).Append(" |");
				var chunk = bytes.Slice(offset, n);
				DumpHexaLine(sb, chunk);
				if (preview)
				{
					sb.Append(" | ");
					if ((options & Options.Text) == 0)
						DumpRawLine(sb, chunk);
					else
						DumpTextLine(sb, chunk);
					sb.Append(" |");
				}
				sb.AppendLine();
				count -= n;
				p += n;
				offset += n;
			}

			if ((options & Options.NoFooter) == 0)
			{
				sb.Append(prefix);
				sb.AppendFormat(
					CultureInfo.InvariantCulture,
					"---- : Size = {0:N0} / 0x{0:X} : Hash = 0x{1:X8}",
					bytes.Length,
					XxHash32.Compute(bytes)
				);
				sb.AppendLine();
			}
			if ((options & Options.ShowBytesDistribution) != 0)
			{
				sb.Append(prefix);
				sb.AppendFormat("---- [{0}]", ComputeBytesDistribution(bytes, 2));
				sb.AppendLine();
			}

			return sb.ToString();
		}

		/// <summary>Dump un comparatif de deux tableaux de bytes en hexa décimal, formaté en side-by-side avec 16 octets par lignes</summary>
		public static string Versus(byte[] left, byte[] right, Options options = Options.Default)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);
			return Versus(left.AsSpan(), right.AsSpan(), options);
		}

		/// <summary>Dump un comparatif de deux séquences de bytes en hexa décimal, formaté en side-by-side avec 16 octets par lignes</summary>
		public static string Versus(Slice left, Slice right, Options options = Options.Default)
		{
			return Versus(left.OrEmpty().Span, right.OrEmpty().Span);
		}

		/// <summary>Dump un comparatif de deux séquences de bytes en hexa décimal, formaté en side-by-side avec 16 octets par lignes</summary>
		public static string Versus(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, Options options = Options.Default)
		{
			var sb = new StringBuilder();

			bool preview = (options & Options.NoPreview) == 0;

			if ((options & Options.NoHeader) == 0)
			{
				sb.Append("HEXA : x0 x1 x2 x3 x4 x5 x6 x7 x8 x9 xA xB xC xD xE xF : x0 x1 x2 x3 x4 x5 x6 x7 x8 x9 xA xB xC xD xE xF");
				if (preview) sb.Append(" :      : 0123456789ABCDEF : 0123456789ABCDEF");
				sb.AppendLine();
			}

			int p = 0;
			int lr = left.Length;
			int rr = right.Length;
			int count = Math.Max(lr, rr);
			while (count > 0)
			{
				sb.Append((p >> 4).ToString("X3")).Append("x |");

				int ln = Math.Min(lr, 16);
				int rn = Math.Min(rr, 16);

				var lChunk = ln > 0 ? left.Slice(p, ln) : Span<byte>.Empty;
				var rChunk = rn > 0 ? right.Slice(p, rn) : Span<byte>.Empty;

				bool same = !preview || lChunk.SequenceEqual(rChunk);
				DumpHexaLine(sb, lChunk);
				sb.Append(" |");
				DumpHexaLine(sb, rChunk);

				if (preview)
				{
					sb.Append(" ║ ").Append((p >> 4).ToString("X3")).Append("x | ");
					DumpRawLine(sb, lChunk);
					sb.Append(" | ");
					DumpRawLine(sb, rChunk);
					if (!same) sb.Append(" *");
				}

				sb.AppendLine();
				lr -= ln;
				rr -= rn;
				count -= 16;
				p += 16;
			}

			if ((options & Options.NoHeader) == 0)
			{
				if ((options & Options.ShowBytesDistribution) == 0)
				{
					sb.AppendFormat(
						CultureInfo.InvariantCulture,
						"<<<< {0:N0} bytes; 0x{1:X8}",
						left.Length,
						XxHash32.Compute(left)
					);
					sb.AppendLine();
					sb.AppendFormat(
						CultureInfo.InvariantCulture,
						">>>> {0:N0} bytes; 0x{1:X8}",
						right.Length,
						XxHash32.Compute(right)
					);
					sb.AppendLine();
				}
				else
				{
					sb.AppendFormat(
						CultureInfo.InvariantCulture,
						"<<<< [{2}] {0:N0} bytes; 0x{1:X8}",
						left.Length,
						XxHash32.Compute(left),
						ComputeBytesDistribution(left, 1)
					);
					sb.AppendLine();
					sb.AppendFormat(
						CultureInfo.InvariantCulture,
						">>>> [{2}] {0:N0} bytes; 0x{1:X8}",
						right.Length,
						XxHash32.Compute(right),
						ComputeBytesDistribution(left, 1)
					);
					sb.AppendLine();
				}
			}

			return sb.ToString();
		}

		/// <summary>Génère une string ASCII avec la distribution des octets (0..255) dans un segment de données binaires</summary>
		/// <param name="bytes">Tableau contenant des octets à mapper</param>
		/// <param name="shrink">Shrink factor entre 0 et 8. La valeur des octets est divisées par 2^<paramref name="shrink"/> pour obtenir l'index du compteur correspondant</param>
		/// <returns>Chaîne ASCII (de taille 256 >> <paramref name="shrink"/>) avec la répartition des octets de <paramref name="bytes"/></returns>
		public static string ComputeBytesDistribution(byte[] bytes, int shrink = 0)
		{
			Contract.NotNull(bytes);
			return ComputeBytesDistribution(bytes.AsSpan(), shrink);
		}

		/// <summary>Génère une string ASCII avec la distribution des octets (0..255) dans un segment de données binaires</summary>
		/// <param name="bytes">Tableau contenant des octets à mapper</param>
		/// <param name="shrink">Shrink factor entre 0 et 8. La valeur des octets est divisées par 2^<paramref name="shrink"/> pour obtenir l'index du compteur correspondant</param>
		/// <returns>Chaîne ASCII (de taille 256 >> <paramref name="shrink"/>) avec la répartition des octets de <paramref name="bytes"/></returns>
		public static string ComputeBytesDistribution(Slice bytes, int shrink = 0)
		{
			return ComputeBytesDistribution(bytes.Span, shrink);
		}

		/// <summary>Génère une string ASCII avec la distribution des octets (0..255) dans un segment de données binaires</summary>
		/// <param name="bytes">Tableau contenant des octets à mapper</param>
		/// <param name="shrink">Shrink factor entre 0 et 8. La valeur des octets est divisées par 2^<paramref name="shrink"/> pour obtenir l'index du compteur correspondant</param>
		/// <returns>Chaîne ASCII (de taille 256 >> <paramref name="shrink"/>) avec la répartition des octets de <paramref name="bytes"/></returns>
		public static string ComputeBytesDistribution(ReadOnlySpan<byte> bytes, int shrink = 0)

		{
			if (shrink < 0 || shrink > 8) throw new ArgumentOutOfRangeException(nameof(shrink));

			/* trouvé sur le net: Jorn Barger's light value scale
					             Darker    .'`,^:";~    Lighter
					   bright    /|\      -_+<>i!lI?     /|\      dark
					  letters     |       /\|()1{}[]      |     letters
					     on               rcvunxzjft               on
					    dark      |       LCJUYXZO0Q      |      bright
					 background  \|/      oahkbdpqwm     \|/   background
					            Lighter   *WMB8&%$#@   Darker
			*/
#if DEBUG_ASCII_PALETTE
			var brush = "0123456789ABCDE".ToCharArray();
#else
			var brush = " .-:;~+=omMB$#@".ToCharArray();
			//note: tweaké pour que ca rende le mieux avec Consolas (VS output, notepad, ...)
#endif

			var sb = new StringBuilder();
			if (bytes.Length > 0)
			{
				int[] counters = new int[256 >> shrink];
				foreach(var b in bytes)
				{
					++counters[b >> shrink];
				}
				int[] cpy = counters.Where(c => c > 0).ToArray();
				Array.Sort(cpy);
				int max = cpy[^1];
				int half = cpy.Length >> 1;
				int med = cpy[half];
				if (cpy.Length % 2 == 1)
				{
					med = cpy.Length == 1 ? cpy[0] : (med + cpy[half + 1]) / 2;
				}

				foreach (var c in counters)
				{
					if (c == 0)
					{
						sb.Append(brush[0]);
					}
					else if (c == max)
					{
						sb.Append(brush[14]);
					}
					else if (c >= med)
					{ // 8..15
						double p = (c - med) * 6.5 / (max - med);
						sb.Append(brush[(int) Math.Round(p + 7, MidpointRounding.AwayFromZero)]);
					}
					else
					{ // 0..7
						double p = (c * 6.5) / med;
						sb.Append(brush[(int) Math.Round(p + 0.5, MidpointRounding.AwayFromZero)]);
					}
				}
			}
			return sb.ToString();
		}

	}

}
