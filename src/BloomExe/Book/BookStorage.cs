using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using System.Security;
using System.Text;
using System.Xml;
using Bloom.Collection;
using Bloom.ImageProcessing;
using Bloom.Properties;
using Palaso.Code;
using Palaso.Extensions;
using Palaso.IO;
using Palaso.Progress;
using Palaso.Reporting;
using Palaso.UI.WindowsForms.FileSystem;
using Palaso.Xml;

namespace Bloom.Book
{
	/* The role of this class is simply to isolate the actual storage mechanism (e.g. file system)
	 * to a single place.  All the other classes can then just pass around DOMs.
	 */
	public interface IBookStorage
	{
		//TODO Covert this most of this section to something like IBookDescriptor, which has enough display in a catalog, do some basic filtering, etc.
		string Key { get; }
		string FileName { get; }
		string FolderPath { get; }
		string PathToExistingHtml { get; }
		bool TryGetPremadeThumbnail(string fileName, out Image image);
		//bool DeleteBook();
		bool RemoveBookThumbnail(string fileName);

		// REQUIRE INTIALIZATION (AVOID UNLESS USER IS WORKING WITH THIS BOOK SPECIFICALLY)
		bool GetLooksOk();
		HtmlDom Dom { get; }
		void Save();
		HtmlDom GetRelocatableCopyOfDom(IProgress log);
		HtmlDom MakeDomRelocatable(HtmlDom dom, IProgress log);
		string SaveHtml(HtmlDom bookDom);
		void SetBookName(string name);
		string GetValidateErrors();
		void CheckBook(IProgress progress,string pathToFolderOfReplacementImages = null);
		void UpdateBookFileAndFolderName(CollectionSettings settings);
		IFileLocator GetFileLocator();
		event EventHandler FolderPathChanged;

		BookInfo MetaData { get; set; }
		void UpdateStyleSheetLinkPaths(HtmlDom printingDom);
	}

	public class BookStorage : IBookStorage
	{
		public delegate BookStorage Factory(string folderPath);//autofac uses this

		/// <summary>
		/// History of this number:
		///		0.4 had version 0.4
		///		0.8, 0.9, 1.0 had version 0.8
		///		1.1 had version 1.1
		///     Bloom 1.0 went out with format version 0.8, but meanwhile compatible books were made (by hand) with "1.1."
		///     We didn't notice because Bloom didn't actually check this number, it just produced it.
		///     At that point, books with a newer format version (2.0) started becoming availalbe, so we patched Bloom 1.0
		///     to reject those. At that point, we also changed Bloom 1.0's kBloomFormatVersion to 1.1 so that it would
		///     not reject those format version 1.1 books.
		///     For the next version of Bloom (expected to be called version 2, but unknown at the moment) we went with
		///     (coincidentally) kBloomFormatVersion = "2.0"
		internal const string kBloomFormatVersion = "2.0";
		private  string _folderPath;
		private IChangeableFileLocator _fileLocator;
		private BookRenamedEvent _bookRenamedEvent;
		private readonly CollectionSettings _collectionSettings;
		private string ErrorMessages;
		private static bool _alreadyNotifiedAboutOneFailedCopy;
		private HtmlDom _dom; //never remove the readonly: this is shared by others
		private BookInfo _metaData;
		public event EventHandler FolderPathChanged;

		public BookInfo MetaData
		{
			get
			{
				if (_metaData == null)
					_metaData = new BookInfo(_folderPath, false);
				return _metaData;
			}
			set { _metaData = value; }
		}


		public BookStorage(string folderPath, Palaso.IO.IChangeableFileLocator baseFileLocator,
						   BookRenamedEvent bookRenamedEvent, CollectionSettings collectionSettings)
		{
			_folderPath = folderPath;

			//we clone becuase we'll be customizing this for use by just this book
			_fileLocator = (IChangeableFileLocator) baseFileLocator.CloneAndCustomize(new string[]{});
			_bookRenamedEvent = bookRenamedEvent;
			_collectionSettings = collectionSettings;

			ExpensiveInitialization();
		}

		public string PathToExistingHtml
		{
			get { return FindBookHtmlInFolder(_folderPath); }
		}

