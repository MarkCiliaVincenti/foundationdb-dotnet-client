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

#define USE_FAST_STRING_ALLOCATOR

namespace Doxense.Serialization
{
	using System.Buffers;
	using System.Buffers.Text;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Memory;

	[PublicAPI]
	public static class Base64Encoding
	{
		// Encodage similaire à Base64, mais qui remplace '+' et '/' par '-' et '_', avec padding optionnel où '=' est remplacé par '*'
		// => peut être inséré dans une URL sans nécessiter d'encodage spécifique

		private const int CHARMAP_PAGE_SIZE = 256;

		#region Encoding Maps...

		private const char Base64PadChar = '=';

		private static readonly char[] Base64CharMap =
		[
			// HI
			'A', 'A', 'A', 'A', 'B', 'B', 'B', 'B', 'C', 'C',
			'C', 'C', 'D', 'D', 'D', 'D', 'E', 'E', 'E', 'E',
			'F', 'F', 'F', 'F', 'G', 'G', 'G', 'G', 'H', 'H',
			'H', 'H', 'I', 'I', 'I', 'I', 'J', 'J', 'J', 'J',
			'K', 'K', 'K', 'K', 'L', 'L', 'L', 'L', 'M', 'M',
			'M', 'M', 'N', 'N', 'N', 'N', 'O', 'O', 'O', 'O',
			'P', 'P', 'P', 'P', 'Q', 'Q', 'Q', 'Q', 'R', 'R',
			'R', 'R', 'S', 'S', 'S', 'S', 'T', 'T', 'T', 'T',
			'U', 'U', 'U', 'U', 'V', 'V', 'V', 'V', 'W', 'W',
			'W', 'W', 'X', 'X', 'X', 'X', 'Y', 'Y', 'Y', 'Y',
			'Z', 'Z', 'Z', 'Z', 'a', 'a', 'a', 'a', 'b', 'b',
			'b', 'b', 'c', 'c', 'c', 'c', 'd', 'd', 'd', 'd',
			'e', 'e', 'e', 'e', 'f', 'f', 'f', 'f', 'g', 'g',
			'g', 'g', 'h', 'h', 'h', 'h', 'i', 'i', 'i', 'i',
			'j', 'j', 'j', 'j', 'k', 'k', 'k', 'k', 'l', 'l',
			'l', 'l', 'm', 'm', 'm', 'm', 'n', 'n', 'n', 'n',
			'o', 'o', 'o', 'o', 'p', 'p', 'p', 'p', 'q', 'q',
			'q', 'q', 'r', 'r', 'r', 'r', 's', 's', 's', 's',
			't', 't', 't', 't', 'u', 'u', 'u', 'u', 'v', 'v',
			'v', 'v', 'w', 'w', 'w', 'w', 'x', 'x', 'x', 'x',
			'y', 'y', 'y', 'y', 'z', 'z', 'z', 'z', '0', '0',
			'0', '0', '1', '1', '1', '1', '2', '2', '2', '2',
			'3', '3', '3', '3', '4', '4', '4', '4', '5', '5',
			'5', '5', '6', '6', '6', '6', '7', '7', '7', '7',
			'8', '8', '8', '8', '9', '9', '9', '9', '+', '+',
			'+', '+', '/', '/', '/', '/',
			// LO
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
			'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
			'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
			'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
			'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
			'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', '+', '/', 'A', 'B', 'C', 'D', 'E', 'F',
			'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
			'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
			'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3',
			'4', '5', '6', '7', '8', '9', '+', '/', 'A', 'B',
			'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
			'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
			'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
			'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			'+', '/', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
			'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
			'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b',
			'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
			'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5',
			'6', '7', '8', '9', '+', '/'
		];

		private const char Base64UrlPadChar = '*';

