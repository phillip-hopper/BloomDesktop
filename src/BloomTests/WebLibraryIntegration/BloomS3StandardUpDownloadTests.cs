﻿using System;
using System.IO;
using System.Linq;
using Bloom.WebLibraryIntegration;
using BloomTemp;
using NUnit.Framework;
using Palaso.Progress;

namespace BloomTests.WebLibraryIntegration
{
	class BloomS3StandardUpDownloadTests
	{
		private BloomS3Client _client;
		private TemporaryFolder _workFolder;
		private string _srcCollectionPath;
		private string _destCollectionPath;
		private const string BookName = "Test Book";
		private const string ExcludedFile = "thumbs.db";
		private string _storageKeyOfBookFolder;

		[TestFixtureSetUp]
		public void SetupFixture()
		{
			// Basic setup
			_workFolder = new TemporaryFolder("unittest2");
			var workFolderPath = _workFolder.FolderPath;
			Assert.AreEqual(0, Directory.GetDirectories(workFolderPath).Count(), "Some stuff was left over from a previous test");

			_client = new BloomS3Client(BloomS3Client.UnitTestBucketName);

			// Now do standard upload/download. We save time by making this whole class do one upload/download sequence
			// on the assumption that things that should be uploaded were if they make it through the download process too.
			// Individual tests just compare what was uploaded with what came back through the download.
			// If we want to upload and download to separate (collection) folders, we need another layer for the actual book

			_storageKeyOfBookFolder = Guid.NewGuid().ToString();

			// First create folder to upload from
			var unittestGuid = Guid.NewGuid();
			var srcFolder = new TemporaryFolder(_workFolder, "unittest-src-" + unittestGuid);
			_srcCollectionPath = srcFolder.FolderPath;

			// Then create standard book
			var book = MakeBookIncludingThumbs(srcFolder);

			// Upload standard book
			UploadBook(book);

			// Create folder to download to
			var destFolder = new TemporaryFolder(_workFolder, "unittest-dest-" + unittestGuid);
			_destCollectionPath = destFolder.FolderPath;

			// Download standard book
			DownloadBook();
		}

		[TestFixtureTearDown]
		public void TearDown()
		{
			_client.EmptyUnitTestBucket(_storageKeyOfBookFolder);
			_workFolder.Dispose();
			_client.Dispose();
		}

		private string MakeBookIncludingThumbs(TemporaryFolder srcFolder)
		{
			var bookFolder = new TemporaryFolder(srcFolder, BookName).FolderPath;
			File.WriteAllText(Path.Combine(bookFolder, "one.htm"), "test");
			File.WriteAllText(Path.Combine(bookFolder, "one.css"), "test");
			File.WriteAllText(Path.Combine(bookFolder, "thumbs.db"), "test thumbs.db file");
			return bookFolder;
		}

		private void UploadBook(string bookFolder)
		{
			_client.UploadBook(_storageKeyOfBookFolder, bookFolder, new NullProgress());
		}

		private void DownloadBook()
		{
			var expectedBookDestination = Path.Combine(_destCollectionPath, BookName);
			var actualDestination = _client.DownloadBook(_storageKeyOfBookFolder, _destCollectionPath);
			Assert.AreEqual(expectedBookDestination, actualDestination);
		}

		[Test]
		public void UploadDownloadStandardBook_FilesAreInExpectedDirectory()
		{
			var fullBookSrcPath = Path.Combine(_srcCollectionPath, BookName);
			var fullBookDestPath = Path.Combine(_destCollectionPath, BookName);

			Assert.IsTrue(Directory.Exists(_destCollectionPath));
			var srcFileCount = Directory.GetFiles(fullBookSrcPath).Count();

			// Do not count the excluded file (thumbs.db)
			Assert.AreEqual(_client.GetBookFileCount(_storageKeyOfBookFolder) + 1, srcFileCount);
			Assert.AreEqual(Directory.GetFiles(fullBookDestPath).Count() + 1, srcFileCount);
			foreach (var fileName in Directory.GetFiles(fullBookSrcPath)
				.Select(Path.GetFileName)
				.Where(file => file != ExcludedFile))
			{
				Assert.IsTrue(File.Exists(Path.Combine(fullBookDestPath, fileName)));
			}
		}

		[Test]
		public void UploadDownloadStandardBook_ThumbsDbFileDidNotGetSent()
		{
			// Verify that thumbs.db did NOT get uploaded
			var notexpectedDestPath = Path.Combine(_destCollectionPath, BookName, ExcludedFile);
			Assert.IsFalse(File.Exists(notexpectedDestPath));
		}
	}
}
