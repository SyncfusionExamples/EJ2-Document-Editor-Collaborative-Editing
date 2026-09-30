import { hideSpinner, showSpinner } from '@syncfusion/ej2-popups';
import { CollaborationClient } from "@syncfusion/ej2-collaborator";
import { DocumentEditorAdapter } from "../Collaborator/DocumentEditorAdapter";
//Collaborative editing controller url

var serviceUrl = 'http://localhost:5212/';
var connectionId = "";
var currentRoomName = '';
/**
 * Container component
 */
var container = new ej.documenteditor.DocumentEditorContainer({ height: "590px", enableToolbar: true, showPropertiesPane: false, currentUser: 'Guest User' });
container.serviceUrl = serviceUrl + 'api/documenteditor/';
ej.documenteditor.DocumentEditorContainer.Inject(ej.documenteditor.Toolbar);
container.appendTo('#container');

//Injecting collaborative editing module
ej.documenteditor.DocumentEditor.Inject(ej.documenteditor.CollaborativeEditingHandler);
//Enable collaborative editing in DocumentEditor
container.documentEditor.enableCollaborativeEditing = true;

container.documentEditor.documentName = 'Getting Started';

const adapter = new DocumentEditorAdapter(container, 'http://localhost:5212/');
const client = new CollaborationClient(adapter, {
    serviceUrl: 'http://localhost:5212/',
    currentUser: 'Guest User',
    connectionType: "signalr",
    onUserJoined: (user) => {
        console.log("User Joined", user);
    },
    onUserLeft: (user) => {
        console.log("User Left", user);
    }
});

(async () => {
    const roomName = await adapter.loadFromServer("Giant Panda.docx");
    await client.joinRoomAsync(roomName);
    console.log("Loaded document from room:", roomName);
})();

// title bar
var TitleBar = function () {
    function TitleBar(element, docEditor, isShareNeeded, isRtl) {
        var _this = this;
        this.userMap = {};
        this.initializeTitleBar = function (isShareNeeded) {
            var shareText;
            var shareToolTip;
            var documentTileText;
            if (!_this.isRtl) {
                shareText = 'Share';
                shareToolTip = 'Share this link';
            }
            _this.documentTitle = ej.base.createElement('label', { id: 'documenteditor_title_name', styles: 'font-weight:400;text-overflow:ellipsis;white-space:pre;overflow:hidden;user-select:none;cursor:text' });
            var iconCss = 'e-de-padding-right';
            var btnFloatStyle = 'float:right;';
            var titleCss = '';
            _this.documentTitleContentEditor = ej.base.createElement('div', { id: 'documenteditor_title_contentEditor', className: 'single-line', styles: titleCss });
            _this.documentTitleContentEditor.appendChild(_this.documentTitle);
            _this.tileBarDiv.appendChild(_this.documentTitleContentEditor);
            _this.documentTitleContentEditor.setAttribute('title', documentTileText);
            var btnStyles = btnFloatStyle + 'background: transparent;box-shadow:none; font-family: inherit;border-color: transparent;'
                + 'border-radius: 2px;color:inherit;font-size:12px;text-transform:capitalize;height:28px;font-weight:400;margin-top: 2px;';
            _this.print = _this.addButton('e-de-icon-Print ' + iconCss, shareText, btnStyles, 'de-print', shareToolTip, false);
            _this.userList = ej.base.createElement('div', { id: 'de_userInfo', styles: 'float:right;margin-top: 3px;' });
            _this.tileBarDiv.appendChild(_this.userList);
        };
        this.wireEvents = function () {
            _this.print.element.addEventListener('click', _this.shareUrl);
        };
        this.shareUrl = function () {

        },
            this.updateDocumentTitle = function () {
                if (_this.documentEditor.documentName === '') {
                    _this.documentEditor.documentName = 'Untitled';
                }
                _this.documentTitle.textContent = _this.documentEditor.documentName;
            };
        this.onPrint = function () {
            _this.documentEditor.print();
        };
        this.tileBarDiv = element;
        this.documentEditor = docEditor;
        this.isRtl = isRtl;
        this.initializeTitleBar(isShareNeeded);
        this.wireEvents();

    }
    TitleBar.prototype.addButton = function (iconClass, btnText, styles, id, tooltipText, isDropDown, items) {
        var button = ej.base.createElement('button', { id: id, styles: styles });
        this.tileBarDiv.appendChild(button);
        button.setAttribute('title', tooltipText);
        var ejButton = new ej.buttons.Button({ iconCss: iconClass, content: btnText }, button);
        return ejButton;
    };
    TitleBar.prototype.addUser = function (actionInfos) {
        if (!(actionInfos instanceof Array)) {
            actionInfos = [actionInfos];
        }
        for (var i = 0; i < actionInfos.length; i++) {
            var actionInfo = actionInfos[i];
            if (this.userMap[actionInfo.connectionId]) {
                continue;
            }
            var avatar = ej.base.createElement('div', { className: 'e-avatar e-avatar-xsmall e-avatar-circle', styles: 'margin: 0px 5px', innerHTML: this.constructInitial(actionInfo.currentUser) });
            this.userMap[actionInfo.connectionId] = avatar;
            avatar.title = actionInfo.currentUser;
            this.userList.appendChild(avatar);
        }
    };
    TitleBar.prototype.removeUser = function (conectionId) {
        if (this.userMap[conectionId]) {
            this.userList.removeChild(this.userMap[conectionId]);
            delete this.userMap[conectionId];
        }
    };
    TitleBar.prototype.constructInitial = function (authorName) {
        var splittedName = authorName.split(' ');
        var initials = '';
        for (var i = 0; i < splittedName.length; i++) {
            if (splittedName[i].length > 0 && splittedName[i] !== '') {
                initials += splittedName[i][0];
            }
        }
        return initials;


    };
    return TitleBar;
}();

