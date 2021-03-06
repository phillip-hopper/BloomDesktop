﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Bloom.Collection;
using NUnit.Framework;
using Bloom.Book;
using Palaso.TestUtilities;
using Palaso.UI.WindowsForms.ClearShare;

namespace BloomTests.Book
{
	[TestFixture]
	public sealed class BookDataTests
	{
		private CollectionSettings _collectionSettings;

		[SetUp]
		public void Setup()
		{
			_collectionSettings = new CollectionSettings(new NewCollectionSettings() {
				PathToSettingsFile = CollectionSettings.GetPathForNewSettings(new TemporaryFolder("BookDataTests").Path, "test"),
				Language1Iso639Code = "xyz", Language2Iso639Code = "en", Language3Iso639Code = "fr" });
		}

		[Test]
		public void TextOfInnerHtml_RemovesMarkup()
		{
			var input = "This <em>is</em> the day";
			var output = BookData.TextOfInnerHtml(input);
			Assert.That(output, Is.EqualTo("This is the day"));
		}

		[Test]
		public void TextOfInnerHtml_HandlesXmlEscapesCorrectly()
		{
			var input = "Jack &amp; Jill like xml sequences like &amp;amp; &amp; &amp;lt; &amp; &amp;gt; for characters like &lt;&amp;&gt;";
			var output = BookData.TextOfInnerHtml(input);
			Assert.That(output, Is.EqualTo("Jack & Jill like xml sequences like &amp; & &lt; & &gt; for characters like <&>"));
		}

		[Test]
		public void MakeLanguageUploadData_FindsDefaultInfo()
		{
			var results = _collectionSettings.MakeLanguageUploadData(new[] {"en", "tpi", "xy3"});
			Assert.That(results.Length, Is.EqualTo(3), "should get one result per input");
			VerifyLangData(results[0], "en", "English", "eng");
			VerifyLangData(results[1], "tpi", "Tok Pisin", "tpi");
			VerifyLangData(results[2], "xy3", "xy3", "xy3");
		}

		[Test]
		public void MakeLanguageUploadData_FindsOverriddenNames()
		{
			_collectionSettings.Language1Name = "Cockney";
			// Note: no current way of overriding others; verify they aren't changed.
			var results = _collectionSettings.MakeLanguageUploadData(new[] { "en", "tpi", "xyz" });
			Assert.That(results.Length, Is.EqualTo(3), "should get one result per input");
			VerifyLangData(results[0], "en", "English", "eng");
			VerifyLangData(results[1], "tpi", "Tok Pisin", "tpi");
			VerifyLangData(results[2], "xyz", "Cockney", "xyz");
		}

		void VerifyLangData(LanguageDescriptor lang, string code, string name, string ethCode)
		{
			Assert.That(lang.IsoCode, Is.EqualTo(code));
			Assert.That(lang.Name, Is.EqualTo(name));
			Assert.That(lang.EthnologueCode, Is.EqualTo(ethCode));
		}

	   [Test]
		public void SuckInDataFromEditedDom_NoDataDIvTitleChanged_NewTitleInCache()
	   {
		   HtmlDom bookDom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid2'>
					<textarea lang='xyz' data-book='bookTitle'>original</textarea>
				</div>
			 </body></html>");
		   var data = new BookData(bookDom, _collectionSettings, null);
		   Assert.AreEqual("original", data.GetVariableOrNull("bookTitle", "xyz"));


		   HtmlDom editedPageDom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid2'>
					<textarea lang='xyz' data-book='bookTitle'>changed</textarea>
				</div>
			 </body></html>");

		   data.SuckInDataFromEditedDom(editedPageDom);

		   Assert.AreEqual("changed", data.GetVariableOrNull("bookTitle", "xyz"));
	   }