		private static readonly char[] Base64UrlCharMap =
		[
			// HI
			'A', 'A', 'A', 'A', 'B', 'B', 'B', 'B', 'C', 'C',
			'C', 'C', 'D', 'D', 'D', 'D', 'E', 'E', 'E', 'E',
			'F', 'F', 'F', 'F', 'G', 'G', 'G', 'G', 'H', 'H',
			'H', 'H', 'I', 'I', 'I', 'I', 'J', 'J', 'J', 'J',
			'K', 'K', 'K', 'K', 'L', 'L', 'L', 'L', 'M', 'M',
			'M', 'M', 'N', 'N', 'N', 'N', 'O', 'O', 'O', 'O',
			'P', 'P', 'P', 'P', 'Q', 'Q', 'Q', 'Q', 'R', 'R',
			'R', 'R', 'S', 'S', 'S', 'S', 'T', 'T', 'T', 'T',
			'U', 'U', 'U', 'U', 'V', 'V', 'V', 'V', 'W', 'W',
			'W', 'W', 'X', 'X', 'X', 'X', 'Y', 'Y', 'Y', 'Y',
			'Z', 'Z', 'Z', 'Z', 'a', 'a', 'a', 'a', 'b', 'b',
			'b', 'b', 'c', 'c', 'c', 'c', 'd', 'd', 'd', 'd',
			'e', 'e', 'e', 'e', 'f', 'f', 'f', 'f', 'g', 'g',
			'g', 'g', 'h', 'h', 'h', 'h', 'i', 'i', 'i', 'i',
			'j', 'j', 'j', 'j', 'k', 'k', 'k', 'k', 'l', 'l',
			'l', 'l', 'm', 'm', 'm', 'm', 'n', 'n', 'n', 'n',
			'o', 'o', 'o', 'o', 'p', 'p', 'p', 'p', 'q', 'q',
			'q', 'q', 'r', 'r', 'r', 'r', 's', 's', 's', 's',
			't', 't', 't', 't', 'u', 'u', 'u', 'u', 'v', 'v',
			'v', 'v', 'w', 'w', 'w', 'w', 'x', 'x', 'x', 'x',
			'y', 'y', 'y', 'y', 'z', 'z', 'z', 'z', '0', '0',
			'0', '0', '1', '1', '1', '1', '2', '2', '2', '2',
			'3', '3', '3', '3', '4', '4', '4', '4', '5', '5',
			'5', '5', '6', '6', '6', '6', '7', '7', '7', '7',
			'8', '8', '8', '8', '9', '9', '9', '9', '-', '-',
			'-', '-', '_', '_', '_', '_',
			// LO
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
			'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
			'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
			'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
			'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
			'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', '-', '_', 'A', 'B', 'C', 'D', 'E', 'F',
			'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
			'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
			'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3',
			'4', '5', '6', '7', '8', '9', '-', '_', 'A', 'B',
			'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
			'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
			'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
			'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			'-', '_', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
			'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
			'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b',
			'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
			'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5',
			'6', '7', '8', '9', '-', '_'
		];

		#endregion

		#region Base64 Generic