		public string FileName
		{
			get { return Path.GetFileNameWithoutExtension(_folderPath); }
		}

		public string FolderPath
		{
			get { return _folderPath; }
		}

		public string Key
		{
			get
			{
				return _folderPath;
			}
		}

		/// <summary>
		///
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns>false if we shouldn't mess with the thumbnail</returns>
		public bool RemoveBookThumbnail(string fileName)
		{
			string path = Path.Combine(_folderPath, fileName);
			if(File.Exists(path) &&
			 (new System.IO.FileInfo(path).IsReadOnly)) //readonly is good when you've put in a custom thumbnail
			{
				return false;
			}
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			return true;
		}

		/// <summary>
		/// this is a method because it wasn't clear if we will eventually generate it on the fly (book paths do change as they are renamed)
		/// </summary>
		/// <returns></returns>
		public IFileLocator GetFileLocator()
		{
			return _fileLocator;
		}

		public bool TryGetPremadeThumbnail(string fileName, out Image image)
		{
			string path = Path.Combine(_folderPath, fileName);
			if (File.Exists(path))
			{
				//this FromFile thing locks the file until the image is disposed of. Therefore, we copy the image and dispose of the original.
				using (var tempImage = Image.FromFile(path))
				{
					image = new Bitmap(tempImage);
				}
				return true;
			}
			image = null;
			return false;
		}



		public HtmlDom Dom
		{
			get
			{

				return _dom;
			}
		}

		public static string GetMessageIfVersionIsIncompatibleWithThisBloom(HtmlDom dom)
		{
			//var dom = new HtmlDom(XmlHtmlConverter.GetXmlDomFromHtmlFile(path, false));//with throw if there are errors

			var versionString = dom.GetMetaValue("BloomFormatVersion", "").Trim();
			if (string.IsNullOrEmpty(versionString))
				return "";// "This file lacks the following required element: <meta name='BloomFormatVersion' content='x.y'>";

			float versionFloat = 0;
			if (!float.TryParse(versionString, out versionFloat))
				return "This file claims a version number that isn't really a number: " + versionString;

			if (versionFloat > float.Parse(kBloomFormatVersion))
				return string.Format("This book or template was made with a newer version of Bloom. Download the latest version of Bloom from bloomlibrary.org (format {0} vs. {1})", versionString, kBloomFormatVersion);

			return null;
		}

		public bool GetLooksOk()
		{
			return File.Exists(PathToExistingHtml) && string.IsNullOrEmpty(ErrorMessages);
		}

		public void Save()
		{
			Logger.WriteEvent("BookStorage.Saving... (eventual destination: {0})", PathToExistingHtml);

			Dom.UpdateMetaElement("Generator", "Bloom " + ErrorReport.GetVersionForErrorReporting());
			if (null != Assembly.GetEntryAssembly()) // null during unit tests
			{
				var ver = Assembly.GetEntryAssembly().GetName().Version;
				Dom.UpdateMetaElement("BloomFormatVersion", kBloomFormatVersion);
			}
			MetaData.FormatVersion = kBloomFormatVersion;
			string tempPath = SaveHtml(Dom);


			string errors = ValidateBook(tempPath);
			if (!string.IsNullOrEmpty(errors))
			{
				Logger.WriteEvent("Errors saving book {0}: {1}", PathToExistingHtml, errors);
				var badFilePath = PathToExistingHtml + ".bad";
				File.Copy(tempPath, badFilePath, true);
				//hack so we can package this for palaso reporting
				errors += string.Format("{0}{0}{0}Contents:{0}{0}{1}", Environment.NewLine,
					File.ReadAllText(badFilePath));
				var ex = new XmlSyntaxException(errors);

				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(ex, "Before saving, Bloom did an integrity check of your book, and found something wrong. This doesn't mean your work is lost, but it does mean that there is a bug in the system or templates somewhere, and the developers need to find and fix the problem (and your book).  Please click the 'Details' button and send this report to the developers.  Bloom has saved the bad version of this book as " + badFilePath + ".  Bloom will now exit, and your book will probably not have this recent damage.  If you are willing, please try to do the same steps again, so that you can report exactly how to make it happen.");
				Process.GetCurrentProcess().Kill();
			}
			else
			{
				Logger.WriteMinorEvent("ReplaceFileWithUserInteractionIfNeeded({0},{1})", tempPath, PathToExistingHtml);
				if (!string.IsNullOrEmpty(tempPath))
					FileUtils.ReplaceFileWithUserInteractionIfNeeded(tempPath, PathToExistingHtml, null);
			}

			MetaData.Save();
		}

