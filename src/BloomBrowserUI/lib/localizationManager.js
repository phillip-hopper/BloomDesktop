/// <reference path="jquery.d.ts" />
/// <reference path="misc-types.d.ts" />
/**
* L10NSharp LocalizationManager for javascript.
*
* For Bloom 2.0: Since the javascript files are being loaded using file:// urls, ajax is not possible
* so the dictionary is loaded into the DOM using the C# class RuntimeInformationInjector, and into the
* localizationManager using localizationManager.loadStringsFromObject().
*
* For Bloom 2.1 and following: The javascript files are being loaded using http://localhost, so it is
* now possible to load the dictionary using ajax through the C# EnhancedImageServer class. The dictionary
* is retrieved and loaded by calling localizationManager.loadStrings().
*
* Bloom 2.0 String Localization steps:
*   1. C# RuntimeInformationInjector.AddUIDictionaryToDom() is called when loading an page into the editor, which
*      retrieves all the localized strings that may be needed by the editing page, using L10NSharp, and loads them
*      into a script tag that defines the JS function GetInlineDictionary().
*   2. C# RuntimeInformationInjector.AddLocalizationTriggerToDom() is called, adding a script tag to the page that
*      instructs the browser control to gather all elements that have a "data-i18n" attribute once the document has
*      finished loading, and run the jQuery.fn.localize() function.
*   3. C# loads the resulting html into a GeckoFx browser control.
*   4. The browser executes the trigger script once the document has finished loading.
*   5. The JS function jQuery.fn.localize() loops through each element with a "data-i18n" attribute and passes it to
*      localizationManager.setElementText().
*   6. The JS function localizationManager.setElementText() reads the stringId from the "data-i18n" attribute and uses
*      it to search for the localized text in the dictionary created in step 1.
*   7. There is a separate function, localizationManager.getLocalizedHint(), for localizing the text of hint bubbles
*      because sometimes there is a {lang} tag in the hint text that needs to be substituted.
*   8. Strings that are generated dynamically by JS can also be localized by the localizationManager. An example of
*      this is found in StyleEditor.GetToolTip(), where the tip for the font size changer is built.
*/
var LocalizationManager = (function () {
    function LocalizationManager() {
        this.inlineDictionaryLoaded = false;
        if (typeof document['getIframeChannel'] === 'function')
            this.dictionary = document['getIframeChannel']().localizationManagerDictionary;
        else
            this.dictionary = {};
    }
    /**
    * Retrieves localized strings from the server
    * Used in Bloom 2.1
    * @param {Object} [keyValuePairs] Optional. Each property name (i.e. keyValuePairs.keys) is a string id, and the
    * property value is the default value/english text. If keyValuePairs is omitted, all dictionary entries will be
    * returned, otherwise only the requested entries will be returned.
    * @param [elementsToLocalize]
    * @param {Function} [callbackDone] Optional function to call when done.
    */
    LocalizationManager.prototype.loadStrings = function (keyValuePairs, elementsToLocalize, callbackDone) {
        // NOTE: This function is not used in Bloom 2.0, but will be used in Bloom 2.1
        var ajaxSettings = { type: 'POST', url: '/bloom/i18n/loadStrings' };
        if (keyValuePairs)
            ajaxSettings['data'] = keyValuePairs;

        $.ajax(ajaxSettings).done(function (data) {
            localizationManager.dictionary = $.extend(localizationManager.dictionary, data);

            // if callback is passes without a list of elements to localize...
            if (typeof elementsToLocalize === 'function') {
                elementsToLocalize();
            } else if (elementsToLocalize) {
                $(elementsToLocalize).each(function () {
                    localizationManager.setElementText(this);
                });
                if (typeof callbackDone === 'function')
                    callbackDone();
            } else if (typeof callbackDone === 'function')
                callbackDone();
        });
    };

    /**
    * Set dictionary values from an object.
    * Used in Bloom 2.0
    * @param {Object} keyValuePairObject
    */
    LocalizationManager.prototype.loadStringsFromObject = function (keyValuePairObject) {
        // TODO: Evaluate if this function is needed in Bloom 2.1
        this.dictionary = keyValuePairObject;
    };

    /**
    * Gets translated text.
    * Additional parameters after the englishText are treated as arguments for SimpleDotNetFormat.
    * @param {String} stringId
    * @param {String} [englishText]
    * @param [args]
    * @returns {String}
    */
    LocalizationManager.prototype.getText = function (stringId, englishText) {
        var args = [];
        for (var _i = 0; _i < (arguments.length - 2); _i++) {
            args[_i] = arguments[_i + 2];
        }
        if ((!this.inlineDictionaryLoaded) && (typeof GetInlineDictionary === 'function')) {
            if (Object.keys(this.dictionary).length == 0) {
                this.inlineDictionaryLoaded = true;
                $.extend(localizationManager.dictionary, GetInlineDictionary());
            }
        }

        // check if englishText is missing
        englishText = englishText || stringId;

        // get the translation
        var text = this.dictionary[stringId];
        if (!text) {
            text = this.dictionary[stringId.replace('&', '&amp;')];
        }

        // try to get from L10NSharp
        if (!text) {
            var ajaxSettings = { type: 'POST', url: '/bloom/i18n/loadStrings' };
            var pair = {};
            pair[stringId] = englishText;
            ajaxSettings['data'] = pair;

            $.ajax(ajaxSettings).done(function (data) {
                localizationManager.dictionary = $.extend(localizationManager.dictionary, data);
            });

            text = englishText;
        }

        // is this a string.format style request?
        if (args.length > 0) {
            // Do the formatting.
            text = SimpleDotNetFormat(text, args);
        }

        return HtmlDecode(text);
    };

    /**
    * Sets the translated text of element.
    * @param element
    */
    LocalizationManager.prototype.setElementText = function (element) {
        var key = element.dataset['i18n'];
        if (!key)
            return;

        var elem = $(element);
        var text = this.getText(key, elem.html());

        if (text)
            elem.html(text);
    };

    /**
    * Hints sometimes have a {lang} tag in the text that needs to be substituted.
    * Replaces {0}, {1} ... {n} with the corresponding elements of the args array.
    * @param {String} whatToSay
    * @param {element} targetElement
    * @returns {String}
    */
    LocalizationManager.prototype.getLocalizedHint = function (whatToSay, targetElement) {
        var args = Array.prototype.slice.call(arguments);
        args[1] = null; //we're passing null into the gettext englishText arg

        // this awkward, fragile method call sends along the 2 fixed arguments
        // to the method, plus any extra arguments we might have been called with,
        // as parameters for a  c#-style template string
        var translated = this.getText.apply(this, args);

        // stick in the language
        if (translated.indexOf('{lang}') != -1) {
            //This is the preferred approach, but it's not working yet.
            //var languageName = localizationManager.dictionary[$(targetElement).attr('lang')];
            var languageName = GetInlineDictionary()[$(targetElement).attr('lang')];
            translated = translated.replace("{lang}", languageName);
        }

        return translated;
    };

    LocalizationManager.prototype.getVernacularLang = function () {
        return this.getText('vernacularLang');
    };

    LocalizationManager.prototype.getLanguageName = function (iso) {
        return this.getText(iso);
    };
    return LocalizationManager;
})();

var localizationManager = new LocalizationManager();

/**
* Return a formatted string.
* Replaces {0}, {1} ... {n} with the corresponding elements of the args array.
* @param {String} format
* @param {String[]} args
* @returns {String}
*/
function SimpleDotNetFormat(format, args) {
    for (var i = 0; i < args.length; i++) {
        var regex = new RegExp('\\{' + i + '\\}', 'g');
        format = format.replace(regex, args[i]);
    }
    return format;
}

/**
* Returns a string where html entities have been convert to normal text
* @param {String} text
*/
function HtmlDecode(text) {
    var div = document.createElement('div');
    div.innerHTML = text;
    return div.firstChild.nodeValue;
}
//# sourceMappingURL=localizationManager.js.map
