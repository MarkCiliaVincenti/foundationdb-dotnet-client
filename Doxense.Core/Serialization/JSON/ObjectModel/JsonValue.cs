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

namespace Doxense.Serialization.Json
{
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Memory;

	[Serializable]
	[DebuggerNonUserCode]
	[PublicAPI]
	[JetBrains.Annotations.CannotApplyEqualityOperator]
	public abstract partial class JsonValue : IEquatable<JsonValue>, IComparable<JsonValue>, IJsonSerializable, IFormattable, ISliceSerializable
#pragma warning disable CS0618 // Type or member is obsolete
		, IJsonDynamic
#pragma warning restore CS0618 // Type or member is obsolete
	{
		/// <summary>Type du token JSON</summary>
		public abstract JsonType Type { [Pure] get; }

		/// <summary>Conversion en object CLR (type automatique)</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public abstract object? ToObject();

		/// <summary>Bind this value into an instance of the specified <paramref name="type"/></summary>
		/// <param name="type">Target managed type</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>An instance of the target type that is equivalent to the original JSON value, if there exists a valid conversion path or convention. Otherwise, an exception will be thrown.</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound into an instance of the target <paramref name="type"/>.</exception>
		/// <example><c>JsonNumber.Return(123).Bind(typeof(long))</c> will return a boxed Int64 with value <c>123</c>.</example>
		/// <remarks>If the target type is a Value Type, the instance will be boxed, which may cause extra memory allocations. Consider calling <see cref="Bind{TValue}"/> instance, or use any of the convenience methods like <see cref="JsonValueExtensions.Required{TValue}"/>, <see cref="JsonValueExtensions.As{TValue}(Doxense.Serialization.Json.JsonValue?,Doxense.Serialization.Json.ICrystalJsonTypeResolver?)"/>, ...</remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public abstract object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Bind this value into an instance of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Target managed type</typeparam>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>An instance of the type <typeparamref name="TValue"/> that is equivalent to the original JSON value, if there exists a valid convertion path or convention. Otherwise, an exception will be thrown.</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound into an instance of the target type <typeparamref name="TValue"/>.</exception>
		/// <example><c>JsonNumber.Return(123).Bind&lt;long>()</c> will return the value <c>123</c>.</example>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual TValue? Bind<TValue>(ICrystalJsonTypeResolver? resolver = null) => (TValue?) Bind(typeof(TValue), resolver);

		/// <summary>Tests if this value is null or missing</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual bool IsNull { [Pure] get => false; }

		/// <summary>Tests if this value corresponds to the logical default for this type (0, false, null or missing, ...)</summary>
		/// <returns><see langword="true"/> for values like 0, false, or null; or <see langword="false"/> for non-zero integers, true, strings, arrays and objects</returns>
		/// <remarks>The empty string, array and object are NOT considered to be the default value of their type!</remarks>
		/// <example>
		/// <c>JsonNumber.Zero.IsDefault == true</c>,
		/// <c>JsonNumber.Return(123).IsDefault == false</c>,
		/// <c>JsonString.Return("").IsDefault == false</c>,
		/// <c>new JsonArray().IsDefault == false</c>,
		/// <c>new JsonObject().IsDefault == false</c>
		/// </example>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract bool IsDefault { [Pure] get; }

		/// <summary>Returns <see langword="true"/> if this value is read-only, and cannot be modified, or <see langword="false"/> if it allows mutations.</summary>
		/// <remarks>
		/// <para>Only JSON Objects and Arrays can return <see langword="false"/>. All other "value types" (string, boolean, numbers, ...) are always immutable, and will always be read-only.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract bool IsReadOnly { [Pure] get; }

		/// <summary>Prevents any future mutations of this JSON value (of type Object or Array), by recursively freezing it and all of its children.</summary>
		/// <returns>The same instance, which is now converted to a read-only instance.</returns>
		/// <remarks>
		/// <para>Any future attempt to modify this object, or any of its children that was previously mutable, will fail.</para>
		/// <para>If this instance is already read-only, this will be a no-op.</para>
		/// <para>This should only be used with care, and only on objects that are entirely owned by the called, or that have not been published yet. Freezing a shared mutable object may cause issues for other threads that still were expecting a mutable isntance.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual JsonValue Freeze() => this;

		/// <summary>Returns a read-only copy of this value, unless it is already read-only.</summary>
		/// <returns>The same instance if it is already read-only, or a new read-only deep copy, if it was mutable.</returns>
		/// <remarks>
		/// <para>The value returned is guaranteed to be immutable, and is safe to cache, share, or use as a singleton.</para>
		/// <para>Only JSON Objects and Arrays are impacted. All other "value types" (strings, booleans, numbers, ...) are always immutable, and will not be copied.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[Pure]
		public virtual JsonValue ToReadOnly() => this;

		/// <summary>Convert this JSON value so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (strings, booleans, numbers, ...) that are always immutable.</para>
		/// </remarks>
		[Pure]
		public virtual JsonValue ToMutable() => this;

		/// <summary>Returns <see langword="true"/> if this value is considered as "small" and can be safely written to a debug log (without flooding).</summary>
		/// <remarks>
		/// <para>For example, any JSON Object or Array must have 5 children or less, all small values, to be considered "small". Likewise, a JSON String literal must have a length or 36 characters or less, to be considered "small".</para>
		/// <para>When generating a compact representation of a JSON value, for troubleshooting purpose, an Object or Array that is not "small", may be written as <c>[ 1, 2, 3, 4, 5, ...]</c> with the rest of the value ommitted.</para>
		/// </remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal abstract bool IsSmallValue();

		/// <summary>Returns <see langword="true"/> if this value is considered as "small" enough to be inlined with its parent when generating compact or one-line JSON, like a small array or JSON object.</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal abstract bool IsInlinable();

		/// <summary>Serializes this JSON value into a JSON string literal</summary>
		/// <param name="settings">Settings used to change the serialized JSON output (optional)</param>
		/// <returns>JSON text literal that can be written to disk, returned as the body of an HTTP request, or </returns>
		/// <example><c>var jsonText = new JsonObject() { ["hello"] = "world" }.ToJson(); // == "{ \"hello\": \"world\" }"</c></example>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public virtual string ToJson(CrystalJsonSettings? settings = null)
		{
			// implémentation générique
			if (this.IsNull)
			{
				return JsonTokens.Null;
			}

			var sb = new StringBuilder(this is JsonArray or JsonObject ? 256 : 64);
			var writer = new CrystalJsonWriter(sb, settings ?? CrystalJsonSettings.Json, null);
			JsonSerialize(writer);
			return sb.ToString();
		}
		//TODO: REVIEW: rename as "ToJsonText()" or something else? "ToXYZ" usually means that XYZ is the final result, but here it is a string, and not a JsonValue

		/// <summary>Returns a "compact" string representation of this value, that can fit into a troubleshooting log.</summary>
		/// <remarks>
		/// <para>If the value is too large, parts of it will be shortened by adding <c>', ...'</c> tokens, so that it is still readable by a human.</para>
		/// <para>Note: the string that is return is not valid JSON and may not be parsable! This is only intended for quick introspection of a JSON document, by a human, using a log file or console output.</para>
		/// </remarks>
		[Pure]
		internal virtual string GetCompactRepresentation(int depth) => this.ToJson();

		/// <summary>Converts this JSON value into a printable string</summary>
		/// <remarks>
		/// <para>Please not that, due to convention in .NET, this will return the empty string for null values, since <see cref="object.ToString"/> must not return a null reference! Please call <see cref="ToStringOrDefault"/> if you need null references for null or missing JSON values.</para>
		/// <para>See <see cref="ToString(string,IFormatProvider)"/> if you need to specify a different format than the default</para></remarks>
		public override string ToString() => ToString(null, null);

		/// <summary>Converts this JSON value into a printable string, using the specified format</summary>
		/// <param name="format">Desired format, or "D" (default) if omitted</param>
		/// <remarks>See <see cref="ToString(string,IFormatProvider)"/> for the list of supported formats</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Converts this JSON value into a printable string, using the specified format and provider</summary>
		/// <param name="format">Desired format, or "D" (default) if omitted</param>
		/// <param name="provider">This parameter is ignored. JSON values are always formatted using <see cref="CultureInfo.InvariantCulture"/>.</param>
		/// <remarks>Supported values for <paramref name="format"/> are:
		/// <list type="bullet">
		///   <listheader><term>format</term><description>foo</description></listheader>
		///   <item><term>D</term><description>Default, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.Json"/></description></item>
		///   <item><term>C</term><description>Compact, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.JsonCompact"/></description></item>
		///   <item><term>P</term><description>Pretty, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.JsonIndented"/></description></item>
		///   <item><term>J</term><description>JavaScript, equivalent to calling <see cref="ToJson"/> with <see cref="CrystalJsonSettings.JavaScript"/>.</description></item>
		///   <item><term>Q</term><description>Quick, equivalent to calling <see cref="GetCompactRepresentation"/>, that will return a simplified/partial version, suitable for logs/traces.</description></item>
		/// </list>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual string ToString(string? format, IFormatProvider? provider)
		{
			switch(format ?? "D")
			{
				case "D":
				case "d":
				{ // "D" is for Default!
					return this.ToJson();
				}
				case "C":
				case "c":
				{ // "C" is for Compact!
					return this.ToJsonCompact();
				}
				case "P":
				case "p":
				{ // "P" is for Pretty!
					return this.ToJsonIndented();
				}
				case "Q":
				case "q":
				{ // "Q" is for Quick!
					return GetCompactRepresentation(0);
				}
				case "J":
				case "j":
				{ // "J" is for Javascript!
					return this.ToJson(CrystalJsonSettings.JavaScript);
				}
				case "B":
				case "b":
				{ // "B" is for Build!
					return JsonEncoding.Encode(this.ToJsonCompact());
				}
				default:
				{
#if DEBUG
					if (format!.EndsWith("}}", StringComparison.Ordinal))
					{
						// ATTENTION! vous avez un bug dans un formatter de string interpolation!
						// Si vous écrivez "... {{{obj:P}}}", ca va parser "P}}" comme le string format!!
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					}
#endif
					throw new NotSupportedException($"Invalid JSON format '{format}' specification");
				}
			}
		}

		JsonValue IJsonDynamic.GetJsonValue()
		{
			return this;
		}

		/// <summary>Creates a deep copy of this value (and all of its children)</summary>
		/// <returns>A new instance, isolated from the original.</returns>
		/// <remarks>
		/// <para>Any changes to this copy will not have any effect of the original, and vice-versa</para>
		/// <para>If the value is an immutable type (like <see cref="JsonString"/>, <see cref="JsonNumber"/>, <see cref="JsonBoolean"/>, ...) then the same instance will be returned.</para>
		/// <para>If the value is an array then a new array containing a </para>
		/// </remarks>
		public virtual JsonValue Copy() => Copy(deep: true, readOnly: false);

		/// <summary>Creates a copy of this object</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		/// <remarks>Immutable JSON values (like strings, numbers, ...) will return themselves without any change.</remarks>
		[Pure]
		protected internal virtual JsonValue Copy(bool deep, bool readOnly) => this;

		public override bool Equals(object? obj) => obj is JsonValue value && Equals(value);

		public abstract bool Equals(JsonValue? other);

		/// <summary>Tests if two JSON values are equivalent</summary>
		public static bool Equals(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).Equals(right ?? JsonNull.Missing);

		/// <summary>Compares two JSON values, and returns an integer that indicates whether the first value precedes, follows, or occurs in the same position in the sort order as the second value.</summary>
		public static int Compare(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).CompareTo(right ?? JsonNull.Missing); 

		/// <summary>Returns a hashcode that can be used to quickly identify a JSON value.</summary>
		/// <remarks>
		/// <para>The hashcode is guaranteed to remain unchanged during the lifetime of the object.</para>
		/// <para>
		/// <b>Caution:</b> there is *NO* guarantee that two equivalent Objects or Arrays will have the same hash code! 
		/// This means that it is *NOT* safe to use a JSON object or array has the key of a Dictionary or other collection that uses hashcodes to quickly compare two instances.
		/// </para>
		/// </remarks>
		public abstract override int GetHashCode();

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual int CompareTo(JsonValue? other)
		{
			if (other == null) return this.IsNull ? 0 : +1;
			if (ReferenceEquals(this, other)) return 0;

			// cannot compare a value type directly with an object or array
			if (other is JsonObject or JsonArray)
			{
				throw ThrowHelper.InvalidOperationException($"Cannot compare a JSON value with another value of type {other.Type}");
			}

			// pas vraiment de solution magique, on va comparer les type et les hashcode (pas pire que mieux)
			int c = ((int)this.Type).CompareTo((int)other.Type);
			if (c == 0)
			{
				c = this.GetHashCode().CompareTo(other.GetHashCode());
			}

			return c;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool Contains(JsonValue? value) => throw FailDoesNotSupportContains(this);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportContains(JsonValue value) => new($"Cannot index into a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, string key) => new($"Cannot read property '{key}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, int index) => new($"Cannot read value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, Index index) => new($"Cannot read value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, string key) => new($"Cannot set proprty '{key}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, int index) => new($"Cannot set value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, Index index) => new($"Cannot set value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailCannotMutateReadOnlyValue(JsonValue value) => new($"Cannot mutate JSON {value.Type} because it is marked as read-only.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailCannotMutateImmutableValue(JsonValue value) => new($"Cannot mutate JSON {value.Type} because it is immutable.");

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		/// <summary>Returns the value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="value">When this method returns, the value located at the specified index, if the index is inside the bounds of the array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if <paramref name="index"/> is inside the bounds of the array; otherwise, <see langword="false"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		/// <summary>Returns the value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="value">When this method returns, the value located at the specified index, if the index is inside the bounds of the array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if <paramref name="index"/> is inside the bounds of the array; otherwise, <see langword="false"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(string key) => GetValueOrDefault(key, JsonNull.Missing).RequiredField(key);

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(ReadOnlySpan<char> key) => GetValueOrDefault(key, JsonNull.Missing).RequiredField(key);

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(ReadOnlyMemory<char> key) => GetValueOrDefault(key, JsonNull.Missing).RequiredField(key.Span);

		/// <summary>Returns the value at the <b>required</b> item at the specified index.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <returns>The value located at the specified index, or an exception if the index is outside the bounds of the array, or if the item is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(int index) => GetValueOrDefault(index, JsonNull.Missing).RequiredIndex(index);

		/// <summary>Returns the value at the <b>required</b> item at the specified index.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <returns>The value located at the specified index, or an exception if the index is outside the bounds of the array, or if the item is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(Index index) => GetValueOrDefault(index, JsonNull.Missing).RequiredIndex(index);
		
		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown. Please call <see cref="JsonValueExtensions.AsObject">obj.AsObject()</see>.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(string key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key);

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown. Please call <see cref="JsonValueExtensions.AsObject">obj.AsObject()</see>.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key.ToString());

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown. Please call <see cref="JsonValueExtensions.AsObject">obj.AsObject()</see>.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key.ToString());

		/// <summary>Returns the value at the <i>optional</i> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array, or if the item at this location is null or missing.</param>
		/// <returns>The value located at the specified index, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, of the item is null or missing.</returns>
		/// <remarks>If the index is outside the bounds, and <paramref name="defaultValue"/> is not specified, then <see cref="JsonNull.Error"/> is returned.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, index);

		/// <summary>Returns the value at the <i>optional</i> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array, or if the item at this location is null or missing.</param>
		/// <returns>The value located at the specified index, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, of the item is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, index);

		/// <summary>Retourne la valeur d'un champ, si cette valeur est un objet JSON</summary>
		/// <param name="key">Nom du champ à retourner</param>
		/// <returns>Valeur de ce champ, ou missing si le champ n'existe. Une exception si cette valeur n'est pas un objet JSON</returns>
		/// <exception cref="System.ArgumentNullException">Si <paramref name="key"/> est null.</exception>
		/// <exception cref="System.InvalidOperationException">Cet valeur JSON ne supporte pas la notion d'indexation</exception>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue this[string key]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(key);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, key));
		}

		/// <summary>Returns the element at the specified index, if the current value is a JSON Array</summary>
		/// <param name="index">Index of the element to return</param>
		/// <returns>Value of the element at the specified <paramref name="index"/>. An exception is thrown if the current value is not a JSON Array, or if the element is outside the bounds of the array</returns>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing</exception>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the bounds of the array</exception>
		[AllowNull]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(index);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, index));
		}

		/// <summary>Returns the element at the specified index, if the current value is a JSON Array</summary>
		/// <param name="index">Index of the element to return</param>
		/// <returns>Value of the element at the specified <paramref name="index"/>. An exception is thrown if the current value is not a JSON Array, or if the element is outside the bounds of the array</returns>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing</exception>
		/// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is outside the bounds of the array</exception>
		[AllowNull]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue this[Index index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(index);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? ThrowHelper.InvalidOperationException($"Cannot mutate a read-only JSON {this.Type}") : ThrowHelper.InvalidOperationException($"Cannot set value at index {index} on a JSON {this.Type}"));
		}

		#region NEW API

		#region Object...

		/// <summary>Gets the JSON Object that corresponds to the field with the specified name, it it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="obj">When this method returns, contains the value of the field if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(string key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(key, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the field with the specified name, it it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="obj">When this method returns, contains the value of the field if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(key, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the field with the specified name, it it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="obj">When this method returns, contains the value of the field if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(key, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the item at the specified location, it it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="obj">When this method returns, contains the value at this location if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(int index, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(index, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the item at the specified location, it it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="obj">When this method returns, contains the value at this location if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(Index index, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(index, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(string key, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="resolver"></param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(string key, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="resolver"></param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(ReadOnlySpan<char> key, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="resolver"></param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(ReadOnlyMemory<char> key, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello") // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => Exception
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public TValue Get<TValue>(string key) where TValue : notnull => GetValue(key).Required<TValue>();

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the field is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(string key, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValueOrDefault(key).RequiredField(key, message).Required<TValue>(resolver);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello", "not_found") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello", -1) // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello", null) // => <see langword="null"/>
		/// ({ }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ }).Get&lt;int&gt;("Hello", -1) // => <c>-1</c>
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => <see langword="null"/>
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string key, TValue defaultValue) => GetValueOrDefault(key).As(defaultValue);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string key, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(key).As(defaultValue, resolver);

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObject(string key) => GetValue(key).AsObject();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if there if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetObjectOrDefault(string key, JsonObject? defaultValue = null) => GetValueOrDefault(key).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObjectOrEmpty(string key) => GetValueOrDefault(key).AsObjectOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObject(int index) => GetValueOrDefault(index).RequiredIndex(index).AsObject();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if there if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetObjectOrDefault(int index, JsonObject? defaultValue) => GetValueOrDefault(index).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObjectOrEmpty(int index) => GetValueOrDefault(index).AsObjectOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObject(Index index) => GetValueOrDefault(index).RequiredIndex(index).AsObject();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if there if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetObjectOrDefault(Index index, JsonObject? defaultValue = null) => GetValueOrDefault(index).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObjectOrEmpty(Index index) => GetValueOrDefault(index).AsObjectOrEmpty();

		#endregion

		#region Array

		/// <summary>Gets the JSON Array that corresponds to the field with the specified name, it it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="array">When this method returns, contains the value of the field if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(string key, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(key, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the field with the specified name, it it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="array">When this method returns, contains the value of the field if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(key, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the field with the specified name, it it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="array">When this method returns, contains the value of the field if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(key, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the item at the specified location, it it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="array">When this method returns, contains the value at this location if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(int index, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(index, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the item at the specified location, it it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="array">When this method returns, contains the value at this location if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(Index index, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(index, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(0, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(2, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(3, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(3, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(0, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(0, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(int index, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="resolver"></param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(0, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(2, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(3, resolver, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(3, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(0, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(0, resolver, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(int index, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^3, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^1, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^4, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^4, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(^1, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(^1, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(Index index, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="resolver"></param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^3, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^1, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^4, resolver, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^4, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(^1, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(^1, resolver, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(Index index, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public TValue Get<TValue>(int index) where TValue : notnull => GetValue(index).Required<TValue>();

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="resolver"></param>
		/// <param name="message"></param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(int index, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValueOrDefault(index).RequiredIndex(index, message).Required<TValue>();

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public TValue Get<TValue>(Index index) where TValue : notnull => Get<TValue>(index, resolver: null, message: null);

		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(Index index, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValue(index).Required<TValue>();

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue defaultValue) => GetValueOrDefault(index).As(defaultValue);

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <param name="resolver"></param>
		/// <returns>the value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(index).As(defaultValue, resolver);

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>the value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue defaultValue) => GetValueOrDefault(index).As(defaultValue);

		/// <summary>Gets the converted value at the specified index, if it is contains inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <param name="resolver"></param>
		/// <returns>the value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(index).As(defaultValue, resolver);

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(string key) => GetValue(key).AsArray();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(ReadOnlySpan<char> key) => GetValue(key).AsArray();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(ReadOnlyMemory<char> key) => GetValue(key).AsArray();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(string key) => GetValueOrDefault(key).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(ReadOnlySpan<char> key) => GetValueOrDefault(key).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(ReadOnlyMemory<char> key) => GetValueOrDefault(key).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(string key) => GetValueOrDefault(key).AsArrayOrEmpty();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(ReadOnlySpan<char> key) => GetValueOrDefault(key).AsArrayOrEmpty();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(ReadOnlyMemory<char> key) => GetValueOrDefault(key).AsArrayOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(int index) => GetValue(index).AsArray();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(int index) => GetValueOrDefault(index).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(int index) => GetValueOrDefault(index).AsArrayOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing, or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(Index index) => GetValue(index).AsArray();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(Index index) => GetValueOrDefault(index).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.EmptyReadOnly"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(Index index) => GetValueOrDefault(index).AsArrayOrEmpty();

		#endregion

		#endregion

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, or <see cref="JsonNull.Missing"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValue(string path) => GetPathCore(JsonPath.Create(path), null, required: true);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">Value that is returned if the path was not found, or the value is null or missing.</param>
		/// <returns>the value found at this location, or <paramref name="defaultValue"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValueOrDefault(string path, JsonValue? defaultValue = null) => GetPathCore(JsonPath.Create(path), defaultValue, required: false);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, or <see cref="JsonNull.Missing"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValue(JsonPath path) => GetPathCore(path, null, required: true);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">Value that is returned if the path was not found, or the value is null or missing.</param>
		/// <returns>the value found at this location, or <paramref name="defaultValue"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValueOrDefault(JsonPath path, JsonValue? defaultValue = null) => GetPathCore(path, defaultValue, required: false);

		private JsonValue GetPathCore(JsonPath path, JsonValue? defaultValue, bool required)
		{
			var current = this;
			foreach (var (parent, key, idx, last) in path)
			{
				if (key.Length > 0)
				{ // field access
					if (current.IsNullOrMissing())
					{
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Missing;
					}
					if (current is not JsonObject obj)
					{ // equivalent to null, but to notify that we tried to index into a value that is not an object
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Error;
					}
					current = obj.GetValueOrDefault(key);
				}
				else
				{ // array index
					if (current.IsNullOrMissing())
					{
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Missing;
					}
					if (current is not JsonArray arr)
					{ // equivalent to null, but to notify that we tried to index into a value that is not an object
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Error;
					}
					current = arr.GetValueOrDefault(idx);
				}
			}

			if (current is JsonNull)
			{
				if (required) throw JsonValueExtensions.ErrorPathIsNullOrMissing(this, path);
				// must keep the "type" of null so that we can distinguish between an explicit null, or a missing field in the path
				return defaultValue ?? current;
			}

			return current;
		}

		[Pure]
		public JsonObject GetPathObject(string path) => GetPathCore(JsonPath.Create(path), null, required: true).AsObject();

		[Pure]
		public JsonObject GetPathObject(JsonPath path) => GetPathCore(path, null, required: true).AsObject();

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetPathObjectOrDefault(string path, JsonObject? defaultValue = null) => GetPathCore(JsonPath.Create(path), null, required: true).AsObjectOrDefault() ?? defaultValue;

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetPathObjectOrDefault(JsonPath path, JsonObject? defaultValue = null) => GetPathCore(path, null, required: true).AsObjectOrDefault() ?? defaultValue;

		[Pure]
		public JsonObject GetPathObjectOrEmpty(string path) => GetPathCore(JsonPath.Create(path), null, required: false).AsObjectOrEmpty();

		[Pure]
		public JsonObject GetPathObjectOrEmpty(JsonPath path) => GetPathCore(path, null, required: false).AsObjectOrEmpty();

		[Pure]
		public JsonArray GetPathArray(string path) => GetPathCore(JsonPath.Create(path), null, required: true).AsArray();

		[Pure]
		public JsonArray GetPathArray(JsonPath path) => GetPathCore(path, null, required: true).AsArray();

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonArray? GetPathArrayOrDefault(string path, JsonArray? defaultValue = null) => GetPathCore(JsonPath.Create(path), null, required: false).AsArrayOrDefault() ?? defaultValue;

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonArray? GetPathArrayOrDefault(JsonPath path, JsonArray? defaultValue = null) => GetPathCore(path, null, required: false).AsArrayOrDefault() ?? defaultValue;

		[Pure]
		public JsonArray GetPathArrayOrEmpty(string path) => GetPathCore(JsonPath.Create(path), null, required: false).AsArrayOrEmpty();

		[Pure]
		public JsonArray GetPathArrayOrEmpty(JsonPath path) => GetPathCore(path, null, required: false).AsArrayOrEmpty();

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or and exception if there was not match, or the matched value is null.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue GetPath<TValue>(string path) where TValue : notnull
		{
			return GetPathCore(JsonPath.Create(path), null, required: true).Required<TValue>();
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">The default value to return when the no match is found for the specified <paramref name="path" />, or it is null or missing.</param>
		/// <returns>the value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if no match was found or the value is null or missing.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? GetPath<TValue>(string path, TValue defaultValue)
		{
			return GetPathCore(JsonPath.Create(path), JsonNull.Missing, required: false).As(defaultValue);
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or and exception if there was not match, or the matched value is null.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue GetPath<TValue>(JsonPath path) where TValue : notnull
		{
			return GetPathCore(path, null, required: true).Required<TValue>();
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">The default value to return when the no match is found for the specified <paramref name="path" />, or it is null or missing.</param>
		/// <returns>the value found at this location, converted into a instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if no match was found or the value is null or missing.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? GetPath<TValue>(JsonPath path, TValue defaultValue)
		{
			return GetPathCore(path, JsonNull.Missing, required: false).As(defaultValue);
		}

		//BLACK MAGIC!

		// In order to be able to write "if (obj["Hello"]) ... else ...", then JsonValue must implement or operators 'op_true' and 'op_false'
		// In order to be able to write "if (obj["Hello"] && obj["World"]) ...." (resp. '||') then JsonValue must implements the operator 'op_&' (resp: 'op_|'),
		// and it must return a JsonValue instance (which will then be passed to 'op_true'/'op_false' to produce a boolean).

		/// <summary>Test if this value is logically equivalent to <see langword="true"/></summary>
		public static bool operator true(JsonValue? obj) => obj != null && obj.ToBoolean();

		/// <summary>Test if this value is logically equivalent to <see langword="false"/></summary>
		public static bool operator false(JsonValue? obj) => obj == null || obj.ToBoolean();

		/// <summary>Perform a logical AND operation between two JSON values</summary>
		public static JsonValue operator &(JsonValue? left, JsonValue? right)
		{
			//REVIEW:TODO: maybe handle the cases were both values are JSON Numbers, and perform a bit-wise AND instead?
			return left != null && right!= null && left.ToBoolean() && right.ToBoolean() ? JsonBoolean.True : JsonBoolean.False;
		}

		/// <summary>Perform a logical OR operation between two JSON values</summary>
		public static JsonValue operator |(JsonValue? left, JsonValue? right)
		{
			//REVIEW:TODO: maybe handle the cases were both values are JSON Numbers, and perform a bit-wise OR instead?
			return (left != null && left.ToBoolean()) || (right != null && right.ToBoolean()) ? JsonBoolean.True : JsonBoolean.False;
		}

		public static bool operator <(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) < 0;
		}

		public static bool operator <=(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) <= 0;
		}

		public static bool operator >(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) > 0;
		}

		public static bool operator >=(JsonValue? left, JsonValue? right)
		{
			return (left ?? JsonNull.Null).CompareTo(right ?? JsonNull.Null) >= 0;
		}

		#region IJsonSerializable

		public abstract void JsonSerialize(CrystalJsonWriter writer);

		#endregion

		#region IJsonConvertible...

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual string? ToStringOrDefault(string? defaultValue = null) => ToString();

		public virtual bool ToBoolean() => throw Errors.JsonConversionNotSupported(this, typeof(bool));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual bool? ToBooleanOrDefault(bool? defaultValue = default) => ToBoolean();

		public virtual byte ToByte() => throw Errors.JsonConversionNotSupported(this, typeof(byte));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual byte? ToByteOrDefault(byte? defaultValue = default) => ToByte();

		public virtual sbyte ToSByte() => throw Errors.JsonConversionNotSupported(this, typeof(sbyte));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual sbyte? ToSByteOrDefault(sbyte? defaultValue = default) => ToSByte();

		public virtual char ToChar() => throw Errors.JsonConversionNotSupported(this, typeof(char));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual char? ToCharOrDefault(char? defaultValue = default) => ToChar();

		public virtual short ToInt16() => throw Errors.JsonConversionNotSupported(this, typeof(short));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual short? ToInt16OrDefault(short? defaultValue = default) => ToInt16();

		public virtual ushort ToUInt16() => throw Errors.JsonConversionNotSupported(this, typeof(ushort));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual ushort? ToUInt16OrDefault(ushort? defaultValue = default) => ToUInt16();

		public virtual int ToInt32() => throw Errors.JsonConversionNotSupported(this, typeof(int));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual int? ToInt32OrDefault(int? defaultValue = default) => ToInt32();

		public virtual uint ToUInt32() => throw Errors.JsonConversionNotSupported(this, typeof(uint));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual uint? ToUInt32OrDefault(uint? defaultValue = default) => ToUInt32();

		public virtual long ToInt64() => throw Errors.JsonConversionNotSupported(this, typeof(long));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual long? ToInt64OrDefault(long? defaultValue = default) => ToInt64();

		public virtual ulong ToUInt64() => throw Errors.JsonConversionNotSupported(this, typeof(ulong));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual ulong? ToUInt64OrDefault(ulong? defaultValue = default) => ToUInt64();

		public virtual float ToSingle() => throw Errors.JsonConversionNotSupported(this, typeof(float));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual float? ToSingleOrDefault(float? defaultValue = default) => ToSingle();

		public virtual double ToDouble() => throw Errors.JsonConversionNotSupported(this, typeof(double));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual double? ToDoubleOrDefault(double? defaultValue = default) => ToDouble();

		public virtual Half ToHalf() => throw Errors.JsonConversionNotSupported(this, typeof(Half));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Half? ToHalfOrDefault(Half? defaultValue = default) => ToHalf();

		public virtual decimal ToDecimal() => throw Errors.JsonConversionNotSupported(this, typeof(decimal));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual decimal? ToDecimalOrDefault(decimal? defaultValue = default) => ToDecimal();

		public virtual Guid ToGuid() => throw Errors.JsonConversionNotSupported(this, typeof(Guid));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Guid? ToGuidOrDefault(Guid? defaultValue = default) => ToGuid();

		public virtual Uuid128 ToUuid128() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid128));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = default) => ToUuid128();

		public virtual Uuid96 ToUuid96() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid96));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = default) => ToUuid96();

		public virtual Uuid80 ToUuid80() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid80));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = default) => ToUuid80();

		public virtual Uuid64 ToUuid64() => throw Errors.JsonConversionNotSupported(this, typeof(Uuid64));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = default) => ToUuid64();

		public virtual DateTime ToDateTime() => throw Errors.JsonConversionNotSupported(this, typeof(DateTime));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual DateTime? ToDateTimeOrDefault(DateTime? defaultValue = default) => ToDateTime();

		public virtual DateTimeOffset ToDateTimeOffset() => throw Errors.JsonConversionNotSupported(this, typeof(DateTimeOffset));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = default) => ToDateTimeOffset();

		public virtual TimeSpan ToTimeSpan() => throw Errors.JsonConversionNotSupported(this, typeof(TimeSpan));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = default) => ToTimeSpan();

		public virtual TEnum ToEnum<TEnum>()
			where TEnum : struct, Enum
			=> throw Errors.JsonConversionNotSupported(this, typeof(TimeSpan));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = default)
			where TEnum : struct, Enum
			=> ToEnum<TEnum>();

		public virtual NodaTime.Instant ToInstant() => throw Errors.JsonConversionNotSupported(this, typeof(NodaTime.Instant));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = default) => ToInstant();

		public virtual NodaTime.Duration ToDuration() => throw Errors.JsonConversionNotSupported(this, typeof(NodaTime.Duration));

		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = default) => ToDuration();
		//TODO: ToZonedDateTime, ToLocalDateTime ?

		#endregion

		#region ISliceSerializable...

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract void WriteTo(ref SliceWriter writer);

		#endregion

	}

}
