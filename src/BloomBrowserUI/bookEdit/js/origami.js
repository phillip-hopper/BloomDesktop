$(function() {
    $('div.split-pane').splitPane();
});
$(window).load(function() {
//    $('.add').click(addClickHandler);
});

function setupOrigami() {
    $('.customPage').append(function() {
        var switchLabel = localizationManager.getText('EditTab.LayoutMode.ChangeLayout', 'Change Layout');
        return '\
<div class="origami-toggle edit-mode bloom-ui">' + switchLabel + ' \
    <div class="onoffswitch"> \
        <input type="checkbox" name="onoffswitch" class="onoffswitch-checkbox" id="myonoffswitch"> \
        <label class="onoffswitch-label" for="myonoffswitch"> \
            <span class="onoffswitch-inner"></span> \
            <span class="onoffswitch-switch"></span> \
        </label> \
    </div> \
</div>';
    });

    $('.origami-ui').css('visibility', 'hidden');

    $('.origami-toggle .onoffswitch').change(function() {
        var toggle = $(this).closest('.origami-toggle');
        if (toggle.hasClass('edit-mode')) {
            toggle.removeClass('edit-mode');
            toggle.addClass('layout-mode');

            setupLayoutMode();
        } else {
            toggle.removeClass('layout-mode');
            toggle.addClass('edit-mode');

            fireCSharpEditEvent('reloadPageEvent', '');
        }
    });
}

function cleanupOrigami() {
    // Otherwise, we get a new one each time the page is loaded
    $('.split-pane-resize-shim').remove();
}

function setupLayoutMode() {
    $('.split-pane-component-inner').each(function() {
        if (!$(this).find('.bloom-imageContainer, .bloom-translationGroup').length)
            $(this).append(getTypeSelectors());
        $(this).append(getButtons());
    });
    $('.origami-ui').css('visibility', 'visible');
    // Text should not be edittable in layout mode
    $('.bloom-editable:visible[contentEditable=true]').removeAttr('contentEditable');
    // Images cannot be changed (other than growing/shrinking with container) in layout mode
    $('.bloom-imageContainer').off('mouseenter').off('mouseleave');
}

// Event handler to split the current box in half (vertically or horizontally)
function splitClickHandler() {
    var myInner = $(this).closest('.split-pane-component-inner');
    var newSplitPane;
    if ($(this).hasClass('vertical')) {
        myInner.wrap(getSplitPaneHtml('vertical'));
        myInner.wrap(getSplitPaneComponentHtml('left'));
        newSplitPane = myInner.closest('.split-pane');
        newSplitPane.append(getSplitPaneDividerHtml('vertical'));
        newSplitPane.append(getSplitPaneComponentWithNewContent('right'));
    } else {
        myInner.wrap(getSplitPaneHtml('horizontal'));
        myInner.wrap(getSplitPaneComponentHtml('top'));
        newSplitPane = myInner.closest('.split-pane');
        newSplitPane.append(getSplitPaneDividerHtml('horizontal'));
        newSplitPane.append(getSplitPaneComponentWithNewContent('bottom'));
    }
    newSplitPane.splitPane();
}

// Event handler to add a new column or row (currently only works for column)
function addClickHandler() {
    var topSplitPane = $('.split-pane-frame').children('div').first();
    if ($(this).hasClass('right')) {
        topSplitPane.wrap(getSplitPaneHtml('vertical'));
        topSplitPane.wrap(getSplitPaneComponentHtml('left'));
        var newSplitPane = $('.split-pane-frame').children('div').first();
        newSplitPane.append(getSplitPaneDividerHtml('vertical'));
        newSplitPane.append(getSplitPaneComponentWithNewContent('right'));
        newSplitPane.splitPane();
    } else if ($(this).hasClass('left')) {
        topSplitPane.wrap(getSplitPaneHtml('vertical'));
        topSplitPane.wrap(getSplitPaneComponentHtml('right'));
        var newSplitPane = $('.split-pane-frame').children('div').first();
        newSplitPane.prepend(getSplitPaneDividerHtml('vertical'));
        newSplitPane.prepend(getSplitPaneComponentWithNewContent('left'));
        newSplitPane.splitPane();
    }
}