		private void AssertIsAlreadyInitialized()
		{
			if (_dom == null)
				throw new ApplicationException("BookStorage was at a place that should have been initialized earlier, but wasn't.");
		}


		public string SaveHtml(HtmlDom dom)
		{
			AssertIsAlreadyInitialized();
			string tempPath = Path.GetTempFileName();
			MakeCssLinksAppropriateForStoredFile(dom);
			SetBaseForRelativePaths(dom, string.Empty, false);// remove any dependency on this computer, and where files are on it.
			//CopyXMatterStylesheetsIntoFolder
			return XmlHtmlConverter.SaveDOMAsHtml5(dom.RawDom, tempPath);
		}


		/// <summary>
		/// creates a relocatable copy of our main HtmlDom
		/// </summary>
		/// <param name="log"></param>
		/// <returns></returns>
		public HtmlDom GetRelocatableCopyOfDom(IProgress log)
		{

			HtmlDom relocatableDom = Dom.Clone();

			SetBaseForRelativePaths(relocatableDom, _folderPath, true);
			EnsureHasLinksToStylesheets(relocatableDom);
			UpdateStyleSheetLinkPaths(relocatableDom, _fileLocator, log);

			return relocatableDom;
		}

		/// <summary>
		/// this one works on the dom passed to it
		/// </summary>
		/// <param name="dom"></param>
		/// <param name="log"></param>
		/// <returns></returns>
		public HtmlDom MakeDomRelocatable(HtmlDom dom, IProgress log)
		{
			var relocatableDom = dom.Clone();

			SetBaseForRelativePaths(relocatableDom, _folderPath, true);
			EnsureHasLinksToStylesheets(relocatableDom);
			UpdateStyleSheetLinkPaths(relocatableDom, _fileLocator, log);

			return relocatableDom;
		}


		public void SetBookName(string name)
		{

			if (!Directory.Exists(_folderPath)) //bl-290 (user had 4 month-old version, so the bug may well be long gone)
			{
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem("Bloom has a pesky bug we've been searching for, and you've found it. Most likely, you won't lose any work, but we do need to report the problem and then have you restart. Bloom will now show an error box where you can tell us anything that might help us understand how to reproduce the problem, and let you email it to us.\r\nThanks for your help!");
				throw new ApplicationException(string.Format("In SetBookName('{0}'), BookStorage thinks the existing folder is '{1}', but that does not exist. (ref bl-290)", name, _folderPath));
			}
			name = SanitizeNameForFileSystem(name);

			var currentFilePath = PathToExistingHtml;
			//REVIEW: This doesn't immediataly make sense; if this functino is told to call it Foo but it's current Foo1... why does this just return?

			if (Path.GetFileNameWithoutExtension(currentFilePath).StartsWith(name)) //starts with because maybe we have "myBook1"
				return;

			//figure out what name we're really going to use (might need to add a number suffix)
			var newFolderPath = Path.Combine(Directory.GetParent(FolderPath).FullName, name);
			newFolderPath = GetUniqueFolderPath(newFolderPath);

			Logger.WriteEvent("Renaming html from '{0}' to '{1}.htm'", currentFilePath, newFolderPath);

			//next, rename the file
			File.Move(currentFilePath, Path.Combine(FolderPath, Path.GetFileName(newFolderPath) + ".htm"));

			//next, rename the enclosing folder
			var fromToPair = new KeyValuePair<string, string>(FolderPath, newFolderPath);
			try
			{
				Logger.WriteEvent("Renaming folder from '{0}' to '{1}'", FolderPath, newFolderPath);

				Palaso.IO.DirectoryUtilities.MoveDirectorySafely(FolderPath, newFolderPath);

				_fileLocator.RemovePath(FolderPath);
				_fileLocator.AddPath(newFolderPath);

				_folderPath = newFolderPath;
			}
			catch (Exception e)
			{
				Logger.WriteEvent("Failed folder rename: " + e.Message);
				Debug.Fail("(debug mode only): could not rename the folder");
			}

			_bookRenamedEvent.Raise(fromToPair);

			OnFolderPathChanged();
		}

