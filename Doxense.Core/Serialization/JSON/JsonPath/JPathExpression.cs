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

namespace Doxense.Serialization.Json.JsonPath
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq.Expressions;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public abstract class JPathExpression : IEquatable<JPathExpression>
	{

		internal abstract IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current);

		protected static JsonValue? GetAtIndex(JsonValue item, int index)
		{
			if (item is JsonArray array)
			{
				if (index < 0) index += array.Count;
				return (uint) index < (uint) array.Count ? array[index] : null;
			}
			return null;
		}

		protected static bool IsTruthy(JsonValue x)
		{
			switch (x)
			{
				case null: return false;
				case JsonNull _: return false;
				case JsonBoolean b: return b.Value;
				case JsonString s: return s.Length > 0;
				case JsonNumber n: return !n.IsDefault;
				case JsonArray a: return a.Count > 0;
			}
			return true;
		}

		protected static JsonValue? ArrayPseudoProperty(JsonArray array, string name)
		{
			switch (name)
			{
				case "$length": return array.Count;
				case "$first": return array.Count > 0 ? array[0] : null;
				case "$last": return array.Count > 0 ? array[array.Count - 1] : null;
				default: return null; //TODO: or throw "invalid array pseudo-property?"
			}
		}

		/// <summary>Returns the root node of the JSON document</summary>
		public static JPathExpression Root { get; } = new JPathSpecialToken('$');

		/// <summary>Returns the current node (relative to the current scope)</summary>
		public static JPathExpression Current { get; } = new JPathSpecialToken('@');

		[Pure]
		public static JPathExpression Not(JPathExpression node)
		{
			Contract.NotNull(node);
			return new JPathUnaryOperator(ExpressionType.Not, node);
		}

		[Pure]
		public JPathExpression Not() => new JPathUnaryOperator(ExpressionType.Not, this);

		[Pure]
		public static JPathExpression Quote(JPathExpression node)
		{
			Contract.NotNull(node);
			return new JPathQuoteExpression(node);
		}

		[Pure]
		public JPathExpression Quote() => new JPathQuoteExpression(this);

		[Pure]
		public static JPathExpression Matching(JPathExpression node, JPathExpression filter)
		{
			Contract.NotNull(node);
			Contract.NotNull(filter);
			return new JPathFilterExpression(node, filter);
		}

		[Pure]
		public JPathExpression Matching(JPathExpression filter) => Matching(this, filter);

		/// <summary>Returns the property of an object with the specified name</summary>
		[Pure]
		public static JPathExpression Property(JPathExpression node, string name)
		{
			Contract.NotNull(node);
			Contract.NotNull(name);
			return new JPathObjectIndexer(node, name);
		}

		[Pure]
		public JPathExpression Property(string name) => Property(this, name);

		/// <summary>Unwrap elle the items of an array</summary>
		public static JPathExpression All(JPathExpression node)
		{
			Contract.NotNull(node);
			return new JPathArrayRange(node, null, null);
		}

		public JPathExpression All() => new JPathArrayRange(this, null, null);

		/// <summary>Returns the item at the specified index of an array</summary>
		[Pure]
		public static JPathExpression At(JPathExpression node, int index)
		{
			Contract.NotNull(node);
			return new JPathArrayIndexer(node, index);
		}

		[Pure]
		public JPathExpression At(int index) => At(this, index);

		[Pure]
		public static JPathExpression BinaryOperator(ExpressionType op, JPathExpression node, string literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(op, node, literal);
		}

		[Pure]
		public static JPathExpression BinaryOperator(ExpressionType op, JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(op, node, literal);
		}

		[Pure]
		public static JPathExpression BinaryOperator(ExpressionType op, JPathExpression node, JPathExpression literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(op, node, literal);
		}

		[Pure]
		public static JPathExpression AndAlso(JPathExpression left, JPathExpression right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);
			//note: to simplify parsing of parenthesis inside logical expressions, we unwrap the Quote(..) that contain them here!
			if (left is JPathQuoteExpression ql) left = ql.Node;
			if (right is JPathQuoteExpression qr) right = qr.Node;
			//REVIEW: this waste some memory, maybe we could simplify this! by only quoting if followed by [] ?
			return new JPathBinaryOperator(ExpressionType.AndAlso, left, right);
		}

		[Pure]
		public static JPathExpression OrElse(JPathExpression left, JPathExpression right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);
			//note: to simplify parsing of parenthesis inside logical expressions, we unwrap the Quote(..) that contain them here!
			if (left is JPathQuoteExpression ql) left = ql.Node;
			if (right is JPathQuoteExpression qr) right = qr.Node;
			//REVIEW: this waste some memory, maybe we could simplify this! by only quoting if followed by [] ?
			return new JPathBinaryOperator(ExpressionType.OrElse, left, right);
		}

		[Pure]
		public static JPathExpression EqualTo(JPathExpression node, string literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.Equal, node, literal);
		}

		[Pure]
		public static JPathExpression EqualTo(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.Equal, node, literal);
		}

		[Pure]
		public JPathExpression EqualTo(string literal) => EqualTo(this, literal);

		[Pure]
		public JPathExpression EqualTo(JsonValue literal) => EqualTo(this, literal);

		[Pure]
		public static JPathExpression NotEqual(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.NotEqual, node, literal);
		}

		[Pure]
		public JPathExpression NotEqualTo(JsonValue literal) => NotEqual(this, literal);


		[Pure]
		public static JPathExpression GreaterThan(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.GreaterThan, node, literal);
		}

		[Pure]
		public JPathExpression GreaterThan(JsonValue literal) => GreaterThan(this, literal);

		[Pure]
		public static JPathExpression GreaterThanOrEqual(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.GreaterThanOrEqual, node, literal);
		}

		[Pure]
		public JPathExpression GreaterThanOrEqualTo(JsonValue literal) => GreaterThanOrEqual(this, literal);

		[Pure]
		public static JPathExpression LessThan(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.LessThan, node, literal);
		}

		[Pure]
		public JPathExpression LessThan(JsonValue literal) => LessThan(this, literal);

		[Pure]
		public static JPathExpression LessThanOrEqual(JPathExpression node, JsonValue literal)
		{
			Contract.NotNull(node);
			Contract.NotNull(literal);
			return new JPathBinaryOperator(ExpressionType.LessThanOrEqual, node, literal);
		}

		[Pure]
		public JPathExpression LessThanOrEqualTo(JsonValue literal) => LessThanOrEqual(this, literal);

		public override bool Equals(object? obj)
		{
			return obj == this || (obj is JPathExpression expr && Equals(expr));
		}

		public abstract override int GetHashCode();

		public abstract bool Equals(JPathExpression? other);
		
	}

	public sealed class JPathSpecialToken : JPathExpression
	{
		public char Token { get; }

		internal JPathSpecialToken(char token)
		{
			this.Token = token;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathSpecialToken tok && tok.Token == this.Token;
		}

		public override int GetHashCode()
		{
			return this.Token;
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			switch (this.Token)
			{
				case '$':
				{
					yield return root;
					break;
				}
				case '@':
				{
					yield return current;
					break;
				}
				default:
				{
					throw new InvalidOperationException();
				}
			}
		}

		public override string ToString()
		{
			return this.Token == '$' ? "$" : "@";
		}
	}

	public sealed class JPathObjectIndexer : JPathExpression
	{

		public JPathExpression Node { get; }

		public string Name { get; }

		internal JPathObjectIndexer(JPathExpression node, string name)
		{
			Contract.Debug.Requires(node != null && name != null);
			this.Node = node;
			this.Name = name;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathObjectIndexer idx && idx.Name == this.Name && idx.Node.Equals(this.Node);
		}

		public override int GetHashCode()
		{
			//TODO: cache!
			return HashCodes.Combine(this.Node.GetHashCode(), this.Node.GetHashCode());
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			// optimisation pour les cas les plus fréquents
			if (this.Node is JPathSpecialToken tok)
			{
				return IterateSpecialNode(tok, this.Name, root, current);
			}
			else
			{
				return IterateNodes(this.Node, this.Name, root, current);
			}
		}

		private static IEnumerable<JsonValue> IterateNodes(JPathExpression node, string name, JsonValue root, JsonValue current)
		{ 

			//Console.WriteLine($"Visit object prop '{this.Name}'");
			foreach (var x in node.Iterate(root, current))
			{
				if (x is JsonArray map)
				{
					var y = ArrayPseudoProperty(map, name);
					if (y != null) yield return y;
				}
				else if (x is JsonObject obj)
				{
					if (obj.TryGetValue(name, out var y))
					{
						yield return y;
					}
				}
			}
		}

		private static JsonValue[] IterateSpecialNode(JPathSpecialToken node, string name, JsonValue root, JsonValue current)
		{
			var x = node.Token == '$' ? root : current;

			if (x is JsonArray map)
			{
				var y = ArrayPseudoProperty(map, name);
				if (y != null) return new [] { y };
			}
			else if (x is JsonObject obj)
			{
				if (obj.TryGetValue(name, out var y))
				{
					return new [] { y };
				}
			}

			return Array.Empty<JsonValue>();
		}

		public override string ToString()
		{
			return this.Node.ToString() + "['" + this.Name + "']";
		}
	}

	[DebuggerDisplay("[{StartInclusive}:{EndExclusive}")]
	public sealed class JPathArrayRange : JPathExpression
	{
		public JPathExpression Node { get; }

		public int? StartInclusive { get; }

		public int? EndExclusive { get; }

		internal JPathArrayRange(JPathExpression node, int? start, int? end)
		{
			Contract.Debug.Requires(node != null);
			this.Node = node;
			this.StartInclusive = start;
			this.EndExclusive = end;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathArrayRange range && range.StartInclusive == this.StartInclusive && range.EndExclusive == this.EndExclusive && range.Node.Equals(this.Node);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(this.StartInclusive ?? 0, this.EndExclusive ?? 0, this.Node.GetHashCode());
		}

		public override string ToString()
		{
			if (this.StartInclusive == null && this.EndExclusive == null) return this.Node.ToString() + ".All()";
			return $"{this.Node}.Range({this.StartInclusive}:{this.EndExclusive})";
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			if ((this.StartInclusive ?? 0) == 0 && this.EndExclusive == null)
			{ // FULL SCAN
				foreach (var x in this.Node.Iterate(root, current))
				{
					if (x is JsonArray arr)
					{
						foreach (var y in arr)
						{
							yield return y;
						}
					}
				}
			}
			else
			{
				throw new NotImplementedException();
			}
		}
	}

	public sealed class JPathArrayIndexer : JPathExpression
	{

		public JPathExpression Node { get; }

		public int Index { get; }

		internal JPathArrayIndexer(JPathExpression node, int index)
		{
			Contract.Debug.Requires(node != null);
			this.Node = node;
			this.Index = index;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathArrayIndexer idx && idx.Index == this.Index && idx.Node.Equals(this.Node);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(this.Index, this.Node.GetHashCode());
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			foreach (var x in this.Node.Iterate(root, current))
			{
				var y = GetAtIndex(x, this.Index);
				if (y != null) yield return y;
			}
		}

		public override string ToString()
		{
			return this.Node.ToString() + "[" + this.Index + "]";
		}

	}

	public sealed class JPathFilterExpression : JPathExpression
	{

		public JPathExpression Node { get; }

		public JPathExpression Filter { get; }

		public JPathFilterExpression(JPathExpression node, JPathExpression filter)
		{
			Contract.Debug.Requires(node != null && filter != null);
			this.Node = node;
			this.Filter = filter;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathFilterExpression filter && filter.Filter.Equals(this.Filter) && filter.Node.Equals(this.Node);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(this.Node.GetHashCode(), this.Filter.GetHashCode());
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			// for each element that passes here:
			// - if apply the filter on this element returns at least one result that is ~true, then the element is passed along
			// - if the filter does not return anything or only items that are ~false, then the element is dropped.
			//note: special case here: if item is an array, then we filter the ITEMS of the array!
			// => if caller wants to filter the array itself, it must be quoted first

			bool unrollArrays = true;
			var node = this.Node;
			if (node is JPathQuoteExpression quote)
			{
				unrollArrays = false;
				node = quote.Node;
			}

			foreach (var x in node.Iterate(root, current))
			{
				if (x.IsNullOrMissing()) continue; //REVIEW: is there any expression that would select 'null' ?

				if (unrollArrays && x is JsonArray array)
				{ // for arrays we must apply the filter on the _elements_ and not the array itself!
					foreach (var item in array)
					{
						foreach (var y in this.Filter.Iterate(root, item))
						{
							if (IsTruthy(y))
							{ // found at least one match, we can emit this item
								yield return item;
								break;
							}
						}
					}
				}
				else
				{
					foreach (var y in this.Filter.Iterate(root, x))
					{
						if (IsTruthy(y))
						{ // found at least one match, we can emit this item
							yield return x;
							break;
						}
					}
				}
			}
		}

		public override string ToString()
		{
			return this.Node.ToString() + ".Where(@ => " + this.Filter.ToString() + ")";
		}

	}

	public sealed class JPathBinaryOperator : JPathExpression
	{
		public ExpressionType Operator { get; }

		public JPathExpression Left { get; }

		public object Right { get; }
		// can be: string, JsonValue, JPathExpression

		internal JPathBinaryOperator(ExpressionType op, JPathExpression left, object right)
		{
			Contract.Debug.Requires(left != null && right != null);
			Contract.Debug.Requires(right is string || right is JsonValue || right is JPathExpression);
			this.Operator = op;
			this.Left = left;
			this.Right = right;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathBinaryOperator op && op.Operator == this.Operator && object.Equals(op.Right, this.Right) && op.Left.Equals(this.Left);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine((int) this.Operator, this.Left.GetHashCode(), 123 /*TODO: Right?*/);
		}

		private IEnumerable<JsonValue> IterateStringLiteral(string literal, JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.Equal:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && s.Value == literal)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.NotEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && s.Value != literal)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.Compare(s.Value, literal, StringComparison.Ordinal) < 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.Compare(s.Value, literal, StringComparison.Ordinal) <= 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.Compare(s.Value, literal, StringComparison.Ordinal) > 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x is JsonString s && string.Compare(s.Value, literal, StringComparison.Ordinal) >= 0)
						{
							return True();
						}
					}
					return None();
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		private IEnumerable<JsonValue> IterateJsonLiteral(JsonValue literal, JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.Equal:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (literal.Equals(x))
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.NotEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (!literal.Equals(x))
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) < 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.LessThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) <= 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThan:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) > 0)
						{
							return True();
						}
					}
					return None();
				}
				case ExpressionType.GreaterThanOrEqual:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (x.CompareTo(literal) >= 0)
						{
							return True();
						}
					}
					return None();
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		private static JsonValue[] TrueArray { get; } = new JsonValue[] { JsonBoolean.True };

		private JsonValue[] True() => TrueArray;

		private JsonValue[] None() => Array.Empty<JsonValue>();

		private IEnumerable<JsonValue> IterateExpression(JPathExpression right, JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.AndAlso:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (IsTruthy(x))
						{
							foreach (var y in right.Iterate(root, current))
							{
								if (IsTruthy(y))
								{
									return True();
								}
							}
						}
					}
					return None();
				}
				case ExpressionType.OrElse:
				{
					foreach (var x in this.Left.Iterate(root, current))
					{
						if (IsTruthy(x))
						{
							return True();
						}
					}
					foreach (var x in right.Iterate(root, current))
					{
						if (IsTruthy(x))
						{
							return True();
						}
					}
					return None();
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			switch (this.Right)
			{
				case string str: return IterateStringLiteral(str, root, current);
				case JsonValue j: return IterateJsonLiteral(j, root, current);
				case JPathExpression expr: return IterateExpression(expr, root, current);
				default: throw new NotImplementedException();
			}
		}

		public override string ToString()
		{
			if (this.Right is string s) return $"{this.Operator}({this.Left}, '{s}')";
			if (this.Right is JsonValue j) return $"{this.Operator}({this.Left}, {j:Q})";
			return $"{this.Operator}({this.Left}, {this.Right})";
		}
	}


	public sealed class JPathUnaryOperator : JPathExpression
	{

		public ExpressionType Operator { get; }

		public JPathExpression Node { get; }

		internal JPathUnaryOperator(ExpressionType op, JPathExpression node)
		{
			Contract.Debug.Requires(node != null);
			Contract.Debug.Requires(op == ExpressionType.Not); //TODO: add others here!
			this.Node = node;
			this.Operator = op;
		}

		public override bool Equals(JPathExpression? obj)
		{
			return obj is JPathUnaryOperator op && op.Operator == this.Operator && op.Node.Equals(this.Node);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine((int) this.Operator, this.Node.GetHashCode());
		}

		public override string ToString()
		{
			return "Not(" + this.Node.ToString() + ")";
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			switch (this.Operator)
			{
				case ExpressionType.Not:
				{
					// "not(...)" returns:
					// - false if there is AT LEAST one item that is ~true
					// - true if there are no items, or they where all ~false

					foreach (var x in this.Node.Iterate(root, current))
					{
						if (IsTruthy(x)) yield break; // at least one true, so the whole expression is true, and not(true) => false
					}
					// we did not see any "true" (either nothing, or all false) so the whole expression is false, and not(false) => true
					yield return JsonBoolean.True;
					break;
				}
				default:
				{
					throw new InvalidOperationException();
				}
			}
		}
	}

	public sealed class JPathQuoteExpression : JPathExpression
	{

		public JPathExpression Node { get; }

		internal JPathQuoteExpression(JPathExpression node)
		{
			this.Node = node;
		}

		public override bool Equals(JPathExpression? other)
		{
			return other is JPathQuoteExpression quote && quote.Node.Equals(this.Node);
		}

		public override int GetHashCode()
		{
			return HashCodes.Combine(0xC0FFEEE, this.Node.GetHashCode());
		}

		public override string ToString()
		{
			return "Quote(" + this.Node.ToString() + ")";
		}

		internal override IEnumerable<JsonValue> Iterate(JsonValue root, JsonValue current)
		{
			// we will simply buffer all incoming elements into a single new array, that we will emit as a single element
			JsonArray? arr = null;
			foreach (var x in this.Node.Iterate(root, current))
			{
				if (x != null!)
				{
					(arr ??= new JsonArray()).Add(x);
				}
			}
			return arr == null ? Array.Empty<JsonValue>() : new JsonValue[] {arr};
		}

	}

}