function closeClickHandler() {
    if (!$('.split-pane'))
        // We're at the topmost element; don't allow deletion
        return;

    var myComponent = $(this).closest('.split-pane-component');
    var sibling = myComponent.siblings('.split-pane-component').first('div');
    var toReplace = myComponent.parent().parent();
    var positionClass = toReplace.attr('class');
    toReplace.replaceWith(sibling);

    // The idea here is we need the position-* class from the parent to replace the sibling's position-* class.
    // This is working for now, but should be cleaned up since it could also add other classes.
    sibling.removeClass(function (index, css) {
        return (css.match (/(^|\s)position-\S+/g) || []).join(' ');
    });
    sibling.addClass(positionClass);
}

function getSplitPaneHtml(verticalOrHorizontal) {
    return $('<div class="split-pane ' + verticalOrHorizontal + '-percent"></div>');
}
function getSplitPaneComponentHtml(leftOrRight) {
    return $('<div class="split-pane-component position-' + leftOrRight + '"></div>');
}
function getSplitPaneDividerHtml(verticalOrHorizontal) {
    return $('<div class="split-pane-divider ' + verticalOrHorizontal + '-divider origami-ui"></div>');
}
function getSplitPaneComponentWithNewContent(rightOrBottom) {
    var spc = $('<div class="split-pane-component position-' + rightOrBottom + '">');
    spc.append(getSplitPaneComponentInner());
    return spc;
}
function getSplitPaneComponentInner() {
    var spci = $('<div class="split-pane-component-inner"><div class="box-header"></div></div>');
    spci.append(getTypeSelectors());
    spci.append(getButtons());
    return spci;
}

function getButtons() {
    var buttons = $('<div class="origami-buttons bloom-ui origami-ui"></div>');
    buttons.append(getVSplitButton())
        .append('&nbsp;')
        .append(getHSplitButton())
        .append('&nbsp;')
        .append(getCloseButton());
    return buttons;
}
function getVSplitButton() {
    var vSplitButton = $('<a class="button bloom-purple splitter vertical">&#124;</a>');
    vSplitButton.click(splitClickHandler);
    return vSplitButton;
}
function getHSplitButton() {
    var hSplitButton = $('<a class="button bloom-purple splitter horizontal">&#8212;&#8212;</a>');
    hSplitButton.click(splitClickHandler);
    return hSplitButton;
}
function getCloseButton() {
    var closeButton = $('<a class="button bloom-purple close">&#215;</a>');
    closeButton.click(closeClickHandler);
    return closeButton;
}
function getTypeSelectors() {
    var links = $('<div class="selector-links bloom-ui origami-ui"></div>');
    var pictureLink = $('<a href="">Picture</a>');
    pictureLink.click(makePictureFieldClickHandler);
    var textLink = $('<a href="">Text</a>');
    textLink.click(makeTextFieldClickHandler);
    links.append(pictureLink).append(' or ').append(textLink);
    return links;
}
function makeTextFieldClickHandler(e) {
    e.preventDefault();
    var translationGroup = $('<div class="bloom-translationGroup bloom-trailingElement normal-style"><div lang="z" class="bloom-content1 bloom-editable"></div>');
    $(this).closest('.split-pane-component-inner').append(translationGroup);
    $(this).closest('.selector-links').remove();
    //TODO: figure out what needs to get hooked up immediately
}
function makePictureFieldClickHandler(e) {
    e.preventDefault();
    var imageContainer = $('<div class="bloom-imageContainer bloom-leadingElement"></div>');
    var image = $('<img src="placeHolder.png" alt="Could not load the picture"/>');
    SetupImage(image);
    imageContainer.append(image);
    $(this).closest('.split-pane-component-inner').append(imageContainer);
    $(this).closest('.selector-links').remove();
    //TODO: figure out what (else) needs to get hooked up immediately, e.g. pictures resize when added but not when slider moves
}