		protected virtual void OnFolderPathChanged()
		{
			var handler = FolderPathChanged;
			if (handler != null) handler(this, EventArgs.Empty);
		}


		public string GetValidateErrors()
		{
			if (!Directory.Exists(_folderPath))
			{
				return "The directory (" + _folderPath + ") could not be found.";
			}
			if (!File.Exists(PathToExistingHtml))
			{
				return "Could not find an html file to use.";
			}


			return ValidateBook(PathToExistingHtml);
		}

		/// <summary>
		///
		/// </summary>
		/// <remarks>The image-replacement feature is perhaps a one-off for a project where an advisor replaced the folders
		/// with a version that lacked most of the images (perhaps because dropbox copies small files first and didn't complete the sync)</remarks>
		/// <param name="progress"></param>
		/// <param name="pathToFolderOfReplacementImages">We'll find any matches in the entire folder, regardless of sub-folder name</param>
		public void CheckBook(IProgress progress, string pathToFolderOfReplacementImages = null)
		{
			var error = GetValidateErrors();
			if(!string.IsNullOrEmpty(error))
				progress.WriteError(error);

			//check for missing images

			foreach (XmlElement imgNode in Dom.SafeSelectNodes("//img"))
			{
				var imageFileName = imgNode.GetAttribute("src");
				if (string.IsNullOrEmpty(imageFileName))
				{
					var classNames=imgNode.GetAttribute("class");
					if (classNames == null || !classNames.Contains("licenseImage"))//bit of hack... it's ok for licenseImages to be blank
					{
						progress.WriteWarning("image src is missing");
						//review: this, we could fix with a new placeholder... maybe in the javascript edit stuff?
					}
					continue;
				}
				// Certain .svg files (cogGrey.svg, FontSizeLetter.svg) aren't really part of the book and are stored elsewhere.
				// Also, at present the user can't insert them into a book. Don't report them.
				// TODO: if we ever allow the user to add .svg files, we'll need to change this
				if (Path.HasExtension(imageFileName) && Path.GetExtension(imageFileName).ToLowerInvariant() == ".svg")
					continue;

				//trim off the end of "license.png?123243"
				var startOfDontCacheHack = imageFileName.IndexOf('?');
				if (startOfDontCacheHack > -1)
					imageFileName = imageFileName.Substring(0, startOfDontCacheHack);

				while (Uri.UnescapeDataString(imageFileName) != imageFileName)
					imageFileName = Uri.UnescapeDataString(imageFileName);

				if (!File.Exists(Path.Combine(_folderPath, imageFileName)))
				{
					if (!string.IsNullOrEmpty(pathToFolderOfReplacementImages))
					{
						if (!AttemptToReplaceMissingImage(imageFileName, pathToFolderOfReplacementImages, progress))
						{
							progress.WriteWarning(string.Format("Could not find replacement for image {0} in {1}", imageFileName, _folderPath));
						}
					}
					else
					{
						progress.WriteWarning(string.Format("Image {0} is missing from the folder {1}", imageFileName, _folderPath));
					}
				}
			}
		}

		private bool AttemptToReplaceMissingImage(string missingFile, string pathToFolderOfReplacementImages, IProgress progress)
		{
			try
			{
				foreach (var imageFilePath in Directory.GetFiles(pathToFolderOfReplacementImages, missingFile))
				{
					File.Copy(imageFilePath, Path.Combine(_folderPath, missingFile));
					progress.WriteMessage(string.Format("Replaced image {0} from a copy in {1}", missingFile,
														pathToFolderOfReplacementImages));
					return true;
				}
				foreach (var dir in Directory.GetDirectories(pathToFolderOfReplacementImages))
				{
//				    doesn't really matter
//					if (dir == _folderPath)
//				    {
//						progress.WriteMessage("Skipping the directory of this book");
//				    }
					if (AttemptToReplaceMissingImage(missingFile, dir, progress))
						return true;
				}
				return false;
			}
			catch (Exception error)
			{
				progress.WriteException(error);
				return false;
			}
		}