		/// <summary>
		/// Regression test: the difference between this situation (had a value before) and the one where this is newly discovered was the source of a bug
		/// </summary>
	   [Test]
	   public void SuckInDataFromEditedDom_HasDataDivWithOldTitleThenTitleChanged_NewTitleInCache()
	   {
		   HtmlDom bookDom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
						<div data-book='bookTitle' lang='xyz'>original</div>
				</div>
				<div class='bloom-page' id='guid2'>
					<textarea lang='xyz' data-book='bookTitle'>original</textarea>
				</div>
			 </body></html>");

		   var data = new BookData(bookDom, _collectionSettings, null);
		   Assert.AreEqual("original", data.GetVariableOrNull("bookTitle", "xyz"));

		   HtmlDom editedPageDom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid2'>
					<textarea lang='xyz' data-book='bookTitle'>changed</textarea>
				</div>
			 </body></html>");

		   data.SuckInDataFromEditedDom(editedPageDom);

		   Assert.AreEqual("changed", data.GetVariableOrNull("bookTitle", "xyz"));
	   }

		[Test]
		public void UpdateFieldsAndVariables_CustomLibraryVariable_CopiedToOtherElement()
		{
			var dom=new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid3'>
					<p>
						<textarea lang='xyz' id='copyOfVTitle'  data-book='bookTitle'>tree</textarea>
						<textarea lang='xyz' id='1' data-collection='testLibraryVariable'>aa</textarea>
					   <textarea lang='xyz' id='2'  data-collection='testLibraryVariable'>bb</textarea>
					</p>
				</div>
				</body></html>");
			var data = new BookData(dom, _collectionSettings, null);
			data.UpdateVariablesAndDataDivThroughDOM();
			var textarea2 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@id='2']");
			Assert.AreEqual("aa", textarea2.InnerText);
		}


