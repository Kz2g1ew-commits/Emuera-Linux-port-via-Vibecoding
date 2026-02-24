using System;
using System.Collections.Generic;
using System.IO;

namespace MinorShift.Emuera.UI.Game;

internal static class RikaiIndexGenerator
{
	public static bool TryGenerateAndSave(
		byte[] edict,
		string outputPath,
		Action<int> reportProgress,
		out byte[] edictIndex,
		out string errorMessage)
	{
		edictIndex = Array.Empty<byte>();
		errorMessage = string.Empty;

		if (edict == null || edict.Length == 0)
		{
			errorMessage = "Dictionary payload is empty.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(outputPath))
		{
			errorMessage = "Output path is empty.";
			return false;
		}

		try
		{
			edictIndex = GenerateIndex(edict, reportProgress);

			var outputDir = Path.GetDirectoryName(outputPath);
			if (!string.IsNullOrWhiteSpace(outputDir))
				Directory.CreateDirectory(outputDir);

			File.WriteAllBytes(outputPath, edictIndex);
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			return false;
		}
	}

	private static byte[] GenerateIndex(byte[] edict, Action<int> reportProgress)
	{
		var indexEntryList = new IndexEntryList();
		int oldPercentage = -1;
		var edictParser = new EdictParser(edict);

		edictParser.Step();
		indexEntryList.AddFirst(edictParser.mWord, edictParser.mStart);

		edictParser.Step();
		indexEntryList.AddSecond(edictParser.mWord, edictParser.mStart);

		while (true)
		{
			edictParser.Step();
			if (edictParser.mFinished)
			{
				reportProgress?.Invoke(100);
				break;
			}

			long tickNew = DateTime.Now.Ticks;
			if (tickNew > edictParser.mTickNext)
			{
				edictParser.mTickNext = tickNew + EdictParser.mTickDelta;
				int percentage = (int)(edictParser.mEnd / (float)edict.Length * 100);
				if (oldPercentage != percentage)
				{
					oldPercentage = percentage;
					reportProgress?.Invoke(percentage);
				}
			}

			indexEntryList.Add(edictParser.mWord, edictParser.mStart);

			if (edictParser.mPronunciation.Equals(null))
				continue;

			indexEntryList.Add(edictParser.mPronunciation, edictParser.mStart);
		}

		var memory = new MemoryStream(0x10000);
		memory.WriteByte(0);

		var bytesToWrite = new List<byte>(64);
		foreach (var ie in indexEntryList.mList)
		{
			memory.Write(ie.mWord.Span);
			foreach (var offset in ie.offsets)
			{
				int num = offset;

				memory.WriteByte(1);
				bytesToWrite.Clear();

				while (true)
				{
					num = Math.DivRem(num, 0x100, out int rem);
					if ((rem >> 2) == 0)
					{
						bytesToWrite.Insert(0, 2);
						rem = (rem << 2) + 2;
						bytesToWrite.Insert(1, (byte)rem);
					}
					else
					{
						bytesToWrite.Insert(0, (byte)rem);
					}

					if (num == 0)
						break;
				}

				foreach (var b in bytesToWrite)
					memory.WriteByte(b);
			}

			memory.WriteByte(0);
		}

		return memory.ToArray();
	}

	private sealed class EdictParser
	{
		public readonly byte[] mEdict;
		public readonly ReadOnlyMemory<byte> mEdictMemory;
		public int mStart;
		public int mEnd;
		public bool mFinished;
		public const long mTickDelta = 4000;
		public long mTickNext = DateTime.Now.Ticks + mTickDelta;
		public ReadOnlyMemory<byte> mWord;
		public ReadOnlyMemory<byte> mPronunciation;

		public EdictParser(byte[] aEdict)
		{
			mEdict = aEdict;
			mEdictMemory = mEdict.AsMemory();

			for (; mEnd < mEdict.Length; ++mEnd)
			{
				if (mEdict[mEnd] == '\n')
				{
					mEnd--;
					mStart = mEnd;
					break;
				}
			}
		}

		public void Step()
		{
			mWord = null;
			mPronunciation = null;
			mEnd += 2;
			mStart = mEnd;

			if (mEnd >= mEdict.Length)
			{
				mFinished = true;
				return;
			}

			for (; mEnd < mEdict.Length; ++mEnd)
			{
				if (mEdict[mEnd] == '\n')
					break;
			}

			mEnd--;

			for (int c = mStart; c < mEnd; c++)
			{
				if (mEdict[c] == ',')
					throw new Exception();
				if (mEdict[c] == '/')
				{
					int b = c - 1;
					mWord = mEdictMemory.Slice(mStart, b - mStart);
					break;
				}

				if (mEdict[c] != '[')
					continue;

				int b2 = c - 1;
				mWord = mEdictMemory.Slice(mStart, b2 - mStart);
				c++;
				int pronounStart = c;
				for (; c < mEnd; c++)
				{
					if (mEdict[c] == ']')
					{
						mPronunciation = mEdictMemory.Slice(pronounStart, c - pronounStart);
						c = mEnd;
						break;
					}

					if (mEdict[c] == ',')
						throw new Exception();
				}
			}
		}
	}

	private sealed class IndexEntryList
	{
		public readonly List<IndexEntry> mList = new(0x10000);

		public void Add(ReadOnlyMemory<byte> word, int offset)
		{
			var ie = new IndexEntry(word);
			int index = mList.BinarySearch(ie);
			if (index >= 0)
			{
				mList[index].offsets.Add(offset);
			}
			else
			{
				ie.offsets.Add(offset);
				mList.Insert(~index, ie);
			}
		}

		public void AddFirst(ReadOnlyMemory<byte> word, int offset)
		{
			var indexEntry = new IndexEntry(word);
			indexEntry.offsets.Add(offset);
			mList.Add(indexEntry);
		}

		public void AddSecond(ReadOnlyMemory<byte> word, int offset)
		{
			var indexEntry = new IndexEntry(word);
			indexEntry.offsets.Add(offset);
			mList.Add(indexEntry);
		}
	}

	private sealed class IndexEntry(ReadOnlyMemory<byte> word) : IComparable<IndexEntry>
	{
		public readonly ReadOnlyMemory<byte> mWord = word;
		public readonly List<int> offsets = new(4);

		public int CompareTo(IndexEntry ie)
		{
			return mWord.Span.SequenceCompareTo(ie.mWord.Span);
		}
	}
}