		public static string ToBase64String(byte[] buffer)
		{
			Contract.NotNull(buffer);
			return EncodeBuffer(buffer.AsSpan(), padded:true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64String(Slice buffer)
		{
			return EncodeBuffer(buffer.Span, padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64String(ReadOnlySpan<byte> buffer)
		{
			return EncodeBuffer(buffer, padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64String(string s)
		{
			Contract.NotNull(s);

			return DecodeBuffer(s.AsSpan(), padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64String(ReadOnlySpan<char> s)
		{
			return DecodeBuffer(s, padded: true, urlSafe: false);
		}

		#endregion

		#region Base64Url (WebSafe)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(byte[] buffer)
		{
			Contract.NotNull(buffer);
			return EncodeBuffer(buffer.AsSpan(), padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(ReadOnlySpan<byte> buffer)
		{
			return EncodeBuffer(buffer, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(Slice buffer)
		{
			return EncodeBuffer(buffer.Span, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64UrlString(string s)
		{
			Contract.NotNull(s);
			return DecodeBuffer(s.AsSpan(), padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64UrlString(ReadOnlySpan<char> s)
		{
			return DecodeBuffer(s, padded: false, urlSafe: true);
		}

		#endregion

		#region Common...

		/// <summary>Calcule le nombre exacte de caractères nécessaire pour encoder un buffer en Base64</summary>
		/// <param name="length">Nombre d'octets source à encoder</param>
		/// <param name="padded">True s'il y a du padding (résultat multiple de 4).</param>
		/// <returns>Nombre de caractères nécessaire pour encoder un buffer de taille <paramref name="length"/>, avec ou sans padding</returns>
		[Pure]
		public static int GetCharsCount(int length, bool padded)
		{
			if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Buffer length cannot be negative.");
			if (padded)
			{ // 3 octets source sont encodés en 4 chars, arrondi au supérieur
				return checked((length + 2) / 3 * 4);
			}
			else
			{
				int chunks = checked((length / 3) * 4);
				switch (length % 3)
				{
					case 0: return chunks;
					case 1: return checked(chunks + 2);
					default: return checked(chunks + 3);
				}
			}
		}

		[Pure]
		public static int EstimateBytesCount(int length)
		{
			// 4 chars décodés en 3 bytes, arrondi à l'inférieur
			return checked(length / 4 * 3 + 2);
		}

		[Pure]
		public static string EncodeBuffer(ReadOnlySpan<byte> buffer, bool padded = true, bool urlSafe = false)
		{
			if (buffer.Length == 0) return string.Empty;

			// note: cette version est 20-25% plus rapide que Convert.ToBase64String() sur ma machine.
			// La version de la BCL "triche" en pré-allouant la string, et en écrivant directement dedans.
			// => j'ai essayé d'appeler FastAllocateString() directement, et je passe a 35% plus rapide...

			if (padded && !urlSafe)
			{
				return Convert.ToBase64String(buffer);
			}
#if NET9_0_OR_GREATER
			if (urlSafe && !padded)
			{
				return Base64Url.EncodeToString(buffer);
			}
#endif

			// détermine la taille exacte du résultat (incluant le padding si présent)
			int size = GetCharsCount(buffer.Length, padded);
			return EncodeBufferUnsafe(buffer, size, padded, urlSafe);
		}

		[Pure]
		public static string EncodeBufferUnsafe(ReadOnlySpan<byte> source, bool padded = true, bool urlSafe = false)
		{
			if (source.Length == 0) return string.Empty;

			// détermine la taille exacte du résultat (incluant le padding si présent)
			int size = GetCharsCount(source.Length, padded);

			return EncodeBufferUnsafe(source, size, padded, urlSafe);
		}

		/// <summary>Encode un buffer en base64</summary>
		/// <param name="source">Buffer source</param>
		/// <param name="charCount">Taille du résultat (précalculée via <see cref="GetCharsCount"/>)</param>
		/// <param name="padded">Si true, pad la fin si pas multiple de 3</param>
		/// <param name="urlSafe">Si true, utilise le charset web safe</param>
		/// <returns></returns>
		[Pure]
		private static string EncodeBufferUnsafe(ReadOnlySpan<byte> source, int charCount, bool padded, bool urlSafe)
		{
			Contract.Debug.Requires(charCount >= 0);

			char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';
			var charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;

#if USE_FAST_STRING_ALLOCATOR
			// alloue directement la string, avec la bonne taille

			var s = new string('\0', charCount);
			var b = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(s.AsSpan()), charCount);
			// on va écrire directement dans la string (!!)
			EncodeBufferUnsafe(b, source, charMap, padChar);
			return s;
#else
			// arrondi a 8 supérieur, pour garder un alignement correct sur la stack
			size = (size + 7) & (~0x7);

			if (size <= MAX_INLINE_SIZE)
			{ // Allocate on the stack
				char* output = stackalloc char[size];
				int n = EncodeBufferUnsafe(output, size, buffer, count, ref charMap[0], padChar);
				return new string(output, 0, n);
			}
			else
			{ // Allocate on the heap
				char[] output = new char[size];
				fixed (char* pOut = output)
				{
					int n = EncodeBufferUnsafe(pOut, size, buffer, count, ref charMap[0], padChar);
					return new string(output, 0, n);
				}
			}
#endif
		}

		/// <summary>Ecrit un buffer de taille quelconque dans un TextWriter, encodé en Base64</summary>
		/// <param name="output">Writer où écrire le texte encodé en Base64</param>
		/// <param name="buffer">Buffer source contenant les données à encoder</param>
		/// <param name="padded">Ajoute des caractères de padding (true) ou non (false). Le caractère de padding utilisé dépend de la valeur de <paramref name="urlSafe"/>.</param>
		/// <param name="urlSafe">Si true, remplace les caractères 63 et 63 par des versions Url Safe ('-' et '_'). Sinon, utilise les caractères classiques ('+' et '/')</param>
		/// <remarks>Si <paramref name="padded"/> est true, le nombre de caractères écrit sera toujours un multiple de 4.</remarks>
		public static void EncodeTo(TextWriter output, Slice buffer, bool padded = true, bool urlSafe = false)
		{
			EncodeTo(output, buffer.Span, padded, urlSafe);
		}

		/// <summary>Ecrit un buffer de taille quelconque dans un TextWriter, encodé en Base64</summary>
		/// <param name="output">Writer où écrire le texte encodé en Base64</param>
		/// <param name="source">Buffer source contenant les données à encoder</param>
		/// <param name="padded">Ajoute des caractères de padding (true) ou non (false). Le caractère de padding utilisé dépend de la valeur de <paramref name="urlSafe"/>.</param>
		/// <param name="urlSafe">Si true, remplace les caractères 63 et 63 par des versions Url Safe ('-' et '_'). Sinon, utilise les caractères classiques ('+' et '/')</param>
		/// <remarks>Si <paramref name="padded"/> est true, le nombre de caractères écrit sera toujours un multiple de 4.</remarks>
		public static void EncodeTo(TextWriter output, ReadOnlySpan<byte> source, bool padded = true, bool urlSafe = false)
		{
			Contract.NotNull(output);


			if (source.Length == 0) return;

			int size = GetCharsCount(source.Length, padded);
			char padChar = !padded ? '\0' : urlSafe ? Base64UrlPadChar : Base64PadChar;
			var charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;

			//TODO: if StringWriter, extraire le StringBuilder, et faire une version qui écrit directement dedans !

			// pour générer 4096 chars, il faut 3 072 bytes
			// pour générer 65536 chars, il faut 49 152 bytes
			const int CHARSIZE = 65536;
			const int BYTESIZE = (CHARSIZE / 4) * 3;

			int bufferSize = Math.Min(size, CHARSIZE);
			bufferSize = (bufferSize + 7) & (~0x7); // arrondi a 8 supérieur, pour garder un alignement correct
			var chars = ArrayPool<char>.Shared.Rent(bufferSize);

			var remaining = source;
			while (remaining.Length > 0)
			{
				int sz = Math.Min(remaining.Length, BYTESIZE);
				int n = EncodeBufferUnsafe(chars, remaining[..sz], charMap, remaining.Length > BYTESIZE ? '\0' : padChar);
				Contract.Debug.Assert(n <= bufferSize);
				if (n <= 0) break; //REVIEW: dans quel cas on peut avoir <= 0 ???
				output.Write(chars, 0, n);
				remaining = remaining[sz..];
			}
			ArrayPool<char>.Shared.Return(chars);
			if (remaining.Length > 0)
			{
				throw new InvalidOperationException(); // ??
			}
		}

		#endregion

		#region Internal Helpers...

		/// <summary>Encode un segment de données vers un buffer</summary>
		/// <param name="output">Buffer où écrire le texte encodée. La capacité doit être suffisante!</param>
		/// <param name="source">Pointeur vers le début octets à encoder.</param>
		/// <param name="charMap">Pointeur vers la map de conversion Base64 à utiliser (2 x 256 chars)</param>
		/// <param name="padChar">Caractère de padding utilisé, ou '\0' si pas de padding</param>
		/// <returns>Nombre de caractères écrits dans <paramref name="output"/>.</returns>
		internal static int EncodeBufferUnsafe(Span<char> output, ReadOnlySpan<byte> source, ReadOnlySpan<char> charMap, char padChar)
		{
			// portage en C# de modp_b64.c: https://code.google.com/p/stringencoders/source/browse/trunk/src/modp_b64.c

			ref byte inp = ref MemoryMarshal.GetReference(source);

			ref char e0 = ref MemoryMarshal.GetReference(charMap);
			ref char e1 = ref Unsafe.Add(ref e0, CHARMAP_PAGE_SIZE);
			//note: e2 == e1 ?

			// vérifie que le buffer peut contenir au moins les chunks complets
			ref char start = ref MemoryMarshal.GetReference(output);
			ref char stop = ref Unsafe.Add(ref start, output.Length);

			int len = source.Length;
			if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref start, ((source.Length / 3) * 4)), ref stop))
			{
				ThrowOutputBufferTooSmall();
			}

			ref char outp = ref start;
			int i, end;
			uint t1, t2, t3;

			// 4 bytes chunks...
			for (i = 0, end = len - 2; i < end; i += 3)
			{
				t1 = Unsafe.Add(ref inp, 0);
				t2 = Unsafe.Add(ref inp, 1);
				t3 = Unsafe.Add(ref inp, 2);
				Unsafe.Add(ref outp, 0) = Unsafe.Add(ref e0, t1);
				Unsafe.Add(ref outp, 1) = Unsafe.Add(ref e1, ((t1 & 0x03) << 4) | ((t2 >> 4) & 0x0F));
				Unsafe.Add(ref outp, 2) = Unsafe.Add(ref e1, ((t2 & 0x0F) << 2) | ((t3 >> 6) & 0x03));
				Unsafe.Add(ref outp, 3) = Unsafe.Add(ref e1, t3);
				inp = ref Unsafe.Add(ref inp, 3);
				outp = ref Unsafe.Add(ref outp, 4);
			}

			// remainder...
			switch (len - i)
			{
				case 0:
				{
					break;
				}
				case 1:
				{
					if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp,  2), ref stop))
					{
						ThrowOutputBufferTooSmall();
					}

					t1 = inp;
					Unsafe.Add(ref outp, 0) = Unsafe.Add(ref e0, t1);
					Unsafe.Add(ref outp, 1) = Unsafe.Add(ref e1, (t1 & 0x03) << 4);
					outp = ref Unsafe.Add(ref outp, 2);

					if (padChar != '\0')
					{
						if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp, 2), ref stop))
						{
							ThrowOutputBufferTooSmall();
						}
						Unsafe.Add(ref outp, 0) = padChar;
						Unsafe.Add(ref outp, 1) = padChar;
						outp = ref Unsafe.Add(ref outp, 2);
					}

					break;
				}
				default:
				{
					t1 = inp;
					t2 = Unsafe.Add(ref inp, 1);
					if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp, 3), ref stop))
					{
						ThrowOutputBufferTooSmall();
					}
					Unsafe.Add(ref outp, 0) = Unsafe.Add(ref e0, t1);
					Unsafe.Add(ref outp, 1) = Unsafe.Add(ref e1, ((t1 & 0x03) << 4) | ((t2 >> 4) & 0x0F));
					Unsafe.Add(ref outp, 2) = Unsafe.Add(ref e1, (t2 & 0x0F) << 2);
					outp = ref Unsafe.Add(ref outp, 3);

					if (padChar != '\0')
					{
						if (Unsafe.IsAddressGreaterThan(ref Unsafe.Add(ref outp, 1), ref stop))
						{
							ThrowOutputBufferTooSmall();
						}
						outp = padChar;
						outp = ref Unsafe.Add(ref outp, 1);
					}

					break;
				}
			}

			Contract.Debug.Ensures(!Unsafe.IsAddressLessThan(ref inp, ref MemoryMarshal.GetReference(source)) && !Unsafe.IsAddressGreaterThan(ref inp, ref Unsafe.Add(ref MemoryMarshal.GetReference(source), source.Length)));
			Contract.Debug.Ensures(!Unsafe.IsAddressLessThan(ref outp, ref MemoryMarshal.GetReference(output)) && !Unsafe.IsAddressGreaterThan(ref outp, ref stop));
			return (int) (Unsafe.ByteOffset(ref output[0], ref outp).ToInt64() / Unsafe.SizeOf<char>());
		}

		[ContractAnnotation("buffer:null => halt")]
		private static void EnsureBufferIsValid(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer);
			if (offset < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
			if (count < 0 || offset + count > buffer.Length) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
		}

		[ContractAnnotation("=> halt")]
		private static void ThrowOutputBufferTooSmall()
		{
			throw new InvalidOperationException("The output buffer is too small");
		}

#if USE_FAST_STRING_ALLOCATOR

		private static readonly Func<int, string> s_fastStringAllocator = GetFastStringAllocator();

		private static Func<int, string> GetFastStringAllocator()
		{
			var method = typeof(string).GetMethod("FastAllocateString", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			if (method != null)
			{
				return (Func<int, string>) method.CreateDelegate(typeof(Func<int, string>));
			}
			else
			{
#if DEBUG
				// Si vous arrivez ici, c'est que la méthode String.FastAllocateString() n'existe plus (nouvelle version de .NET Framework?)
				// => essayez de trouver une solution alternative pour pré-allouer une string à la bonne taille
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				return (count) => new string('\0', count);
			}
		}

#endif

		/// <summary>Détermine la taille de buffer nécessaire pour encoder un segment de text en Base64</summary>
		/// <param name="s">Segment de texte</param>
		/// <param name="padChar">Padding character (optionnel)</param>
		public static int GetBytesCount(ReadOnlySpan<char> s, char padChar = '\0')
		{
			unsafe
			{
				fixed (char* chars = &MemoryMarshal.GetReference(s))
				{
					return GetBytesCount(chars, s.Length, padChar);
				}
			}
		}

		/// <summary>Détermine la taille de buffer nécessaire pour encoder un segment de text en Base64</summary>
		/// <param name="src">Pointeur vers le début du segment de texte</param>
		/// <param name="len">Taille du segment (en caractères)</param>
		/// <param name="padChar">Padding character (optionnel)</param>
		public static unsafe int GetBytesCount(char* src, int len, char padChar)
		{
			Contract.Debug.Requires(len >= 0 && (len == 0 || src != null));
			if (len == 0) return 0;

			int padding;
			if (padChar != '\0')
			{
				padding = 0;
				while (len > 0)
				{
					if (src[len - 1] != padChar) break;
					++padding;
					--len;
				}
				if (padding != 0)
				{
					if (padding == 1) { padding = 2; }
					else if (padding == 2) { padding = 1; }
					else
					{
						throw new FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.");
					}
				}
			}
			else
			{
				switch(len % 4)
				{
					case 0: padding = 0; break;
					case 1: case 2: padding = 1; break;
					default: padding = 2; break;
				}
			}
			return (len / 4) * 3 + padding;
		}

		[Pure]
		public static byte[] DecodeBuffer(ReadOnlySpan<char> encoded, bool padded = true, bool urlSafe = false)
		{
			if (encoded.Length == 0) return [ ];

			unsafe
			{
				char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';

				fixed (char* pSrc = &MemoryMarshal.GetReference(encoded))
				{
					int size = GetBytesCount(pSrc, encoded.Length, padChar); //TODO: padding + urlSafe ?
					var dest = new byte[size];

					fixed (byte* pDst = dest)
					{
						int p = DecodeBufferUnsafe(pDst, pSrc, encoded.Length, padChar);
						if (p < 0) throw new FormatException("Malformed base64 string");
						return dest;
					}
				}
			}
		}

		[Pure]
		public static Slice DecodeBuffer(ReadOnlySpan<char> encoded, ref byte[]? buffer, bool padded = true, bool urlSafe = false)
		{
			if (encoded.Length == 0) return Slice.Empty;

			unsafe
			{
				char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';

				fixed (char* pSrc = &MemoryMarshal.GetReference(encoded))
				{
					int size = GetBytesCount(pSrc, encoded.Length, padChar); //TODO: padding + urlSafe ?

					var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, size);

					fixed (byte* pDst = &tmp[0])
					{
						int p = DecodeBufferUnsafe(pDst, pSrc, encoded.Length, padChar);
						if (p < 0) throw new FormatException("Malformed base64 string");
						return new Slice(tmp, 0, p);
					}
				}
			}
		}

		internal static unsafe int DecodeBufferUnsafe(byte* dest, char* src, int len, char padChar)
		{
			if (len == 0) return 0;
			Contract.PointerNotNull(dest);
			Contract.PointerNotNull(src);

			if (padChar != '\0')
			{
				if (len < 4 || len % 4 != 0)
				{ // doit être un multiple de 4
					throw new FormatException("Invalid padding");
				}
				// ne doit pas y avoir plus de 2 pad chars a la fin
				if (src[len - 1] == padChar)
				{
					--len;
					if (src[len - 1] == padChar)
					{
						--len;
					}
				}
			}

			int leftOver = len % 4;
			int chunks = leftOver == 0 ? (len / 4 - 1) : (len / 4);

			byte* p = dest;
			uint x;
			ulong* srcInt = (ulong*)src;
			ulong y = *srcInt++;

			uint[] d0 = DecodeMap0;
			uint[] d1 = DecodeMap1;
			uint[] d2 = DecodeMap2;
			uint[] d3 = DecodeMap3;

			for (int i = 0; i < chunks; ++i)
			{
				x = d0[y & 0xff]
				  | d1[(y >> 16) & 0xff]
				  | d2[(y >> 32) & 0xff]
				  | d3[(y >> 48) & 0xff];

				if (x >= BADCHAR) return -1;
				*((uint*)p) = x;
				p += 3;
				y = *srcInt++;
			}

			switch(leftOver)
			{
				case 0:
				{
					x = d0[y & 0xff]
					  | d1[(y >> 16) & 0xff]
					  | d2[(y >> 32) & 0xff]
					  | d3[(y >> 48) & 0xff];

					if (x >= BADCHAR) return -1;

					p[0] = (byte)x;
					p[1] = (byte)(x >> 8);
					p[2] = (byte)(x >> 16);

					return (chunks + 1) * 3;
				}

				case 1:
				{ // 1 output byte
					if (padChar != '\0') return -1; //impossible avec du padding
					x = d0[y & 0xFF];
					*p = (byte)x;
					break;
				}
				case 2:
				{ // 1 output byte
					x = d0[y & 0xFF]
					  | d1[(y >> 16) & 0xFF];
					*p = (byte)x;
					break;
				}
				default:
				{ // 2 output byte
					x = d0[y & 0xFF]
					  | d1[(y >> 16) & 0xFF]
					  | d2[(y >> 32) & 0xFF];
					p[0] = (byte)x;
					p[1] = (byte)(x >> 8);
					break;
				}
			}

			if (x >= BADCHAR) return -1;
			return (3 * chunks) + ((6 * leftOver) / 8);

		}

		#endregion

		#region Data...

		private const uint BADCHAR = 0x01FFFFFF;

		private static readonly uint[] DecodeMap0 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x000000f8, 0x01ffffff, 0x000000f8, 0x01ffffff, 0x000000fc,
			0x000000d0, 0x000000d4, 0x000000d8, 0x000000dc, 0x000000e0, 0x000000e4,
			0x000000e8, 0x000000ec, 0x000000f0, 0x000000f4, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00000004, 0x00000008, 0x0000000c, 0x00000010, 0x00000014, 0x00000018,
			0x0000001c, 0x00000020, 0x00000024, 0x00000028, 0x0000002c, 0x00000030,
			0x00000034, 0x00000038, 0x0000003c, 0x00000040, 0x00000044, 0x00000048,
			0x0000004c, 0x00000050, 0x00000054, 0x00000058, 0x0000005c, 0x00000060,
			0x00000064, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x000000fc,
			0x01ffffff, 0x00000068, 0x0000006c, 0x00000070, 0x00000074, 0x00000078,
			0x0000007c, 0x00000080, 0x00000084, 0x00000088, 0x0000008c, 0x00000090,
			0x00000094, 0x00000098, 0x0000009c, 0x000000a0, 0x000000a4, 0x000000a8,
			0x000000ac, 0x000000b0, 0x000000b4, 0x000000b8, 0x000000bc, 0x000000c0,
			0x000000c4, 0x000000c8, 0x000000cc, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];

		private static readonly uint[] DecodeMap1 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x0000e003, 0x01ffffff, 0x0000e003, 0x01ffffff, 0x0000f003,
			0x00004003, 0x00005003, 0x00006003, 0x00007003, 0x00008003, 0x00009003,
			0x0000a003, 0x0000b003, 0x0000c003, 0x0000d003, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00001000, 0x00002000, 0x00003000, 0x00004000, 0x00005000, 0x00006000,
			0x00007000, 0x00008000, 0x00009000, 0x0000a000, 0x0000b000, 0x0000c000,
			0x0000d000, 0x0000e000, 0x0000f000, 0x00000001, 0x00001001, 0x00002001,
			0x00003001, 0x00004001, 0x00005001, 0x00006001, 0x00007001, 0x00008001,
			0x00009001, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x0000f003,
			0x01ffffff, 0x0000a001, 0x0000b001, 0x0000c001, 0x0000d001, 0x0000e001,
			0x0000f001, 0x00000002, 0x00001002, 0x00002002, 0x00003002, 0x00004002,
			0x00005002, 0x00006002, 0x00007002, 0x00008002, 0x00009002, 0x0000a002,
			0x0000b002, 0x0000c002, 0x0000d002, 0x0000e002, 0x0000f002, 0x00000003,
			0x00001003, 0x00002003, 0x00003003, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];


		private static readonly uint[] DecodeMap2 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x00800f00, 0x01ffffff, 0x00800f00, 0x01ffffff, 0x00c00f00,
			0x00000d00, 0x00400d00, 0x00800d00, 0x00c00d00, 0x00000e00, 0x00400e00,
			0x00800e00, 0x00c00e00, 0x00000f00, 0x00400f00, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00400000, 0x00800000, 0x00c00000, 0x00000100, 0x00400100, 0x00800100,
			0x00c00100, 0x00000200, 0x00400200, 0x00800200, 0x00c00200, 0x00000300,
			0x00400300, 0x00800300, 0x00c00300, 0x00000400, 0x00400400, 0x00800400,
			0x00c00400, 0x00000500, 0x00400500, 0x00800500, 0x00c00500, 0x00000600,
			0x00400600, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00c00f00,
			0x01ffffff, 0x00800600, 0x00c00600, 0x00000700, 0x00400700, 0x00800700,
			0x00c00700, 0x00000800, 0x00400800, 0x00800800, 0x00c00800, 0x00000900,
			0x00400900, 0x00800900, 0x00c00900, 0x00000a00, 0x00400a00, 0x00800a00,
			0x00c00a00, 0x00000b00, 0x00400b00, 0x00800b00, 0x00c00b00, 0x00000c00,
			0x00400c00, 0x00800c00, 0x00c00c00, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];