		public void UpdateBookFileAndFolderName(CollectionSettings collectionSettings)
		{
			var title = Dom.Title;
			if (title != null)
			{
				SetBookName(title);
			}
		}


		#region Static Helper Methods
		public static string FindBookHtmlInFolder(string folderPath)
		{
			string p = Path.Combine(folderPath, Path.GetFileName(folderPath) + ".htm");
			if (File.Exists(p))
				return p;

			if (!Directory.Exists(folderPath)) //bl-291 (user had 4 month-old version, so the bug may well be long gone)
			{
				//in version 1.012 I got this because I had tried to delete the folder on disk that had a book
				//open in Bloom.
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem("There's a problem; Bloom can't save this book. Did you perhaps delete or rename the folder that this book is (was) in?");
				throw new ApplicationException(string.Format("In FindBookHtmlInFolder('{0}'), the folder does not exist. (ref bl-291)", folderPath));
			}

			//ok, so maybe they changed the name of the folder and not the htm. Can we find a *single* html doc?
			var candidates = new List<string>(Directory.GetFiles(folderPath, "*.htm"));
			candidates.RemoveAll((name) => name.ToLower().Contains("configuration"));
			if (candidates.Count == 1)
				return candidates[0];

			//template
			p = Path.Combine(folderPath, "templatePages.htm");
			if (File.Exists(p))
				return p;

			return string.Empty;
		}

		public static void SetBaseForRelativePaths(HtmlDom dom, string folderPath, bool pointAtEmbeddedServer)
		{
			string path = "";
			if (!string.IsNullOrEmpty(folderPath))
			{
				if (pointAtEmbeddedServer && Settings.Default.ImageHandler == "http" && ImageServer.IsAbleToUsePort)
				{
					//this is only used by relative paths, and only img src's are left relative.
					//we are redirecting through our build-in httplistener in order to shrink
					//big images before giving them to gecko which has trouble with really hi-res ones
					var uri = folderPath + Path.DirectorySeparatorChar;
					uri = uri.Replace(":", "%3A");
					uri = uri.Replace('\\', '/');
					uri = ImageServer.PathEndingInSlash + uri;
					path = uri;
				}
				else
				{
					path = "file://" + folderPath + Path.DirectorySeparatorChar;
				}
			}
			dom.SetBaseForRelativePaths(path);
		}


		public static string ValidateBook(string path)
		{
			Debug.WriteLine(string.Format("ValidateBook({0})", path));
			var dom = new HtmlDom(XmlHtmlConverter.GetXmlDomFromHtmlFile(path, false));//with throw if there are errors
			var msg= GetMessageIfVersionIsIncompatibleWithThisBloom(dom);
			if (!string.IsNullOrEmpty(msg))
				return msg;
			return dom.ValidateBook(path);
		}



		#endregion


