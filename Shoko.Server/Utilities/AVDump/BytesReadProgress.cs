using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AVDump3Lib.Processing.BlockBuffers;
using AVDump3Lib.Processing.StreamConsumer;
using AVDump3Lib.Processing.StreamProvider;

namespace Shoko.Server.Utilities.AVDump;


public record FileProgress (string FilePath, bool Completed, long TotalBytes, long ProcessedBytes);

public class BytesReadProgress : IBytesReadProgress {
	public class StreamConsumerProgressInfo {
		public string fileName = null!;
		public bool completed;
		public long totalBytesProcessed;
		public long totalBytes;
		public long blockConsumerCount;
	}

	private DateTimeOffset startedOn;

	private readonly ConcurrentDictionary<IBlockStream, StreamConsumerProgressInfo> blockStreamProgress;

	public BytesReadProgress() {
		blockStreamProgress = new ConcurrentDictionary<IBlockStream, StreamConsumerProgressInfo>();

	}

	public FileProgress[] GetProgress() {
		var fileProgressList = new List<FileProgress>();
		foreach(var blockStreamProgressItem in blockStreamProgress) {
			var progressInfo = blockStreamProgressItem.Value;
			fileProgressList.Add(new FileProgress(progressInfo.fileName, progressInfo.completed, progressInfo.totalBytes, progressInfo.totalBytesProcessed / progressInfo.blockConsumerCount));

			if(progressInfo.completed) {
				blockStreamProgress.TryRemove(blockStreamProgressItem.Key, out _);
			}
		}
		return fileProgressList.ToArray();
	}

	public void Register(ProvidedStream providedStream, IStreamConsumer streamConsumer) {
		var progressInfo = new StreamConsumerProgressInfo();
		progressInfo.fileName = (string)providedStream.Tag;
		progressInfo.blockConsumerCount = streamConsumer.BlockConsumers.Length;
		progressInfo.totalBytes = providedStream.Stream.Length; 

		blockStreamProgress.TryAdd(streamConsumer.BlockStream, progressInfo);

		streamConsumer.Finished += BlockConsumerFinished;
	}

	private void BlockConsumerFinished(IStreamConsumer streamConsumer) {
		if(blockStreamProgress.TryGetValue(streamConsumer.BlockStream, out var progressInfo)) {
			progressInfo.completed = true;
		}


	}

	public void Report(BlockStreamProgress value) {
		if(startedOn == DateTimeOffset.MinValue) {
			startedOn = DateTimeOffset.UtcNow;
		}

		if(value.Index > -1 && blockStreamProgress.TryGetValue(value.Sender, out var progressInfo)) {
			Interlocked.Add(ref progressInfo.totalBytesProcessed, value.BytesRead);
		}
	}

	public void Skip(ProvidedStream providedStream, long length) {
		if(startedOn == DateTimeOffset.MinValue) {
			startedOn = DateTimeOffset.UtcNow;
		}
	}
}