		private static readonly uint[] DecodeMap3 =
		[
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x003e0000, 0x01ffffff, 0x003e0000, 0x01ffffff, 0x003f0000,
			0x00340000, 0x00350000, 0x00360000, 0x00370000, 0x00380000, 0x00390000,
			0x003a0000, 0x003b0000, 0x003c0000, 0x003d0000, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00010000, 0x00020000, 0x00030000, 0x00040000, 0x00050000, 0x00060000,
			0x00070000, 0x00080000, 0x00090000, 0x000a0000, 0x000b0000, 0x000c0000,
			0x000d0000, 0x000e0000, 0x000f0000, 0x00100000, 0x00110000, 0x00120000,
			0x00130000, 0x00140000, 0x00150000, 0x00160000, 0x00170000, 0x00180000,
			0x00190000, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x003f0000,
			0x01ffffff, 0x001a0000, 0x001b0000, 0x001c0000, 0x001d0000, 0x001e0000,
			0x001f0000, 0x00200000, 0x00210000, 0x00220000, 0x00230000, 0x00240000,
			0x00250000, 0x00260000, 0x00270000, 0x00280000, 0x00290000, 0x002a0000,
			0x002b0000, 0x002c0000, 0x002d0000, 0x002e0000, 0x002f0000, 0x00300000,
			0x00310000, 0x00320000, 0x00330000, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		];
		#endregion

	}

}