		/// <summary>
		/// Do whatever is needed to do more than just show a title and thumbnail
		/// </summary>
		private void ExpensiveInitialization()
		{
			Debug.WriteLine(string.Format("ExpensiveInitialization({0})", _folderPath));
			_dom = new HtmlDom();
			//the fileLocator we get doesn't know anything about this particular book.
			_fileLocator.AddPath(_folderPath);
			RequireThat.Directory(_folderPath).Exists();
			if (!File.Exists(PathToExistingHtml))
			{
				var files = new List<string>(Directory.GetFiles(_folderPath));
				var b = new StringBuilder();
				b.AppendLine("Could not determine which html file in the folder to use.");
				if (files.Count == 0)
					b.AppendLine("***There are no files.");
				else
				{
					b.AppendLine("Files in this book are:");
					foreach (var f in files)
					{
						b.AppendLine("  " + f);
					}
				}
				throw new ApplicationException(b.ToString());
			}
			else
			{
				var xmlDomFromHtmlFile = XmlHtmlConverter.GetXmlDomFromHtmlFile(PathToExistingHtml, false);
				_dom = new HtmlDom(xmlDomFromHtmlFile); //with throw if there are errors

				//Validating here was taking a 1/3 of the startup time
				// eventually, we need to restructure so that this whole Storage isn't created until actually needed, then maybe this can come back
				//ErrorMessages = ValidateBook(PathToExistingHtml);
				// REVIEW: we did in fact change things so that storage isn't used until we've shown all the thumbnails we can (then we go back and update in background)...
				// so maybe it would be ok to reinstate the above?

				//For now, we really need to do this check, at least. This will get picked up by the Book later (feeling kludgy!)
				//I assume the following will never trigger (I also note that the dom isn't even loaded):


				if (!string.IsNullOrEmpty(ErrorMessages))
				{
					//hack so we can package this for palaso reporting
//                    var ex = new XmlSyntaxException(ErrorMessages);
//                    Palaso.Reporting.ErrorReport.NotifyUserOfProblem(ex, "Bloom did an integrity check of the book named '{0}', and found something wrong. This doesn't mean your work is lost, but it does mean that there is a bug in the system or templates somewhere, and the developers need to find and fix the problem (and your book).  Please click the 'Details' button and send this report to the developers.", Path.GetFileName(PathToExistingHtml));
					_dom.RawDom.LoadXml(
						"<html><body>There is a problem with the html structure of this book which will require expert help.</body></html>");
					Logger.WriteEvent(
						"{0}: There is a problem with the html structure of this book which will require expert help: {1}",
						PathToExistingHtml, ErrorMessages);
				}
					//The following is a patch pushed out on the 25th build after 1.0 was released in order to give us a bit of backwards version protection (I should have done this originally and in a less kludgy fashion than I'm forced to do now)
				else
				{
					var incompatibleVersionMessage = GetMessageIfVersionIsIncompatibleWithThisBloom(Dom);
					if (!string.IsNullOrWhiteSpace(incompatibleVersionMessage))
					{
						_dom.RawDom.LoadXml(
							"<html><body style='background-color: white'><p/><p/><p/>" + WebUtility.HtmlEncode(incompatibleVersionMessage) + "</body></html>");
						Logger.WriteEvent(PathToExistingHtml + " " + incompatibleVersionMessage);
					}
					else
					{
						Logger.WriteEvent("BookStorage Loading Dom from {0}", PathToExistingHtml);
					}
				}

				//TODO: this would be better just to add to those temporary copies of it. As it is, we have to remove it for the webkit printing
				//SetBaseForRelativePaths(Dom, folderPath); //needed because the file itself may be off in the temp directory

				//UpdateStyleSheetLinkPaths(fileLocator);

				Dom.UpdatePageDivs();

				UpdateSupportFiles();
			}
		}

		/// <summary>
		/// we update these so that the file continues to look the same when you just open it in firefox
		/// </summary>
		private void UpdateSupportFiles()
		{
			if (IsPathReadonly(_folderPath))
			{
				Logger.WriteEvent("Not updating files in folder {0} because the directory is read-only.", _folderPath);
			}
			else
			{
				UpdateIfNewer("placeHolder.png");
				UpdateIfNewer("basePage.css");
				UpdateIfNewer("previewMode.css");
				UpdateIfNewer("origami.css");

				foreach (var path in Directory.GetFiles(_folderPath, "*.css"))
				{
					var file = Path.GetFileName(path);
					//if (file.ToLower().Contains("portrait") || file.ToLower().Contains("landscape"))
					UpdateIfNewer(file);
				}
			}

			//by default, this comes from the collection, but the book can select one, inlucing "null" to select the factory-supplied empty xmatter
			var nameOfXMatterPack = _dom.GetMetaValue("xMatter", _collectionSettings.XMatterPackName);
			var helper = new XMatterHelper(_dom, nameOfXMatterPack, _fileLocator);
			UpdateIfNewer(Path.GetFileName(helper.PathToStyleSheetForPaperAndOrientation), helper.PathToStyleSheetForPaperAndOrientation);

		}

