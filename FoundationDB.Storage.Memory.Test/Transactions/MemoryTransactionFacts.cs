﻿#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Layers.Tables;
	using NUnit.Framework;
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class MemoryTransactionFacts
	{

		[Test]
		public async Task Test_Hello_World()
		{
			using (var db = new MemoryDatabase("DB", FdbSubspace.Empty, false))
			{
				var key = db.Pack("hello");

				// v1
				await db.WriteAsync((tr) => tr.Set(key, Slice.FromString("World!")));
				db.Debug_Dump();
				var data = await db.ReadAsync((tr) => tr.GetAsync(key));
				Assert.That(data.ToUnicode(), Is.EqualTo("World!"));


				// v2
				await db.WriteAsync((tr) => tr.Set(key, Slice.FromString("Le Monde!")));
				db.Debug_Dump();
				data = await db.ReadAsync((tr) => tr.GetAsync(key));
				Assert.That(data.ToUnicode(), Is.EqualTo("Le Monde!"));

				using (var tr1 = db.BeginTransaction())
				{
					await tr1.GetReadVersionAsync();

					await db.WriteAsync((tr2) => tr2.Set(key, Slice.FromString("Sekai!")));
					db.Debug_Dump();

					data = await tr1.GetAsync(key);
					Assert.That(data.ToUnicode(), Is.EqualTo("Le Monde!"));
				}

				data = await db.ReadAsync((tr) => tr.GetAsync(key));
				Assert.That(data.ToUnicode(), Is.EqualTo("Sekai!"));

			}
		}

		[Test]
		public async Task Test_GetKey()
		{
			Slice key;
			Slice value;

			using (var db = new MemoryDatabase("DB", FdbSubspace.Empty, false))
			{

				using (var tr = db.BeginTransaction())
				{
					tr.Set(db.Pack(0), Slice.FromString("first"));
					tr.Set(db.Pack(10), Slice.FromString("ten"));
					tr.Set(db.Pack(20), Slice.FromString("ten ten"));
					tr.Set(db.Pack(42), Slice.FromString("narf!"));
					tr.Set(db.Pack(100), Slice.FromString("a hundred missipis"));
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{

					value = await tr.GetAsync(db.Pack(42));
					Console.WriteLine(value);
					Assert.That(value.ToString(), Is.EqualTo("narf!"));

					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(42)));

					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(100)));

					key = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(42)));

					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(20)));

					var keys = await tr.GetKeysAsync(new[]
					{
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(42)),
						FdbKeySelector.FirstGreaterThan(db.Pack(42)),
						FdbKeySelector.LastLessOrEqual(db.Pack(42)),
						FdbKeySelector.LastLessThan(db.Pack(42))
					});

					Assert.That(keys.Length, Is.EqualTo(4));
					Assert.That(keys[0], Is.EqualTo(db.Pack(42)));
					Assert.That(keys[1], Is.EqualTo(db.Pack(100)));
					Assert.That(keys[2], Is.EqualTo(db.Pack(42)));
					Assert.That(keys[3], Is.EqualTo(db.Pack(20)));

					await tr.CommitAsync();
				}

			}

		}

		[Test]
		public async Task Test_GetKey_ReadConflicts()
		{
			Slice key;

			using(var db = new MemoryDatabase("FOO", FdbSubspace.Empty, false))
			{
				using(var tr = db.BeginTransaction())
				{
					tr.Set(db.Pack(42), Slice.FromString("42"));
					tr.Set(db.Pack(50), Slice.FromString("50"));
					tr.Set(db.Pack(60), Slice.FromString("60"));
					await tr.CommitAsync();
				}
				db.Debug_Dump();

				Func<FdbKeySelector, Slice, Task> check = async (selector, expected) =>
				{
					using (var tr = db.BeginTransaction())
					{
						key = await tr.GetKeyAsync(selector);
						await tr.CommitAsync();
						Assert.That(key, Is.EqualTo(expected), selector.ToString() + " => " + FdbKey.Dump(expected));
					}
				};

				await check(
					FdbKeySelector.FirstGreaterOrEqual(db.Pack(50)), 
					db.Pack(50)
				);
				await check(
					FdbKeySelector.FirstGreaterThan(db.Pack(50)),
					db.Pack(60)
				);

				await check(
					FdbKeySelector.FirstGreaterOrEqual(db.Pack(49)),
					db.Pack(50)
				);
				await check(
					FdbKeySelector.FirstGreaterThan(db.Pack(49)),
					db.Pack(50)
				);

				await check(
					FdbKeySelector.FirstGreaterOrEqual(db.Pack(49)) + 1,
					db.Pack(60)
				);
				await check(
					FdbKeySelector.FirstGreaterThan(db.Pack(49)) + 1,
					db.Pack(60)
				);

				await check(
					FdbKeySelector.LastLessOrEqual(db.Pack(49)),
					db.Pack(42)
				);
				await check(
					FdbKeySelector.LastLessThan(db.Pack(49)),
					db.Pack(42)
				);
			}
		}

		[Test]
		public async Task Test_GetRangeAsync()
		{
			Slice key;

			using (var db = new MemoryDatabase("DB", FdbSubspace.Empty, false))
			{

				using (var tr = db.BeginTransaction())
				{
					for (int i = 0; i <= 100; i++)
					{
						tr.Set(db.Pack(i), Slice.FromString("value of " + i));
					}
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				// verify that key selectors work find
				using (var tr = db.BeginTransaction())
				{
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(FdbKey.MaxValue));
					if (key != FdbKey.MaxValue) Assert.Inconclusive("Key selectors are buggy: fGE(max)");
					key = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(FdbKey.MaxValue));
					if (key != FdbKey.MaxValue) Assert.Inconclusive("Key selectors are buggy: lLE(max)");
					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.MaxValue));
					if (key != db.Pack(100)) Assert.Inconclusive("Key selectors are buggy: lLT(max)");
				}

				using (var tr = db.BeginTransaction())
				{

					var chunk = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(0)),
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(50))
					);

					for (int i = 0; i < chunk.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + chunk.Chunk[i].Key + " = " + chunk.Chunk[i].Value);
					}
					Assert.That(chunk.Count, Is.EqualTo(50), "chunk.Count");
					Assert.That(chunk.HasMore, Is.False, "chunk.HasMore");
					Assert.That(chunk.Reversed, Is.False, "chunk.Reversed");
					Assert.That(chunk.Iteration, Is.EqualTo(0), "chunk.Iteration");

					for (int i = 0; i < 50; i++)
					{
						Assert.That(chunk.Chunk[i].Key, Is.EqualTo(db.Pack(i)), "[{0}].Key", i);
						Assert.That(chunk.Chunk[i].Value.ToString(), Is.EqualTo("value of " + i), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					var chunk = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(0)),
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(50)),
						new FdbRangeOptions { Reverse = true }
					);

					for (int i = 0; i < chunk.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + chunk.Chunk[i].Key + " = " + chunk.Chunk[i].Value);
					}
					Assert.That(chunk.Count, Is.EqualTo(50), "chunk.Count");
					Assert.That(chunk.HasMore, Is.False, "chunk.HasMore");
					Assert.That(chunk.Reversed, Is.True, "chunk.Reversed");
					Assert.That(chunk.Iteration, Is.EqualTo(0), "chunk.Iteration");

					for (int i = 0; i < 50; i++)
					{
						Assert.That(chunk.Chunk[i].Key, Is.EqualTo(db.Pack(49 - i)), "[{0}].Key", i);
						Assert.That(chunk.Chunk[i].Value.ToString(), Is.EqualTo("value of " + (49 - i)), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					var chunk = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(0)),
						FdbKeySelector.FirstGreaterOrEqual(FdbKey.MaxValue),
						new FdbRangeOptions { Reverse = true, Limit = 1 }
					);

					for (int i = 0; i < chunk.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + chunk.Chunk[i].Key + " = " + chunk.Chunk[i].Value);
					}
					Assert.That(chunk.Count, Is.EqualTo(1), "chunk.Count");
					Assert.That(chunk.HasMore, Is.True, "chunk.HasMore");
					Assert.That(chunk.Reversed, Is.True, "chunk.Reversed");
					Assert.That(chunk.Iteration, Is.EqualTo(0), "chunk.Iteration");

					await tr.CommitAsync();
				}

			}

		}

		[Test]
		public async Task Test_GetRange()
		{

			using (var db = new MemoryDatabase("DB", FdbSubspace.Empty, false))
			{

				using (var tr = db.BeginTransaction())
				{
					for (int i = 0; i <= 100; i++)
					{
						tr.Set(db.Pack(i), Slice.FromString("value of " + i));
					}
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{

					var results = await tr
						.GetRange(db.Pack(0), db.Pack(50))
						.ToListAsync();

					Assert.That(results, Is.Not.Null);
					for (int i = 0; i < results.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + results[i].Key + " = " + results[i].Value);
					}

					Assert.That(results.Count, Is.EqualTo(50));
					for (int i = 0; i < 50; i++)
					{
						Assert.That(results[i].Key, Is.EqualTo(db.Pack(i)), "[{0}].Key", i);
						Assert.That(results[i].Value.ToString(), Is.EqualTo("value of " + i), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					var results = await tr
						.GetRange(db.Pack(0), db.Pack(50), new FdbRangeOptions { Reverse = true })
						.ToListAsync();

					for (int i = 0; i < results.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + results[i].Key + " = " + results[i].Value);
					}

					Assert.That(results.Count, Is.EqualTo(50));
					for (int i = 0; i < 50; i++)
					{
						Assert.That(results[i].Key, Is.EqualTo(db.Pack(49 - i)), "[{0}].Key", i);
						Assert.That(results[i].Value.ToString(), Is.EqualTo("value of " + (49 - i)), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{
					var result = await tr
						.GetRange(db.Pack(0), FdbKey.MaxValue, new FdbRangeOptions { Reverse = true })
						.FirstOrDefaultAsync();

					Console.WriteLine(result.Key + " = " + result.Value);
					Assert.That(result.Key, Is.EqualTo(db.Pack(100)));
					Assert.That(result.Value.ToString(), Is.EqualTo("value of 100"));

					await tr.CommitAsync();
				}

			}

		}

		private async Task Scenario1(IFdbTransaction tr)
		{

			tr.Set(Slice.FromAscii("hello"), Slice.FromAscii("world!"));
			tr.Clear(Slice.FromAscii("removed"));
			var result = await tr.GetAsync(Slice.FromAscii("narf"));
		}

		private async Task Scenario2(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));
			tr.ClearRange(FdbKeyRange.StartsWith(location.Key));
			for (int i = 0; i < 10; i++)
			{
				tr.Set(location.Pack(i), Slice.FromString("value of " + i));
			}
		}

		private async Task Scenario3(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			tr.Set(location.Key + (byte)'a', Slice.FromAscii("A"));
			tr.AtomicAdd(location.Key + (byte)'k', Slice.FromFixed32(1));
			tr.Set(location.Key + (byte)'z', Slice.FromAscii("C"));
			tr.ClearRange(location.Key + (byte)'a', location.Key + (byte)'k');
			tr.ClearRange(location.Key + (byte)'k', location.Key + (byte)'z');
		}

		private async Task Scenario4(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			//tr.Set(location.Key, Slice.FromString("NARF"));
			//tr.AtomicAdd(location.Key, Slice.FromFixedU32(1));
			tr.AtomicAnd(location.Key, Slice.FromFixedU32(7));
			tr.AtomicXor(location.Key, Slice.FromFixedU32(3));
			tr.AtomicXor(location.Key, Slice.FromFixedU32(15));
		}

		private async Task Scenario5(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			//tr.Set(location.Pack(42), Slice.FromString("42"));
			//tr.Set(location.Pack(50), Slice.FromString("50"));
			//tr.Set(location.Pack(60), Slice.FromString("60"));

			var x = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(location.Pack(49)));
			Console.WriteLine(x);

			tr.Set(location.Pack("FOO"), Slice.FromString("BAR"));

		}

		[Test]
		public async Task Test_Compare_Implementations()
		{
			int mode = 5;

			using (var db = await Fdb.OpenAsync(@"c:\temp\fdb\fdb1.cluster", "DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					await tr.GetReadVersionAsync();

					switch (mode)
					{
						case 1: await Scenario1(tr); break;
						case 2: await Scenario2(tr); break;
						case 3: await Scenario3(tr); break;
						case 4: await Scenario4(tr); break;
						case 5: await Scenario5(tr); break;
					}

					await tr.CommitAsync();
				}
			}

			using (var db = new MemoryDatabase("DB", FdbSubspace.Empty, false))
			{
				using (var tr = db.BeginTransaction(FdbTransactionMode.Default))
				{
					await tr.GetReadVersionAsync();

					switch (mode)
					{
						case 1: await Scenario1(tr); break;
						case 2: await Scenario2(tr); break;
						case 3: await Scenario3(tr); break;
						case 4: await Scenario4(tr); break;
						case 5: await Scenario5(tr); break;
					}

					await tr.CommitAsync();
				}

				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_Write_Then_Read()
		{
			using (var db = new MemoryDatabase("FOO", FdbSubspace.Empty, false))
			{
				using(var tr = db.BeginTransaction())
				{
					tr.Set(Slice.FromString("hello"), Slice.FromString("World!"));
					tr.AtomicAdd(Slice.FromString("counter"), Slice.FromFixed32(1));
					tr.Set(Slice.FromString("foo"), Slice.FromString("bar"));
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{
					var result = await tr.GetAsync(Slice.FromString("hello"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("World!"));

					result = await tr.GetAsync(Slice.FromString("counter"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToInt32(), Is.EqualTo(1));

					result = await tr.GetAsync(Slice.FromString("foo"));
					Assert.That(result.ToString(), Is.EqualTo("bar"));

				}

				using (var tr = db.BeginTransaction())
				{
					tr.Set(Slice.FromString("hello"), Slice.FromString("Le Monde!"));
					tr.AtomicAdd(Slice.FromString("counter"), Slice.FromFixed32(1));
					tr.Set(Slice.FromString("narf"), Slice.FromString("zort"));
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{
					var result = await tr.GetAsync(Slice.FromString("hello"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("Le Monde!"));

					result = await tr.GetAsync(Slice.FromString("counter"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToInt32(), Is.EqualTo(2));

					result = await tr.GetAsync(Slice.FromString("foo"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("bar"));

					result = await tr.GetAsync(Slice.FromString("narf"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("zort"));
				}

			}
		}

		[Test]
		public async Task Test_Use_Simple_Layer()
		{
			using(var db = new MemoryDatabase("FOO", FdbSubspace.Empty, false))
			{

				var table = new FdbTable<int, string>("Foos", db.GlobalSpace.Partition("Foos"), KeyValueEncoders.Values.StringEncoder);
				var index = new FdbIndex<int, string>("Foos.ByColor", db.GlobalSpace.Partition("Foos", "Color"));

				using(var tr = db.BeginTransaction())
				{
					table.Set(tr, 3, @"{ ""name"": ""Juliet"", ""color"": ""red"" }");
					table.Set(tr, 2, @"{ ""name"": ""Joey"", ""color"": ""blue"" }");
					table.Set(tr, 1, @"{ ""name"": ""Bob"", ""color"": ""red"" }");

					index.Add(tr, 3, "red");
					index.Add(tr, 2, "blue");
					index.Add(tr, 1, "red");

					await tr.CommitAsync();
				}

				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_MiniBench()
		{
			const int M = 1 * 1000 * 1000;
			const int B = 100;
			const int W = 1;

			const int T = M / (B * W);
			const int KEYSIZE = 10;
			const int VALUESIZE = 100;
			const bool RANDOM = false;

			var rnd = new Random();

			//WARMUP
			using (var db = new MemoryDatabase("FOO", FdbSubspace.Empty, false))
			{
				await db.WriteAsync((tr) => tr.Set(Slice.FromString("hello"), Slice.FromString("world")));
				Slice.Random(rnd, KEYSIZE);
				Slice.Random(rnd, VALUESIZE);
			}

			Console.WriteLine("Inserting " + KEYSIZE + "-bytes " + (RANDOM ? "random" : "ordered") + " keys / " + VALUESIZE + "-bytes values, in " + T.ToString("N0") + " transactions");

			bool random = RANDOM;
			string fmt = "D" + KEYSIZE;
			using (var db = new MemoryDatabase("FOO", FdbSubspace.Empty, false))
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Console.WriteLine("Total memory: " + GC.GetTotalMemory(false).ToString("N0") + ", " + Environment.WorkingSet.ToString("N0"));

				long total = 0;

				var payload = new byte[1000 + VALUESIZE];
				rnd.NextBytes(payload);

				Task[] tasks = new Task[W];
				for (int w = 0; w < W; w++)
				{
					int wkid = w;
					tasks[w] = Task.Run(async () =>
					{
						await Task.Delay(10).ConfigureAwait(false);

						int offset = wkid * T * B;
						for (int i = 0; i < T; i++)
						{
							using (var tr = db.BeginTransaction())
							{
								for (int j = 0; j < B; j++)
								{
									Slice key;
									if (random)
									{
										do
										{
											key = Slice.Random(rnd, KEYSIZE);
										}
										while (key[0] == 255);
									}
									else
									{
										int x = i * B + offset + j;
										//x = x % 1000;
										key = Slice.FromString(x.ToString(fmt));
									}

									tr.Set(key, Slice.Create(payload, rnd.Next(1000), VALUESIZE));
									//tr.Set(key, Slice.Random(rnd, VALUESIZE));
									//tr.Set(key, Slice.FromString("written at " + (i * B + offset + j)));
									Interlocked.Increment(ref total);
								}
								await tr.CommitAsync().ConfigureAwait(false);
							}
							if (i % 1000 == 0) Console.Write(".");// + (i * B).ToString("D10"));
						}
					});


				}
				var sw = Stopwatch.StartNew();
				Task.WaitAll(tasks);
				sw.Stop();
				Console.WriteLine("done");
				Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
				Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
				Console.WriteLine("* TPS: " + (T / sw.Elapsed.TotalSeconds).ToString("N0") + " transaction/sec");
				Console.WriteLine("* KPS: " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
				Console.WriteLine("* BPS: " + ((total * (KEYSIZE + VALUESIZE)) / sw.Elapsed.TotalSeconds).ToString("N0") + " byte/sec");

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Console.WriteLine("Total memory: " + GC.GetTotalMemory(false).ToString("N0") + ", " + Environment.WorkingSet.ToString("N0"));

				db.Debug_Dump(false);

				var data = await db.GetValuesAsync(Enumerable.Range(0, 1000).Select(i => Slice.FromString(i.ToString(fmt))));

				// sequential reads

				sw.Restart();
				for (int i = 0; i < total; i++)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetAsync(Slice.FromString(i.ToString(fmt)));
					}
				}
				sw.Stop();
				Console.WriteLine("SeqRead1  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i+= 10 )
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(x.ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("SeqRead10 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(x.ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("SeqRead100: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 1000)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 1000).Select(x => Slice.FromString(x.ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("SeqRead1k : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				// random reads

				sw.Restart();
				for (int i = 0; i < total; i++)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						int x = rnd.Next((int)total);
						await db.GetAsync(Slice.FromString(x.ToString(fmt)));
					}
				}
				sw.Stop();
				Console.WriteLine("RndRead1  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("RndRead10 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("RndRead100: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 1000)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 1000).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("RndRead1k : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");
			}

		}

	}
}