		[Test]
		public void UpdateFieldsAndVariables_VernacularTitleChanged_TitleCopiedToParagraphAnotherPage()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid2'>
						<p>
							<textarea lang='xyz' data-book='bookTitle'>original</textarea>
						</p>
				</div>
				<div class='bloom-page' id='0a99fad3-0a17-4240-a04e-86c2dd1ec3bd'>
						<p class='centered' lang='xyz' data-book='bookTitle' id='P1'>originalButNoExactlyCauseItShouldn'tMatter</p>
				</div>
			 </body></html>");
			var data = new BookData(dom,  _collectionSettings, null);
			var textarea1 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@data-book='bookTitle' and @lang='xyz']");
			textarea1.InnerText = "peace & quiet";
			data.SynchronizeDataItemsThroughoutDOM();
			var paragraph = dom.SelectSingleNodeHonoringDefaultNS("//p[@data-book='bookTitle'  and @lang='xyz']");
			Assert.AreEqual("peace & quiet", paragraph.InnerText);
		}


		[Test]
		public void UpdateFieldsAndVariables_OneDataItemChanges_ItemsWithThatLanguageAlsoUpdated()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid1'>
					<p>
						<textarea lang='en' id='1'  data-book='bookTitle'>EnglishTitle</textarea>
						<textarea lang='xyz' id='2'  data-book='bookTitle'>xyzTitle</textarea>
					</p>
				</div>
				<div class='bloom-page' id='guid3'>
					<p>
						<textarea lang='xyz' id='3'  data-book='bookTitle'>xyzTitle</textarea>
					</p>
				</div>
			 </body></html>");
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//textarea[@lang='en' and @id='1' and text()='EnglishTitle']", 1);
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//textarea[@lang='xyz'  and @id='2' and text()='xyzTitle']", 1);
			var textarea2 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@id='2']");
			textarea2.InnerText = "newXyzTitle";
			var data = new BookData(dom, new CollectionSettings() { Language1Iso639Code = "etr" }, null);
			data.SynchronizeDataItemsThroughoutDOM();
			var textarea3 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@id='3']");
			Assert.AreEqual("newXyzTitle", textarea3.InnerText);
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//textarea[@id='1' and text()='EnglishTitle']", 1);
		}

		[Test]
		public void UpdateFieldsAndVariables_EnglishDataItemChanges_VernItemsUntouched()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page' id='guid1'>
					<p>
						<textarea lang='en' id='1'  data-book='bookTitle'>EnglishTitle</textarea>
						<textarea lang='xyz' id='2'  data-book='bookTitle'>xyzTitle</textarea>
					</p>
				</div>
				<div class='bloom-page' id='guid3'>
					<p>
						<textarea lang='xyz' id='3'  data-book='bookTitle'>xyzTitle</textarea>
					</p>
				</div>
			 </body></html>");
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//textarea[@lang='en' and @id='1' and text()='EnglishTitle']", 1);
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//textarea[@lang='xyz'  and @id='2' and text()='xyzTitle']", 1);
			var textarea1 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@id='1']");
			textarea1.InnerText = "newEnglishTitle";
			var data = new BookData(dom,   new CollectionSettings(){Language1Iso639Code = "etr"}, null);
			data.SynchronizeDataItemsThroughoutDOM();
			var textarea2 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@id='2']");
			Assert.AreEqual("xyzTitle", textarea2.InnerText);
			var textarea3 = dom.SelectSingleNodeHonoringDefaultNS("//textarea[@id='3']");
			Assert.AreEqual("xyzTitle", textarea3.InnerText);
		}


		[Test]
		public void UpdateFieldsAndVariables_BookTitleInSpanOnSecondPage_UpdatesH2OnFirstWithCurrentNationalLang()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div class='bloom-page titlePage'>
						<div class='pageContent'>
							<h2 data-book='bookTitle' lang='N1'>{national book title}</h2>
						</div>
					</div>
				<div class='bloom-page verso'>
					<div class='pageContent'>
						(<span lang='en' data-book='bookTitle'>Vaccinations</span><span lang='tpi' data-book='bookTitle'>Tambu Sut</span>)
						<br />
					</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings()
				{
					Language1Iso639Code = "etr"
				};
			var data = new BookData(dom,   collectionSettings, null);
			data.SynchronizeDataItemsThroughoutDOM();
			XmlElement nationalTitle = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//h2[@data-book='bookTitle']");
			Assert.AreEqual("Vaccinations", nationalTitle.InnerText);

			//now switch the national language to Tok Pisin

			collectionSettings.Language2Iso639Code = "tpi";
			data.SynchronizeDataItemsThroughoutDOM();
			nationalTitle = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//h2[@data-book='bookTitle']");
			Assert.AreEqual("Tambu Sut", nationalTitle.InnerText);
		}

		[Test]
		public void GetMultilingualContentLanguage_ContentLanguageSpecifiedInHtml_ReadsIt()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
						<div data-book='contentLanguage2'>fr</div>
						<div data-book='contentLanguage3'>es</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings();
			var data = new BookData(dom,   collectionSettings, null);
			Assert.AreEqual("fr", data.MultilingualContentLanguage2);
			Assert.AreEqual("es", data.MultilingualContentLanguage3);
		}
		[Test]
		public void SetMultilingualContentLanguage_ContentLanguageSpecifiedInHtml_ReadsIt()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
						<div data-book='contentLanguage2'>fr</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings();
			var data = new BookData(dom,   collectionSettings, null);
			data.SetMultilingualContentLanguages("en", "de");
			Assert.AreEqual("en", data.MultilingualContentLanguage2);
			Assert.AreEqual("de", data.MultilingualContentLanguage3);
		}
		[Test]
		public void UpdateVariablesAndDataDivThroughDOM_NewLangAdded_AddedToDataDiv()
		{
			var dom = new HtmlDom(@"<html><head></head><body><div data-book='someVariable' lang='en'>hi</div></body></html>");

			var e = dom.RawDom.CreateElement("div");
			e.SetAttribute("data-book", "someVariable");
			e.SetAttribute("lang", "fr");
			e.InnerText = "bonjour";
			dom.RawDom.SelectSingleNode("//body").AppendChild(e);
			var data = new BookData(dom,   new CollectionSettings(), null);
			data.UpdateVariablesAndDataDivThroughDOM();
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//body/div[1][@id='bloomDataDiv']", 1);//NB microsoft uses 1 as the first. W3c uses 0.
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@id='bloomDataDiv']/div[@data-book='someVariable' and @lang='en' and text()='hi']", 1);
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@id='bloomDataDiv']/div[@data-book='someVariable' and @lang='fr' and text()='bonjour']", 1);
		}

		[Test]
		public void UpdateVariablesAndDataDivThroughDOM_HasDataLibraryValues_LibraryValuesNotPutInDataDiv()
		{
			var dom = new HtmlDom(@"<html><head></head><body><div data-book='someVariable' lang='en'>hi</div><div data-collection='user' lang='en'>john</div></body></html>");
			var data = new BookData(dom,   new CollectionSettings(), null);
			data.UpdateVariablesAndDataDivThroughDOM();
			AssertThatXmlIn.Dom(dom.RawDom).HasNoMatchForXpath("//div[@id='bloomDataDiv']/div[@data-book='user']");
			AssertThatXmlIn.Dom(dom.RawDom).HasNoMatchForXpath("//div[@id='bloomDataDiv']/div[@data-collection]");
		}

		[Test]
		public void UpdateVariablesAndDataDivThroughDOM_DoesNotExist_MakesOne()
		{
			var dom = new HtmlDom(@"<html><head></head><body><div data-book='someVariable'>world</div></body></html>");
			var data = new BookData(dom,   new CollectionSettings(), null);
			data.UpdateVariablesAndDataDivThroughDOM();
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//body/div[1][@id='bloomDataDiv']", 1);//NB microsoft uses 1 as the first. W3c uses 0.
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@id='bloomDataDiv']/div[@data-book='someVariable' and text()='world']", 1);
		}


		[Test]
		public void SetMultilingualContentLanguages_HasTrilingualLanguages_AddsToDataDiv()
		{
			var dom = new HtmlDom(@"<html><head></head><body></body></html>");
			var data = new BookData(dom,  new CollectionSettings(), null);
			data.SetMultilingualContentLanguages("okm", "kbt");
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@id='bloomDataDiv']/div[@data-book='contentLanguage2' and text()='okm']", 1);
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@id='bloomDataDiv']/div[@data-book='contentLanguage3' and text()='kbt']", 1);
		}
		[Test]
		public void SetMultilingualContentLanguages_ThirdContentLangTurnedOff_RemovedFromDataDiv()
		{
			var dom = new HtmlDom(@"<html><head><div id='bloomDataDiv'><div data-book='contentLanguage2'>xyz</div><div data-book='contentLanguage3'>kbt</div></div></head><body></body></html>");
			var data = new BookData(dom,  new CollectionSettings(), null);
			data.SetMultilingualContentLanguages(null,null);
			AssertThatXmlIn.Dom(dom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@id='bloomDataDiv']/div[@data-book='contentLanguage3']", 0);
		}


		[Test]
		public void Constructor_CollectionSettingsHasCountrProvinceDistrict_LanguageLocationFilledIn()
		{
//            var dom = new HtmlDom(@"<html><head><div id='bloomDataDiv'>
//                    <div data-book='country'>the country</div>
//                    <div data-book='province'>the province</div>
//                    <div data-book='district'>the district</div>
//            </div></head><body></body></html>");
			var dom = new HtmlDom();
			var data = new BookData(dom, new CollectionSettings(){Country="the country", Province = "the province", District= "the district"}, null);
			Assert.AreEqual("the district, the province, the country", data.GetVariableOrNull("languageLocation", "*"));
		  }

		/*    data.AddLanguageString("*", "nameOfLanguage", collectionSettings.Language1Name, true);
				data.AddLanguageString("*", "nameOfNationalLanguage1",
									   collectionSettings.GetLanguage2Name(collectionSettings.Language2Iso639Code), true);
				data.AddLanguageString("*", "nameOfNationalLanguage2",
									   collectionSettings.GetLanguage3Name(collectionSettings.Language2Iso639Code), true);
				data.AddGenericLanguageString("iso639Code", collectionSettings.Language1Iso639Code, true);*/

		[Test]
		public void Constructor_CollectionSettingsHasISO639Code_iso639CodeFilledIn()
		{
			var dom = new HtmlDom();
			var data = new BookData(dom, new CollectionSettings() { Language1Iso639Code = "xyz" }, null);
			Assert.AreEqual("xyz", data.GetVariableOrNull("iso639Code", "*"));
		}
		[Test]
		public void Constructor_CollectionSettingsHasISO639Code_DataSetContainsProperV()
		{
			var dom = new HtmlDom();
			var data = new BookData(dom, new CollectionSettings() { Language1Iso639Code = "xyz" }, null);
			Assert.AreEqual("xyz", data.GetWritingSystemCodes()["V"]);
		}
		[Test]
		public void Constructor_CollectionSettingsHasLanguage1Name_LanguagenameOfNationalLanguage1FilledIn()
		{
			var dom = new HtmlDom();
			var data = new BookData(dom, new CollectionSettings() { Language1Name = "foobar" }, null);
			Assert.AreEqual("foobar", data.GetVariableOrNull("nameOfLanguage", "*"));
		}

		//NB: yes, this is confusing, having lang1 = language, lang2 = nationalLang1, lang3 = nationalLang2

		[Test]
		public void Constructor_CollectionSettingsHasLanguage2Iso639Code_nameOfNationalLanguage1FilledIn()
		{
			var dom = new HtmlDom();
			var data = new BookData(dom, new CollectionSettings() { Language2Iso639Code = "tpi" }, null);
			Assert.AreEqual("Tok Pisin", data.GetVariableOrNull("nameOfNationalLanguage1", "*"));
		}
		[Test]
		public void Constructor_CollectionSettingsHasLanguage3Iso639Code_nameOfNationalLanguage2FilledIn()
		{
			var dom = new HtmlDom();
			var data = new BookData(dom, new CollectionSettings() { Language3Iso639Code = "tpi" }, null);
			Assert.AreEqual("Tok Pisin", data.GetVariableOrNull("nameOfNationalLanguage2", "*"));
		}

		[Test]
		public void Set_DidNotHaveForm_Added()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			Assert.AreEqual("one", data.GetVariableOrNull("1", "en"));
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@lang='en']",1);
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			var t = roundTripData.GetVariableOrNull("1", "en");
			Assert.AreEqual("one", t);
		}

		[Test]
		public void Set_AddTwoForms_BothAdded()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			data.Set("1", "uno", "es");
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			Assert.AreEqual("one", roundTripData.GetVariableOrNull("1", "en"));
			Assert.AreEqual("uno", roundTripData.GetVariableOrNull("1", "es"));
		}

		[Test]
		public void Set_DidHaveForm_StillJustOneCopy()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			data.Set("1", "one", "en");
			Assert.AreEqual("one", data.GetVariableOrNull("1", "en"));
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@lang='en']", 1);
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			var t = roundTripData.GetVariableOrNull("1", "en");
			Assert.AreEqual("one", t);
		}

		[Test]
		public void Set_EmptyString_Removes()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			data.Set("1", "", "en");
			Assert.AreEqual(null, data.GetVariableOrNull("1", "en"));
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@lang='en']", 0);
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			Assert.IsNull(roundTripData.GetVariableOrNull("1", "en"));
		}

		[Test]
		public void Set_Null_Removes()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			data.Set("1", null, "en");
			Assert.AreEqual(null, data.GetVariableOrNull("1", "en"));
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@lang='en']", 0);
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			Assert.IsNull(roundTripData.GetVariableOrNull("1", "en"));
		}

		[Test]
		public void RemoveSingleForm_HasForm_Removed()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1","one","en");
			var data2 = new BookData(htmlDom, new CollectionSettings(), null);
			data2.RemoveSingleForm("1","en");
			Assert.IsNull(data2.GetVariableOrNull("1", "en"));
		}

		[Test]
		public void RemoveDataDivVariableForOneLanguage_DoesNotHaveForm_OK()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.RemoveSingleForm("1", "en");
			Assert.AreEqual(null, data.GetVariableOrNull("1", "en"));
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("//div[@lang='en']", 0);
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			Assert.IsNull(roundTripData.GetVariableOrNull("1", "en"));
		}

		[Test]
		public void RemoveDataDivVariableForOneLanguage_WasLastForm_WholeElementRemoved()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1","one","en");
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			roundTripData.RemoveSingleForm("1", "en");
			Assert.IsNull(roundTripData.GetVariableOrNull("1", "en"));

		}


		[Test]
		public void RemoveDataDivVariableForOneLanguage_WasTwoForms_OtherRemains()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			data.Set("1", "uno", "es");
			var roundTripData = new BookData(htmlDom, new CollectionSettings(), null);
			roundTripData.RemoveSingleForm("1", "en");
			Assert.IsNull(roundTripData.GetVariableOrNull("1", "en"));
			Assert.AreEqual("uno",roundTripData.GetVariableOrNull("1","es"));
		}


		[Test]
		public void Set_CalledTwiceWithDIfferentLangs_HasBoth()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1", "one", "en");
			data.Set("1", "uno", "es");
			Assert.AreEqual(2,data.GetMultiTextVariableOrEmpty("1").Forms.Count());
		}

		[Test]
		public void UpdateVariablesAndDataDivThroughDOM_VariableIsNull_DataDivForItRemoved()
		{
			var htmlDom = new HtmlDom();
			var data = new BookData(htmlDom, new CollectionSettings(), null);
			data.Set("1","one","en");
			data.Set("1", null, "es");
			data.UpdateVariablesAndDataDivThroughDOM();
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("html/body/div/div[@lang='en']",1);
			AssertThatXmlIn.Dom(htmlDom.RawDom).HasSpecifiedNumberOfMatchesForXpath("html/body/div/div[@lang='es']", 0);
		}

		[Test]
		public void PrettyPrintLanguage_DoesNotModifyUnknownCodes()
		{
			var htmlDom = new HtmlDom();
			var settingsettings = new CollectionSettings() { Language1Iso639Code = "pdc", Language1Name = "German, Kludged" };
			var data = new BookData(htmlDom, settingsettings, null);
			Assert.That(data.PrettyPrintLanguage("xyz"), Is.EqualTo("xyz"));
		}

		[Test]
		public void PrettyPrintLanguage_AdjustsLang1()
		{
			var htmlDom = new HtmlDom();
			var settingsettings = new CollectionSettings() {Language1Iso639Code = "pdc", Language1Name = "German, Kludged"};
			var data = new BookData(htmlDom, settingsettings, null);
			Assert.That(data.PrettyPrintLanguage("pdc"), Is.EqualTo("German, Kludged"));
		}

		[Test]
		public void PrettyPrintLanguage_AdjustsKnownLanguages()
		{
			var htmlDom = new HtmlDom();
			var settingsettings = new CollectionSettings() { Language1Iso639Code = "pdc", Language1Name = "German, Kludged", Language2Iso639Code = "de", Language3Iso639Code = "fr"};
			var data = new BookData(htmlDom, settingsettings, null);
			Assert.That(data.PrettyPrintLanguage("de"), Is.EqualTo("German"));
			Assert.That(data.PrettyPrintLanguage("fr"), Is.EqualTo("French"));
			Assert.That(data.PrettyPrintLanguage("en"), Is.EqualTo("English"));
			Assert.That(data.PrettyPrintLanguage("es"), Is.EqualTo("Spanish"));
		}

		#region Metadata
		[Test]
		public void GetLicenseMetadata_HasCustomLicense_RightsStatementContainsCustom()
		{
			string dataDivContent= @"<div lang='en' data-book='licenseNotes'>my custom</div>
					<div data-book='copyright' class='bloom-content1'>Copyright © 2012, test</div>";
			Assert.AreEqual("my custom", GetMetadata(dataDivContent).License.RightsStatement);
		}
		[Test]
		public void GetLicenseMetadata_HasCCLicenseURL_ConvertedToFulCCLicenseObject()
		{
			//nb: the real testing is done on the palaso class that does the reading, this is just a quick sanity check
			string dataDivContent = @"<div lang='en' data-book='licenseUrl'>http://creativecommons.org/licenses/by-nc-sa/3.0/</div>";
			var creativeCommonsLicense = (CreativeCommonsLicense) (GetMetadata(dataDivContent).License);
			Assert.IsTrue(creativeCommonsLicense.AttributionRequired);
			Assert.IsFalse(creativeCommonsLicense.CommercialUseAllowed);
			Assert.IsTrue(creativeCommonsLicense.DerivativeRule== CreativeCommonsLicense.DerivativeRules.DerivativesWithShareAndShareAlike);
		}
		[Test]
		public void GetLicenseMetadata_NullLicense_()
		{
			//nb: the real testing is done on the palaso class that does the reading, this is just a quick sanity check
			string dataDivContent = @"<div lang='en' data-book='licenseDescription'>This could say anthing</div>";
			Assert.IsTrue(GetMetadata(dataDivContent).License is NullLicense);
		}

		[Test]
		public void GetLicenseMetadata_HasSymbolInCopyright_FullCopyrightStatmentAcquired()
		{
			string dataDivContent = @"<div data-book='copyright' class='bloom-content1'>Copyright © 2012, test</div>";
			Assert.AreEqual("Copyright © 2012, test", GetMetadata(dataDivContent).CopyrightNotice);
		}

		private static Metadata GetMetadata(string dataDivContent)
		{
			var dom = new HtmlDom(@"<html><head><div id='bloomDataDiv'>" + dataDivContent + "</div></head><body></body></html>");
			var data = new BookData(dom, new CollectionSettings(), null);
			return data.GetLicenseMetadata();
		}

		#endregion

		#region Copying data across languages, where it seems the lesser of two evils