		private bool IsPathReadonly(string path)
		{
			return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
		}

		private void UpdateIfNewer(string fileName, string factoryPath = "")
		{
			if (!IsUserFolder)
			{
				if (fileName.ToLower().Contains("xmatter") && ! fileName.ToLower().StartsWith("factory-xmatter"))
				{
					return; //we don't want to copy custom xmatters around to the program files directory, template directories, the Bloom src code folders, etc.
				}
			}

			string documentPath="notSet";
			try
			{
				if(string.IsNullOrEmpty(factoryPath))
				{
					factoryPath = _fileLocator.LocateFile(fileName);
				}
				if(string.IsNullOrEmpty(factoryPath))//happens during unit testing
					return;

				var factoryTime = File.GetLastWriteTimeUtc(factoryPath);
				documentPath = Path.Combine(_folderPath, fileName);
				if(!File.Exists(documentPath))
				{
					Logger.WriteEvent("BookStorage.UpdateIfNewer() Copying missing file {0} to {1}", factoryPath, documentPath);
					File.Copy(factoryPath, documentPath);
					return;
				}
				var documentTime = File.GetLastWriteTimeUtc(documentPath);
				if(factoryTime> documentTime)
				{
					if (IsPathReadonly(documentPath))
					{
						var msg = string.Format("Could not update one of the support files in this document ({0}) because the destination was marked ReadOnly.", documentPath);
						Logger.WriteEvent(msg);
						Palaso.Reporting.ErrorReport.NotifyUserOfProblem(msg);
						return;
					}
					Logger.WriteEvent("BookStorage.UpdateIfNewer() Updating file {0} to {1}", factoryPath, documentPath);

					File.Copy(factoryPath, documentPath,true);
					//if the source was locked, don't copy the lock over
					File.SetAttributes(documentPath,FileAttributes.Normal);
				}
			}
			catch (Exception e)
			{
				if(documentPath.Contains(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles))
					|| documentPath.ToLower().Contains("program"))//english only
				{
					Logger.WriteEvent("Could not update file {0} because it was in the program directory.", documentPath);
					return;
				}
				if(_alreadyNotifiedAboutOneFailedCopy)
					return;//don't keep bugging them
				_alreadyNotifiedAboutOneFailedCopy = true;
				Palaso.Reporting.ErrorReport.NotifyUserOfProblem(e,
					"Could not update one of the support files in this document ({0} to {1}). This is normally because the folder is 'locked' or the file is marked 'read only'.", documentPath, factoryPath);
			}
		}

		/// <summary>
		/// user folder as opposed to our program installation folder or some template
		/// </summary>
		private bool IsUserFolder
		{
			get { return _folderPath.Contains(_collectionSettings.FolderPath); }
		}

		// NB: this knows nothing of book-specific css's... even "basic book.css"
		private void EnsureHasLinksToStylesheets(HtmlDom dom)
		{
			var nameOfXMatterPack = _dom.GetMetaValue("xMatter", _collectionSettings.XMatterPackName);
			var helper = new XMatterHelper(_dom, nameOfXMatterPack, _fileLocator);

			EnsureHasLinkToStyleSheet(dom, Path.GetFileName(helper.PathToStyleSheetForPaperAndOrientation));

			string autocssFilePath = ".."+Path.DirectorySeparatorChar+"settingsCollectionStyles.css";
			if (File.Exists(Path.Combine(_folderPath,autocssFilePath)))
				EnsureHasLinkToStyleSheet(dom, autocssFilePath);

			var customCssFilePath = ".." + Path.DirectorySeparatorChar + "customCollectionStyles.css";
			if (File.Exists(Path.Combine(_folderPath, customCssFilePath)))
				EnsureHasLinkToStyleSheet(dom, customCssFilePath);

			if (File.Exists(Path.Combine(_folderPath, "customBookStyles.css")))
				EnsureHasLinkToStyleSheet(dom, "customBookStyles.css");
			else
				EnsureDoesntHaveLinkToStyleSheet(dom, "customBookStyles.css");
		}