var titleBar = new TitleBar(document.getElementById('documenteditor_titlebar'), container.documentEditor, true);

var tooltip = new ej.popups.Tooltip({
    cssClass: 'e-tooltip-template-css',
    //Set tooltip open mode
    opensOn: 'Click Custom Focus',
    //Set tooltip content
    content: createPopUpDisplay(),
    beforeRender: onBeforeRender,
    afterOpen: onAfterOpen,
    width: "400px"
});
//Render initialized Tooltip component
tooltip.appendTo('#de-print');

function onBeforeRender() {
    if (document.getElementById('tooltipContent')) {
        document.getElementById('tooltipContent').style.display = 'block';
    }
}
function onAfterOpen() {
    document.getElementById("share_url").value = window.location.href;
}

function copyUrl() {
    // Get the text field
    var copyText = document.getElementById("share_url");

    // Select the text field
    copyText.select();
    copyText.setSelectionRange(0, 99999); // For mobile devices

    // Copy the text inside the text field
    navigator.clipboard.writeText(copyText.value);
}
function createPopUpDisplay() {
    //Creatin the copy link element
    var tooltip = ej.base.createElement('div', { id: 'tooltipContent', styles: 'display:none' });
    var tooltipClass = ej.base.createElement('div', { className: 'content' });
    var firstChild = ej.base.createElement('div', { styles: 'margin-bottom:12px;font-size:15px', })
    firstChild.textContent = 'Share this URL with other for realtime editing';
    var secondchild = ej.base.createElement('div', { styles: 'display:flex' });
    secondchild.appendChild(ej.base.createElement('input', { id: 'share_url', className: 'e-input', attrs: { type: 'text' } }));
    var copyButton = ej.base.createElement('button', {
        styles: 'margin-left:10px',
        className: 'e-primary e-btn',
        innerHTML: 'Copy Url'
    });
    copyButton.addEventListener('click', copyUrl);
    secondchild.appendChild(copyButton);
    tooltipClass.appendChild(firstChild);
    tooltipClass.appendChild(secondchild);
    return tooltip.appendChild(tooltipClass);
}