//		[Test]
//		public void SynchronizeDataItemsThroughoutDOM_EnglishTitleButNoVernacular_DoesNotCopyInEnglish()
//		{
//			var dom = new HtmlDom(@"<html ><head></head><body>
//                <div id='bloomDataDiv'>
//                     <div data-book='bookTitle' lang='en'>the title</div>
//                </div>
//                <div class='bloom-page verso'>
//					 <div id='originalContributions' class='bloom-translationGroup'>
//						<div data-book='originalContributions' lang='etr'></div>
//						<div data-book='originalContributions' lang='en'></div>
//					</div>
//                </div>
//                </body></html>");
//			var collectionSettings = new CollectionSettings()
//			{
//				Language1Iso639Code = "fr"
//			};
//			var data = new BookData(dom, collectionSettings, null);
//			data.SynchronizeDataItemsThroughoutDOM();
//			XmlElement englishContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='en']");
//			Assert.AreEqual("the contributions", englishContributions.InnerText, "Should copy English into body of course, as normal");
//			XmlElement frenchContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='fr']");
//			Assert.AreEqual("the contributions", frenchContributions.InnerText, "Should copy English into French Contributions becuase it's better than just showing nothing");
//		}


		[Test]
		public void SynchronizeDataItemsThroughoutDOM_HasOnlyEnglishContributorsButEnglishIsLang3_CopiesEnglishIntoNationalLanguageSlot()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
					 <div data-book='originalContributions' lang='en'>the contributions</div>
				</div>
				<div class='bloom-page verso'>
					 <div id='originalContributions' class='bloom-translationGroup'>
						<div  class='bloom-copyFromOtherLanguageIfNecessary'  data-book='originalContributions' lang='fr'></div>
						<div  class='bloom-copyFromOtherLanguageIfNecessary'  data-book='originalContributions' lang='en'></div>
					</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings()
				{
					  Language1Iso639Code = "etr",
					  Language2Iso639Code = "fr"
				};
			var data = new BookData(dom, collectionSettings, null);
			data.SynchronizeDataItemsThroughoutDOM();
			XmlElement englishContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='en']");
			Assert.AreEqual("the contributions", englishContributions.InnerText, "Should copy English into body of course, as normal");
			XmlElement frenchContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='fr']");
			Assert.AreEqual("the contributions", frenchContributions.InnerText, "Should copy English into French Contributions becuase it's better than just showing nothing");
			//Assert.AreEqual("en",frenchContributions.GetAttribute("bloom-languageBloomHadToCopyFrom"),"Should have left a record that we did this dubious 'borrowing' from English");
		}



		[Test]
		public void SynchronizeDataItemsThroughoutDOM_HasOnlyEnglishContributorsInDataDivButFrenchInBody_DoesNotCopyEnglishIntoFrenchSlot()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
					 <div data-book='originalContributions' lang='en'>the contributions</div>
				</div>
				<div class='bloom-page verso'>
					 <div id='originalContributions' class='bloom-translationGroup'>
						<div data-book='originalContributions' lang='fr'>les contributeurs</div>
						<div data-book='originalContributions' lang='xyz'></div>
					</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings()
			{
				Language1Iso639Code = "etr",
				Language2Iso639Code = "fr"
			};
			var data = new BookData(dom, collectionSettings, null);
			data.SynchronizeDataItemsThroughoutDOM();
			XmlElement frenchContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='fr']");
			Assert.AreEqual("les contributeurs", frenchContributions.InnerText, "Should not touch existing French Contributions");
			//Assert.IsFalse(frenchContributions.HasAttribute("bloom-languageBloomHadToCopyFrom"));
		}


		[Test]
		public void SynchronizeDataItemsThroughoutDOM_HasFrenchAndEnglishContributorsInDataDiv_DoesNotCopyEnglishIntoFrenchSlot()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
					 <div data-book='originalContributions' lang='en'>the contributions</div>
					<div data-book='originalContributions' lang='fr'>les contributeurs</div>
				</div>
				<div class='bloom-page verso'>
					 <div id='originalContributions' class='bloom-translationGroup'>
						<div data-book='originalContributions' lang='fr'></div>
						<div data-book='originalContributions' lang='xyz'></div>
					</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings()
			{
				Language1Iso639Code = "xyz",
				Language2Iso639Code = "fr"
			};
			var data = new BookData(dom, collectionSettings, null);
			data.SynchronizeDataItemsThroughoutDOM();
			XmlElement frenchContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='fr']");
			Assert.AreEqual("les contributeurs", frenchContributions.InnerText, "Should use the French, not the English even though the French in the body was empty");
			XmlElement vernacularContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='xyz']");
			Assert.AreEqual("", vernacularContributions.InnerText, "Should not copy Edolo into Vernacualr Contributions. Only national language fields get this treatment");
		}

		[Test]
		public void SynchronizeDataItemsThroughoutDOM_HasOnlyEdoloContributors_CopiesItIntoL2ButNotL1()
		{
			var dom = new HtmlDom(@"<html ><head></head><body>
				<div id='bloomDataDiv'>
					 <div data-book='originalContributions' lang='etr'>the contributions</div>
				</div>
				<div class='bloom-page verso'>
					 <div id='originalContributions' class='bloom-translationGroup'>
						<div class='bloom-copyFromOtherLanguageIfNecessary' data-book='originalContributions' lang='fr'></div>
						<div  class='bloom-copyFromOtherLanguageIfNecessary'  data-book='originalContributions' lang='xyz'></div>
					</div>
				</div>
				</body></html>");
			var collectionSettings = new CollectionSettings()
			{
					  Language1Iso639Code = "xyz",
					  Language2Iso639Code = "fr"
			};
			var data = new BookData(dom, collectionSettings, null);
			data.SynchronizeDataItemsThroughoutDOM();
			XmlElement frenchContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='fr']");
			Assert.AreEqual("the contributions", frenchContributions.InnerText, "Should copy Edolo into French Contributions becuase it's better than just showing nothing");
			XmlElement vernacularContributions = (XmlElement)dom.SelectSingleNodeHonoringDefaultNS("//*[@data-book='originalContributions' and @lang='xyz']");
			Assert.AreEqual("", vernacularContributions.InnerText, "Should not copy Edolo into Vernacualr Contributions. Only national language fields get this treatment");
		}

		#endregion
	}
}