		private void EnsureDoesntHaveLinkToStyleSheet(HtmlDom dom, string path)
		{
			foreach (XmlElement link in dom.SafeSelectNodes("//link[@rel='stylesheet']"))
			{
				var fileName = link.GetStringAttribute("href");
				if (fileName == path)
					dom.RemoveStyleSheetIfFound(path);
			}
		}

		private void EnsureHasLinkToStyleSheet(HtmlDom dom, string path)
		{
			foreach (XmlElement link in dom.SafeSelectNodes("//link[@rel='stylesheet']"))
			{
				var fileName = link.GetStringAttribute("href");
				if (fileName == path)
					return;
			}
			dom.AddStyleSheet(path);
		}

//		/// <summary>
//		/// Creates a relative path from one file or folder to another.
//		/// </summary>
//		/// <param name="fromPath">Contains the directory that defines the start of the relative path.</param>
//		/// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
//		/// <param name="dontEscape">Boolean indicating whether to add uri safe escapes to the relative path</param>
//		/// <returns>The relative path from the start directory to the end path.</returns>
//		/// <exception cref="ArgumentNullException"></exception>
//		public static String MakeRelativePath(String fromPath, String toPath)
//		{
//			if (String.IsNullOrEmpty(fromPath)) throw new ArgumentNullException("fromPath");
//			if (String.IsNullOrEmpty(toPath)) throw new ArgumentNullException("toPath");
//
//			//the stuff later on needs to see directory names trailed by a "/" or "\".
//			fromPath = fromPath.Trim();
//			if (!fromPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
//			{
//				if (Directory.Exists(fromPath))
//				{
//					fromPath = fromPath + Path.DirectorySeparatorChar;
//				}
//			}
//			Uri fromUri = new Uri(fromPath);
//			Uri toUri = new Uri(toPath);
//
//			Uri relativeUri = fromUri.MakeRelativeUri(toUri);
//			String relativePath = Uri.UnescapeDataString(relativeUri.ToString());
//
//			return relativePath.Replace('/', Path.DirectorySeparatorChar);
//		}

		//TODO: Clean up this patch that is being done in emergency mode for a customer with a deadline
		private  void UpdateStyleSheetLinkPaths(HtmlDom dom, IFileLocator fileLocator, IProgress log)
		{
			dom.UpdateStyleSheetLinkPaths(_fileLocator, _folderPath, log);
		}
		public void UpdateStyleSheetLinkPaths(HtmlDom printingDom)
		{
			printingDom.UpdateStyleSheetLinkPaths(_fileLocator, _folderPath, new NullProgress());
		}

		//while in Bloom, we could have and edit style sheet or (someday) other modes. But when stored,
		//we want to make sure it's ready to be opened in a browser.
		private void MakeCssLinksAppropriateForStoredFile(HtmlDom dom)
		{
			dom.RemoveModeStyleSheets();
			dom.AddStyleSheet("previewMode.css");
			dom.AddStyleSheet("basePage.css");
			dom.AddStyleSheet("origami.css");
			EnsureHasLinksToStylesheets(dom);
			dom.SortStyleSheetLinks();
			dom.RemoveFileProtocolFromStyleSheetLinks();
		}


		private string SanitizeNameForFileSystem(string name)
		{
			foreach(char c in PathUtilities.GetInvalidOSIndependentFileNameChars())
			{
				name = name.Replace(c, ' ');
			}
			name = name.Trim();
			const int MAX = 50;//arbitrary
			if(name.Length >MAX)
				return name.Substring(0, MAX);
			return name;
		}

		/// <summary>
		/// if necessary, append a number to make the folder path unique
		/// </summary>
		/// <param name="folderPath"></param>
		/// <returns></returns>
		private string GetUniqueFolderPath(string folderPath)
		{
			int i = 0;
			string suffix = "";
			var parent = Directory.GetParent(folderPath).FullName;
			var name = Path.GetFileName(folderPath);
			while (Directory.Exists(Path.Combine(parent, name + suffix)))
			{
				++i;
				suffix = i.ToString();
			}
			return Path.Combine(parent, name + suffix);
		}
	}

}